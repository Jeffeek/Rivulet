using FluentAssertions;

namespace Rivulet.Core.Tests;

public class OrderedOutputTests
{
    [Fact]
    public async Task SelectParallelAsync_WithOrderedOutput_ReturnsResultsInOrder()
    {
        // Arrange
        var source = Enumerable.Range(1, 100).ToArray();
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 16,
            OrderedOutput = true
        };

        // Act
        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                // Simulate varying processing times to ensure out-of-order completion
                await Task.Delay(Random.Shared.Next(1, 10), ct);
                return x * 2;
            },
            options);

        // Assert
        results.Should().HaveCount(100);
        results.Should().Equal(Enumerable.Range(1, 100).Select(x => x * 2));
    }

    [Fact]
    public async Task SelectParallelAsync_WithoutOrderedOutput_MayReturnOutOfOrder()
    {
        // Arrange
        var source = Enumerable.Range(1, 50).ToArray();
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 16,
            OrderedOutput = false // Default
        };

        var orderedCount = 0;
        var totalRuns = 10;
        List<int> results = null!;

        // Act - run multiple times to check if ordering varies
        for (var run = 0; run < totalRuns; run++)
        {
            results = await source.SelectParallelAsync(
                async (x, ct) =>
                {
                    await Task.Delay(Random.Shared.Next(1, 5), ct);
                    return x;
                },
                options);

            if (results.SequenceEqual(source))
                orderedCount++;
        }

        results.Should().HaveCount(50);
        orderedCount.Should().Be(0);
        results.Should().BeEquivalentTo(source, "all items should be present regardless of order");
    }

    [Fact]
    public async Task SelectParallelStreamAsync_WithOrderedOutput_StreamsResultsInOrder()
    {
        // Arrange
        var source = Enumerable.Range(1, 100).ToAsyncEnumerable();
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 16,
            OrderedOutput = true
        };

        var results = await source.SelectParallelStreamAsync(async (x, ct) =>
            {
                await Task.Delay(Random.Shared.Next(1, 10), ct);
                return x * 2;
            }, options)
            .ToListAsync();

        // Act

        // Assert
        results.Should().HaveCount(100);
        results.Should().Equal(Enumerable.Range(1, 100).Select(x => x * 2));
    }

    [Fact]
    public async Task SelectParallelStreamAsync_WithOrderedOutput_BuffersAndYieldsInSequence()
    {
        // Arrange - Create a scenario where items complete out of order
        var source = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }.ToAsyncEnumerable();
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 10,
            OrderedOutput = true
        };

        var results = new List<int>();
        var completionOrder = new List<int>();

        // Act
        await foreach (var result in source.SelectParallelStreamAsync(
            async (x, ct) =>
            {
                // Items with higher numbers complete faster (reverse order)
                await Task.Delay((11 - x) * 10, ct);
                lock (completionOrder) completionOrder.Add(x);
                return x;
            },
            options))
        {
            results.Add(result);
        }

        // Assert
        results.Should().Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, "results should be in order");
        completionOrder.Should().NotEqual(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, "completion should be out of order");
    }

    [Fact]
    public async Task SelectParallelAsync_OrderedOutput_WithErrors_MaintainsOrderForSuccessful()
    {
        // Arrange
        var source = Enumerable.Range(1, 20).ToArray();
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 8,
            OrderedOutput = true,
            ErrorMode = ErrorMode.BestEffort
        };

        // Act
        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(Random.Shared.Next(1, 5), ct);
                if (x % 5 == 0) throw new InvalidOperationException($"Error at {x}");
                return x * 2;
            },
            options);

        // Assert - Should contain results for items that didn't fail, in order
        var expected = Enumerable.Range(1, 20)
            .Where(x => x % 5 != 0)
            .Select(x => x * 2)
            .ToList();

        results.Should().Equal(expected);
    }

    [Fact]
    public async Task SelectParallelStreamAsync_OrderedOutput_WithCancellation_YieldsInOrder()
    {
        // Arrange
        var source = Enumerable.Range(1, 100).ToAsyncEnumerable();
        var cts = new CancellationTokenSource();
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 16,
            OrderedOutput = true
        };

        var results = new List<int>();

        // Act
        try
        {
            await foreach (var result in source.SelectParallelStreamAsync(
                async (x, ct) =>
                {
                    await Task.Delay(Random.Shared.Next(1, 5), ct);
                    return x * 2;
                },
                options,
                cts.Token))
            {
                results.Add(result);
                if (results.Count >= 20)
                {
                    await cts.CancelAsync();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - The results we got should be in order
        results.Take(20).Should().Equal(Enumerable.Range(1, 20).Select(x => x * 2));
    }

    [Fact]
    public async Task SelectParallelAsync_OrderedOutput_EmptySource_ReturnsEmpty()
    {
        // Arrange
        var source = Enumerable.Empty<int>();
        var options = new ParallelOptionsRivulet { OrderedOutput = true };

        // Act
        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x * 2;
            },
            options);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SelectParallelAsync_OrderedOutput_SingleItem_ReturnsSingleItem()
    {
        // Arrange
        var source = new[] { 42 };
        var options = new ParallelOptionsRivulet { OrderedOutput = true };

        // Act
        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x * 2;
            },
            options);

        // Assert
        results.Should().Equal(new[] { 84 });
    }

    [Fact]
    public async Task SelectParallelStreamAsync_OrderedOutput_WithRetries_MaintainsOrder()
    {
        // Arrange
        var source = Enumerable.Range(1, 20).ToAsyncEnumerable();
        var attemptCounts = new System.Collections.Concurrent.ConcurrentDictionary<int, int>();
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 8,
            OrderedOutput = true,
            MaxRetries = 2,
            IsTransient = ex => ex is InvalidOperationException
        };

        var results = new List<int>();

        // Act
        await foreach (var result in source.SelectParallelStreamAsync(
            async (x, ct) =>
            {
                await Task.Delay(Random.Shared.Next(1, 5), ct);
                var attempts = attemptCounts.AddOrUpdate(x, 1, (_, count) => count + 1);

                // Fail on first attempt for every third item
                if (attempts == 1 && x % 3 == 0)
                    throw new InvalidOperationException($"Transient error at {x}");

                return x * 2;
            },
            options))
        {
            results.Add(result);
        }

        // Assert
        results.Should().Equal(Enumerable.Range(1, 20).Select(x => x * 2));
        attemptCounts.Values.Should().Contain(v => v > 1, "some items should have retried");
    }

    [Fact]
    public async Task SelectParallelAsync_OrderedOutput_LargeDataset_MaintainsOrder()
    {
        // Arrange
        var source = Enumerable.Range(1, 1000).ToArray();
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 32,
            OrderedOutput = true
        };

        // Act
        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(Random.Shared.Next(1, 3), ct);
                return x;
            },
            options);

        // Assert
        results.Should().HaveCount(1000);
        results.Should().Equal(source, "large dataset should maintain order");
    }

    [Fact]
    public async Task SelectParallelStreamAsync_OrderedOutput_WithLifecycleHooks_MaintainsOrder()
    {
        // Arrange
        var source = Enumerable.Range(1, 50).ToAsyncEnumerable();
        var startedItems = new System.Collections.Concurrent.ConcurrentBag<int>();
        var completedItems = new System.Collections.Concurrent.ConcurrentBag<int>();

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 16,
            OrderedOutput = true,
            OnStartItemAsync = async (idx) =>
            {
                startedItems.Add(idx);
                await Task.CompletedTask;
            },
            OnCompleteItemAsync = async (idx) =>
            {
                completedItems.Add(idx);
                await Task.CompletedTask;
            }
        };

        var results = new List<int>();

        // Act
        await foreach (var result in source.SelectParallelStreamAsync(
            async (x, ct) =>
            {
                await Task.Delay(Random.Shared.Next(1, 5), ct);
                return x * 2;
            },
            options))
        {
            results.Add(result);
        }

        // Assert
        results.Should().Equal(Enumerable.Range(1, 50).Select(x => x * 2));
        startedItems.Should().HaveCount(50);
        completedItems.Should().HaveCount(50);
    }
}
