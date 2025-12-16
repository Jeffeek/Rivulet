using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Rivulet.Core.Tests;

[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public sealed class CancellationAndTimeoutTests
{
    [Fact]
    public async Task SelectParallelAsync_Cancellation_ThrowsOperationCanceledException()
    {
        var source = Enumerable.Range(1, 100);
        using var cts = new CancellationTokenSource();
        var processedCount = 0;

        Task<List<int>> Act() =>
            source.SelectParallelAsync(async (x, ct) =>
                {
                    Interlocked.Increment(ref processedCount);
                    await Task.Delay(100, ct);
                    return x * 2;
                },
                cancellationToken: cts.Token);

        var task = Act();
        await Task.Delay(50, cts.Token);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        processedCount.ShouldBeLessThan(100);
    }

    [Fact]
    public async Task SelectParallelStreamAsync_Cancellation_ThrowsOperationCanceledException()
    {
        var source = Enumerable.Range(1, 100).ToAsyncEnumerable();
        using var cts = new CancellationTokenSource();
        var results = new List<int>();

        async Task Act()
        {
            await foreach (var item in source.SelectParallelStreamAsync(async (x, ct) =>
                               {
                                   await Task.Delay(100, ct);
                                   return x * 2;
                               },
                               cancellationToken: cts.Token))
                results.Add(item);
        }

        var task = Act();
        await Task.Delay(50, cts.Token);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        results.Count.ShouldBeLessThan(100);
    }

    [Fact]
    public async Task ForEachParallelAsync_Cancellation_ThrowsOperationCanceledException()
    {
        var source = Enumerable.Range(1, 100).ToAsyncEnumerable();
        using var cts = new CancellationTokenSource();
        var processedCount = 0;

        Task Act() =>
            source.ForEachParallelAsync(async (_, ct) =>
                {
                    Interlocked.Increment(ref processedCount);
                    await Task.Delay(100, ct);
                },
                cancellationToken: cts.Token);

        var task = Act();
        await Task.Delay(50, cts.Token);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        processedCount.ShouldBeLessThan(100);
    }

    [Fact]
    public async Task PerItemTimeout_ItemExceedsTimeout_ThrowsException()
    {
        var source = Enumerable.Range(1, 5);
        var options = new ParallelOptionsRivulet
        {
            PerItemTimeout = TimeSpan.FromMilliseconds(2000), ErrorMode = ErrorMode.BestEffort, MaxDegreeOfParallelism = 2
        };

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                if (x == 3)
                    await Task.Delay(10000, ct);
                else
                    await Task.Delay(1, ct);

                return x * 2;
            },
            options);

        results.Count.ShouldBe(4, "items 1,2,4,5 complete within 2s timeout, item 3 exceeds timeout with 10s delay");
        results.ShouldNotContain(6, "item 3 (result=6) should timeout and be excluded from results");
    }

    [Fact]
    public async Task PerItemTimeout_AllItemsWithinTimeout_Succeeds()
    {
        var source = Enumerable.Range(1, 10);
        var options = new ParallelOptionsRivulet { PerItemTimeout = TimeSpan.FromMilliseconds(2000) };

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(50, ct);
                return x * 2;
            },
            options);

        results.Count.ShouldBe(10, "all items should complete within the 2s timeout");
    }

    [Fact]
    public async Task PerItemTimeout_NullTimeout_NoTimeoutEnforced()
    {
        var source = Enumerable.Range(1, 3);
        var options = new ParallelOptionsRivulet { PerItemTimeout = null };

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(200, ct);
                return x * 2;
            },
            options);

        results.Count.ShouldBe(3);
    }

    [Fact]
    public async Task PerItemTimeout_WithRetries_TimeoutAppliedToEachAttempt()
    {
        var source = new[] { 1 };
        var attemptCount = 0;
        var options = new ParallelOptionsRivulet
        {
            PerItemTimeout = TimeSpan.FromMilliseconds(100),
            MaxRetries = 2,
            BaseDelay = TimeSpan.FromMilliseconds(10),
            IsTransient = ex => ex is OperationCanceledException,
            ErrorMode = ErrorMode.BestEffort
        };

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                attemptCount++;
                await Task.Delay(1000, ct);
                return x * 2;
            },
            options);

        results.ShouldBeEmpty("all 3 attempts (initial + 2 retries) should timeout with 1s delay vs 100ms timeout");
        attemptCount.ShouldBe(3, "operation should be attempted 3 times (initial attempt + 2 retries)");
    }

    [Fact]
    public async Task PerItemTimeout_SelectParallelStreamAsync_HandlesTimeout()
    {
        var source = Enumerable.Range(1, 5).ToAsyncEnumerable();
        var options = new ParallelOptionsRivulet { PerItemTimeout = TimeSpan.FromMilliseconds(200), ErrorMode = ErrorMode.BestEffort };

        var results = await source.SelectParallelStreamAsync(async (x, ct) =>
                {
                    if (x == 3)
                        await Task.Delay(1000, ct);
                    else
                        await Task.Delay(5, ct);

                    return x * 2;
                },
                options)
            .ToListAsync();

        results.Count.ShouldBe(4);
    }

    [Fact]
    public async Task Cancellation_DuringItemProcessing_PropagatesCancellation()
    {
        var source = Enumerable.Range(1, 200);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        Task<List<int>> Act() =>
            source.SelectParallelAsync(async (x, ct) =>
                {
                    await Task.Delay(20, ct);
                    ct.ThrowIfCancellationRequested();
                    return x * 2;
                },
                cancellationToken: cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>((Func<Task<List<int>>>)Act);
    }

    [Fact]
    public async Task Cancellation_BeforeStart_ThrowsImmediately()
    {
        var source = Enumerable.Range(1, 100);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Task<List<int>> Act() => source.SelectParallelAsync((x, _) => new ValueTask<int>(x * 2), cancellationToken: cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>((Func<Task<List<int>>>)Act);
    }

    [Fact]
    public async Task CancellationToken_PassedToTaskSelector()
    {
        var source = Enumerable.Range(1, 5);
        var tokensPassed = new ConcurrentBag<bool>();

        var results = await source.SelectParallelAsync((x, ct) =>
        {
            tokensPassed.Add(ct != CancellationToken.None);
            return new ValueTask<int>(x * 2);
        });

        results.Count.ShouldBe(5);
        tokensPassed.ShouldAllBe(token => token);
    }

    [Fact]
    public async Task PerItemTimeout_WithFailFast_ThrowsOnTimeout()
    {
        var source = Enumerable.Range(1, 10);
        var options = new ParallelOptionsRivulet
        {
            PerItemTimeout = TimeSpan.FromMilliseconds(50), ErrorMode = ErrorMode.FailFast, MaxDegreeOfParallelism = 1
        };

        Task<List<int>> Act() =>
            source.SelectParallelAsync(async (x, ct) =>
                {
                    if (x == 5)
                        await Task.Delay(500, ct);
                    else
                        await Task.Delay(10, ct);

                    return x * 2;
                },
                options);

        await Assert.ThrowsAnyAsync<OperationCanceledException>((Func<Task<List<int>>>)Act);
    }

    [Fact]
    public async Task PerItemTimeout_WithCollectAndContinue_CollectsTimeoutErrors()
    {
        var source = Enumerable.Range(1, 10);
        var options = new ParallelOptionsRivulet
        {
            PerItemTimeout = TimeSpan.FromMilliseconds(50), ErrorMode = ErrorMode.CollectAndContinue, MaxDegreeOfParallelism = 1
        };

        Task<List<int>> Act() =>
            source.SelectParallelAsync(async (x, ct) =>
                {
                    if (x is 3 or 7)
                        await Task.Delay(300, ct);
                    else
                        await Task.Delay(5, ct);

                    return x * 2;
                },
                options);

        var exception = await Assert.ThrowsAsync<AggregateException>(((Func<Task<List<int>>>?)Act)!);
        exception.InnerExceptions.Count.ShouldBeGreaterThanOrEqualTo(1);
        exception.InnerExceptions.ShouldAllBe(x => x is OperationCanceledException);
    }

    [Fact]
    public async Task CombinedCancellationAndTimeout_BothWork()
    {
        var source = Enumerable.Range(1, 100);
        using var cts = new CancellationTokenSource();
        var options = new ParallelOptionsRivulet
        {
            PerItemTimeout = TimeSpan.FromMilliseconds(20), ErrorMode = ErrorMode.BestEffort, MaxDegreeOfParallelism = 2
        };

        var task = source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(1000, ct);
                return x * 2;
            },
            options,
            cts.Token);

        await Task.Delay(10, CancellationToken.None);
        await cts.CancelAsync();

        var results = await task;

        results.Count.ShouldBeLessThanOrEqualTo(2,
            "with 20ms timeout and 1000ms delays, plus cancellation after 10ms, at most 2 items should complete");
    }

    [Fact]
    public async Task Cancellation_WriterTask_StopsProducingItems()
    {
        var source = Enumerable.Range(1, 1000);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var processedCount = 0;

        Task<List<int>> Act() =>
            source.SelectParallelAsync(async (x, ct) =>
                {
                    Interlocked.Increment(ref processedCount);
                    await Task.Delay(10, ct);
                    return x * 2;
                },
                cancellationToken: cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>((Func<Task<List<int>>>)Act);
        processedCount.ShouldBeLessThan(1000);
    }

    [Fact]
    public async Task SelectParallelStreamAsync_ConsumerCancellation_Cancels()
    {
        var source = Enumerable.Range(1, 1000).ToAsyncEnumerable();
        using var cts = new CancellationTokenSource();
        var consumedCount = 0;

        async Task Act()
        {
            // ReSharper disable once PossibleMultipleEnumeration
            await foreach (var _ in source.SelectParallelStreamAsync((x, _) => new ValueTask<int>(x * 2), cancellationToken: cts.Token))
            {
                consumedCount++;
                if (consumedCount == 10) await cts.CancelAsync();
            }
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(Act);
        consumedCount.ShouldBeGreaterThanOrEqualTo(10);
        consumedCount.ShouldBeLessThan(1000);
    }
}