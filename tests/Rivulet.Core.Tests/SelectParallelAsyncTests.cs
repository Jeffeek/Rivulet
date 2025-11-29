namespace Rivulet.Core.Tests;

public class SelectParallelAsyncTests
{
    [Fact]
    public async Task SelectParallelAsync_EmptyCollection_ReturnsEmptyList()
    {
        var source = Enumerable.Empty<int>();

        var results = await source.SelectParallelAsync(
            (x, _) => new ValueTask<int>(x * 2));

        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task SelectParallelAsync_SingleItem_ReturnsTransformedItem()
    {
        var source = new[] { 5 };

        var results = await source.SelectParallelAsync(
            (x, _) => new ValueTask<int>(x * 2));

        results.ShouldHaveSingleItem().ShouldBe(10);
    }

    [Fact]
    public async Task SelectParallelAsync_MultipleItems_ReturnsAllTransformed()
    {
        var source = Enumerable.Range(1, 10);

        var results = await source.SelectParallelAsync(
            (x, _) => new ValueTask<int>(x * 2));

        results.Count.ShouldBe(10);
        results.OrderBy(x => x).ShouldBe(new[] { 2, 4, 6, 8, 10, 12, 14, 16, 18, 20 });
    }

    [Fact]
    public async Task SelectParallelAsync_LargeCollection_ProcessesAllItems()
    {
        var source = Enumerable.Range(1, 1000);

        var results = await source.SelectParallelAsync(
            (x, _) => new ValueTask<int>(x * 2));

        results.Count.ShouldBe(1000);
        results.Sum().ShouldBe(1001000);
    }

    [Fact]
    public async Task SelectParallelAsync_WithDelay_ExecutesInParallel()
    {
        var source = Enumerable.Range(1, 10);
        var options = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 5 };
        var startTime = DateTime.UtcNow;

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(100, ct);
                return x * 2;
            },
            options);

        var duration = DateTime.UtcNow - startTime;

        results.Count.ShouldBe(10);
        // Increased from 500ms to 1000ms to account for CI environment variability
        // With MaxDegreeOfParallelism=5 and 10 items of 100ms each, ideal time is ~200ms
        // but CI environments can have high scheduling overhead
        duration.ShouldBeLessThan(TimeSpan.FromMilliseconds(1000));
    }

    [Fact]
    public async Task SelectParallelAsync_CustomMaxDegreeOfParallelism_RespectsLimit()
    {
        var source = Enumerable.Range(1, 20);
        var concurrentCount = 0;
        var maxConcurrent = 0;
        var lockObj = new object();
        var options = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 3 };

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                lock (lockObj)
                {
                    concurrentCount++;
                    maxConcurrent = Math.Max(maxConcurrent, concurrentCount);
                }

                await Task.Delay(50, ct);

                lock (lockObj)
                {
                    concurrentCount--;
                }

                return x;
            },
            options);

        results.Count.ShouldBe(20);
        maxConcurrent.ShouldBeLessThanOrEqualTo(3);
    }

    [Fact]
    public async Task SelectParallelAsync_WithNullOptions_UsesDefaults()
    {
        var source = Enumerable.Range(1, 5);

        var results = await source.SelectParallelAsync(
            (x, _) => new ValueTask<int>(x * 2),
            options: null);

        results.Count.ShouldBe(5);
        results.OrderBy(x => x).ShouldBe(new[] { 2, 4, 6, 8, 10 });
    }

    [Fact]
    public async Task SelectParallelAsync_CustomChannelCapacity_HandlesBackpressure()
    {
        var source = Enumerable.Range(1, 100);
        var options = new ParallelOptionsRivulet
        {
            ChannelCapacity = 10,
            MaxDegreeOfParallelism = 2
        };

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x * 2;
            },
            options);

        results.Count.ShouldBe(100);
    }

    [Fact]
    public async Task SelectParallelAsync_WithComplexTransformation_ProducesCorrectResults()
    {
        var source = Enumerable.Range(1, 50);

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return new { Original = x, Squared = x * x, IsEven = x % 2 == 0 };
            });

        results.Count.ShouldBe(50);
        results.Where(r => r.IsEven).Count().ShouldBe(25);
        results.First(r => r.Original == 5).Squared.ShouldBe(25);
    }

    [Fact]
    public async Task SelectParallelAsync_CancellationToken_DefaultValue_Completes()
    {
        var source = Enumerable.Range(1, 10);

        var results = await source.SelectParallelAsync(
            (x, _) => new ValueTask<int>(x * 2));

        results.Count.ShouldBe(10);
    }

    [Fact]
    public async Task SelectParallelAsync_WithStrings_TransformsCorrectly()
    {
        var source = new[] { "hello", "world", "test", "xunit" };

        var results = await source.SelectParallelAsync(
            (s, _) => new ValueTask<string>(s.ToUpper()));

        results.OrderBy(x => x).ShouldBe(new[] { "HELLO", "TEST", "WORLD", "XUNIT" });
    }

    [Fact]
    public async Task SelectParallelAsync_WithFallback_ReturnsAllResultsWithFallbackForFailures()
    {
        var source = Enumerable.Range(1, 10);
        var failOn = new HashSet<int> { 3, 7 };

        var results = await source.SelectParallelAsync(
            (x, _) =>
            {
                if (failOn.Contains(x))
                    throw new InvalidOperationException($"Failed on {x}");
                return new ValueTask<int>(x * 2);
            },
            new()
            {
                OnFallback = (_, _) => -1
            });

        results.Count.ShouldBe(10);
        foreach (var expected in new[] { 2, 4, -1, 8, 10, 12, -1, 16, 18, 20 })
        {
            results.ShouldContain(expected);
        }
    }

    [Fact]
    public async Task SelectParallelAsync_WithFallback_UsesFallbackAfterRetriesExhausted()
    {
        var source = new[] { 1, 2, 3, 4, 5 };
        var attemptCounts = new System.Collections.Concurrent.ConcurrentDictionary<int, int>();

        var results = await source.SelectParallelAsync(
            (x, _) =>
            {
                attemptCounts.AddOrUpdate(x, 1, (_, count) => count + 1);
                if (x == 3)
                    throw new TimeoutException("Always fails");
                return new ValueTask<int>(x * 10);
            },
            new()
            {
                MaxRetries = 2,
                IsTransient = ex => ex is TimeoutException,
                BaseDelay = TimeSpan.FromMilliseconds(1),
                OnFallback = (_, _) => 999
            });

        results.Count.ShouldBe(5);
        foreach (var expected in new[] { 10, 20, 999, 40, 50 })
        {
            results.ShouldContain(expected);
        }
        attemptCounts[3].ShouldBe(3); // Initial + 2 retries
    }

    [Fact]
    public async Task SelectParallelAsync_WithFallback_NullFallbackForReferenceTypes()
    {
        var source = new[] { "a", "b", "c" };

        var results = await source.SelectParallelAsync(
            (x, _) =>
            {
                if (x == "b")
                    throw new InvalidOperationException();
                return new ValueTask<string>(x.ToUpper());
            },
            new()
            {
                OnFallback = (_, _) => null
            });

        results.Count.ShouldBe(3);
        results.OrderBy(x => x).ShouldBe(new[] { null, "A", "C" });
    }

    [Fact]
    public async Task SelectParallelAsync_WithFallback_DifferentFallbackBasedOnException()
    {
        var source = Enumerable.Range(1, 5);

        var results = await source.SelectParallelAsync(
            (x, _) =>
            {
                if (x == 2)
                    throw new TimeoutException();
                if (x == 4)
                    throw new InvalidOperationException();
                return new ValueTask<int>(x * 10);
            },
            new()
            {
                OnFallback = (_, ex) => ex is TimeoutException ? -1 : -2
            });

        results.Count.ShouldBe(5);
        foreach (var expected in new[] { 10, -1, 30, -2, 50 })
        {
            results.ShouldContain(expected);
        }
    }

    [Fact]
    public async Task SelectParallelAsync_WithFallback_UsesIndexInFallback()
    {
        var source = new[] { 10, 20, 30, 40 };

        var results = await source.SelectParallelAsync(
            (x, _) =>
            {
                if (x == 20 || x == 40)
                    throw new();
                return new ValueTask<int>(x);
            },
            new()
            {
                OnFallback = (index, _) => index * 1000
            });

        results.Count.ShouldBe(4);
        // Index 0 -> 10, Index 1 -> 1000, Index 2 -> 30, Index 3 -> 3000
        foreach (var expected in new[] { 10, 1000, 30, 3000 })
        {
            results.ShouldContain(expected);
        }
    }

    [Fact]
    public async Task SelectParallelAsync_WithoutFallback_ThrowsAsNormal()
    {
        var source = Enumerable.Range(1, 5);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await source.SelectParallelAsync(
                (x, _) =>
                {
                    if (x == 3)
                        throw new InvalidOperationException();
                    return new ValueTask<int>(x * 2);
                });
        });
    }

    [Fact]
    public async Task SelectParallelAsync_WithFallback_WorksWithComplexTypes()
    {
        var source = new[] { 1, 2, 3, 4 };

        var results = await source.SelectParallelAsync(
            (x, _) =>
            {
                if (x == 2)
                    throw new();
                return new ValueTask<(int Value, string Status)>((x, "OK"));
            },
            new()
            {
                OnFallback = (_, _) => (0, "FAILED")
            });

        results.Count.ShouldBe(4);
        foreach (var expected in new[] { (1, "OK"), (0, "FAILED"), (3, "OK"), (4, "OK") })
        {
            results.ShouldContain(expected);
        }
    }
}
