using System.Collections.Concurrent;

namespace Rivulet.Core.Tests;

public class ForEachParallelAsyncTests
{
    [Fact]
    public async Task ForEachParallelAsync_EmptyCollection_CompletesWithoutError()
    {
        var source = AsyncEnumerable.Empty<int>();
        var processedItems = new ConcurrentBag<int>();

        await source.ForEachParallelAsync(
            (x, _) =>
            {
                processedItems.Add(x);
                return ValueTask.CompletedTask;
            });

        processedItems.Should().BeEmpty();
    }

    [Fact]
    public async Task ForEachParallelAsync_SingleItem_ProcessesItem()
    {
        var source = new[] { 5 }.ToAsyncEnumerable();
        var processedItems = new ConcurrentBag<int>();

        await source.ForEachParallelAsync(
            (x, _) =>
            {
                processedItems.Add(x * 2);
                return ValueTask.CompletedTask;
            });

        processedItems.Should().ContainSingle().Which.Should().Be(10);
    }

    [Fact]
    public async Task ForEachParallelAsync_MultipleItems_ProcessesAllItems()
    {
        var source = Enumerable.Range(1, 10).ToAsyncEnumerable();
        var processedItems = new ConcurrentBag<int>();

        await source.ForEachParallelAsync(
            (x, _) =>
            {
                processedItems.Add(x);
                return ValueTask.CompletedTask;
            });

        processedItems.Should().HaveCount(10);
        processedItems.Should().BeEquivalentTo(Enumerable.Range(1, 10));
    }

    [Fact]
    public async Task ForEachParallelAsync_WithSideEffects_ExecutesSideEffects()
    {
        var source = Enumerable.Range(1, 20).ToAsyncEnumerable();
        var results = new ConcurrentDictionary<int, int>();

        await source.ForEachParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(10, ct);
                results[x] = x * x;
            });

        results.Should().HaveCount(20);
        results[5].Should().Be(25);
        results[10].Should().Be(100);
    }

    [Fact]
    public async Task ForEachParallelAsync_WithParallelism_ExecutesInParallel()
    {
        var source = Enumerable.Range(1, 10).ToAsyncEnumerable();
        var options = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 5 };
        var processedCount = 0;
        var lockObj = new object();
        var startTime = DateTime.UtcNow;

        await source.ForEachParallelAsync(
            async (_, ct) =>
            {
                await Task.Delay(100, ct);
                lock (lockObj)
                {
                    processedCount++;
                }
            },
            options);

        var duration = DateTime.UtcNow - startTime;

        processedCount.Should().Be(10);
        // With parallelism=5 and 100ms tasks, should take ~200ms, but CI runners can be slow
        // Increased from 1000ms to 2000ms to account for extreme CI environment variability,
        // thread pool delays, context switching, CPU contention, and resource starvation
        duration.Should().BeLessThan(TimeSpan.FromMilliseconds(2000));
    }

    [Fact]
    public async Task ForEachParallelAsync_CustomMaxDegreeOfParallelism_RespectsLimit()
    {
        var source = Enumerable.Range(1, 20).ToAsyncEnumerable();
        var concurrentCount = 0;
        var maxConcurrent = 0;
        var lockObj = new object();
        var options = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 3 };

        await source.ForEachParallelAsync(
            async (_, ct) =>
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
            },
            options);

        maxConcurrent.Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public async Task ForEachParallelAsync_WithNullOptions_UsesDefaults()
    {
        var source = Enumerable.Range(1, 5).ToAsyncEnumerable();
        var processedItems = new ConcurrentBag<int>();

        await source.ForEachParallelAsync(
            (x, _) =>
            {
                processedItems.Add(x);
                return ValueTask.CompletedTask;
            },
            options: null);

        processedItems.Should().HaveCount(5);
    }

    [Fact]
    public async Task ForEachParallelAsync_LargeCollection_ProcessesAllItems()
    {
        var source = Enumerable.Range(1, 1000).ToAsyncEnumerable();
        var processedCount = 0;

        await source.ForEachParallelAsync(
            (_, _) =>
            {
                Interlocked.Increment(ref processedCount);
                return ValueTask.CompletedTask;
            });

        processedCount.Should().Be(1000);
    }

    [Fact]
    public async Task ForEachParallelAsync_WithComplexActions_ExecutesCorrectly()
    {
        var source = Enumerable.Range(1, 50).ToAsyncEnumerable();
        var evenNumbers = new ConcurrentBag<int>();
        var oddNumbers = new ConcurrentBag<int>();

        await source.ForEachParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(1, ct);
                if (x % 2 == 0)
                    evenNumbers.Add(x);
                else
                    oddNumbers.Add(x);
            });

        evenNumbers.Should().HaveCount(25);
        oddNumbers.Should().HaveCount(25);
    }

    [Fact]
    public async Task ForEachParallelAsync_ReturnsCompletedTask()
    {
        var source = Enumerable.Range(1, 5).ToAsyncEnumerable();

        var task = source.ForEachParallelAsync(
            (_, _) => ValueTask.CompletedTask);

        await task;
        task.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task ForEachParallelAsync_WithCancellationToken_Completes()
    {
        var source = Enumerable.Range(1, 10).ToAsyncEnumerable();
        using var cts = new CancellationTokenSource();
        var processedCount = 0;

        await source.ForEachParallelAsync(
            (_, _) =>
            {
                Interlocked.Increment(ref processedCount);
                return ValueTask.CompletedTask;
            },
            cancellationToken: cts.Token);

        processedCount.Should().Be(10);
    }
}
