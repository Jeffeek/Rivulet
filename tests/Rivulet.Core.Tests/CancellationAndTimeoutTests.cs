using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Rivulet.Core.Tests;

[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public class CancellationAndTimeoutTests
{
    [Fact]
    public async Task SelectParallelAsync_Cancellation_ThrowsOperationCanceledException()
    {
        var source = Enumerable.Range(1, 100);
        using var cts = new CancellationTokenSource();
        var processedCount = 0;

        async Task<List<int>> Act() =>
            await source.SelectParallelAsync(async (x, ct) =>
            {
                Interlocked.Increment(ref processedCount);
                await Task.Delay(100, ct);
                return x * 2;
            }, cancellationToken: cts.Token);

        var task = Act();
        await Task.Delay(50, cts.Token);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        processedCount.Should().BeLessThan(100);
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
                           }, cancellationToken: cts.Token))
            {
                results.Add(item);
            }
        }

        var task = Act();
        await Task.Delay(50, cts.Token);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        results.Count.Should().BeLessThan(100);
    }

    [Fact]
    public async Task ForEachParallelAsync_Cancellation_ThrowsOperationCanceledException()
    {
        var source = Enumerable.Range(1, 100).ToAsyncEnumerable();
        using var cts = new CancellationTokenSource();
        var processedCount = 0;

        async Task Act() =>
            await source.ForEachParallelAsync(async (_, ct) =>
            {
                Interlocked.Increment(ref processedCount);
                await Task.Delay(100, ct);
            }, cancellationToken: cts.Token);

        var task = Act();
        await Task.Delay(50, cts.Token);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        processedCount.Should().BeLessThan(100);
    }

    [Fact]
    public async Task PerItemTimeout_ItemExceedsTimeout_ThrowsException()
    {
        var source = Enumerable.Range(1, 5);
        var options = new ParallelOptionsRivulet
        {
            // Use longer timeout to prevent false positives on slow CI/CD machines
            // where even quick operations might exceed timeout due to thread pool starvation
            PerItemTimeout = TimeSpan.FromMilliseconds(2000),
            ErrorMode = ErrorMode.BestEffort,
            MaxDegreeOfParallelism = 2 // Limit concurrency to reduce thread pool contention
        };

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                if (x == 3)
                    // Use much longer delay (10s) to ensure this item reliably times out
                    // Even on slow CI/CD, 10s >> 2s timeout, so no race condition
                    await Task.Delay(10000, ct);
                else
                    // Use minimal delay to ensure these items complete quickly
                    // Even with thread pool delays, 1ms << 2s timeout
                    await Task.Delay(1, ct);
                return x * 2;
            },
            options);

        results.Should().HaveCount(4, "items 1,2,4,5 should complete, item 3 should timeout");
        results.Should().NotContain(6, "item 3 (value 6) should have timed out");
    }

    [Fact]
    public async Task PerItemTimeout_AllItemsWithinTimeout_Succeeds()
    {
        var source = Enumerable.Range(1, 10);
        var options = new ParallelOptionsRivulet
        {
            // Tasks take 50ms, but CI runners can have significant scheduling delays
            // Use 2000ms timeout (40x the expected duration) to avoid flakiness in heavily loaded CI
            PerItemTimeout = TimeSpan.FromMilliseconds(2000)
        };

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(50, ct);
                return x * 2;
            },
            options);

        results.Should().HaveCount(10);
    }

    [Fact]
    public async Task PerItemTimeout_NullTimeout_NoTimeoutEnforced()
    {
        var source = Enumerable.Range(1, 3);
        var options = new ParallelOptionsRivulet
        {
            PerItemTimeout = null
        };

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(200, ct);
                return x * 2;
            },
            options);

        results.Should().HaveCount(3);
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
                // Increased from 500ms to 1000ms (10x the 100ms timeout) to ensure reliable timeouts
                // This prevents rare race conditions where delays complete before timeout fires
                await Task.Delay(1000, ct);
                return x * 2;
            },
            options);

        results.Should().BeEmpty();
        attemptCount.Should().Be(3);
    }

    [Fact]
    public async Task PerItemTimeout_SelectParallelStreamAsync_HandlesTimeout()
    {
        var source = Enumerable.Range(1, 5).ToAsyncEnumerable();
        var options = new ParallelOptionsRivulet
        {
            PerItemTimeout = TimeSpan.FromMilliseconds(200),
            ErrorMode = ErrorMode.BestEffort
        };

        var results = await source.SelectParallelStreamAsync(async (x, ct) =>
            {
                if (x == 3)
                    await Task.Delay(1000, ct);
                else
                    await Task.Delay(5, ct);
                return x * 2;
            }, options)
            .ToListAsync();

        results.Should().HaveCount(4);
    }

    [Fact]
    public async Task Cancellation_DuringItemProcessing_PropagatesCancellation()
    {
        var source = Enumerable.Range(1, 200);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        async Task<List<int>> Act() =>
            await source.SelectParallelAsync(async (x, ct) =>
            {
                await Task.Delay(20, ct);
                ct.ThrowIfCancellationRequested();
                return x * 2;
            }, cancellationToken: cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>((Func<Task<List<int>>>)Act);
    }

    [Fact]
    public async Task Cancellation_BeforeStart_ThrowsImmediately()
    {
        var source = Enumerable.Range(1, 100);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        async Task<List<int>> Act() => await source.SelectParallelAsync((x, _) => new ValueTask<int>(x * 2), cancellationToken: cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>((Func<Task<List<int>>>)Act);
    }

    [Fact]
    public async Task CancellationToken_PassedToTaskSelector()
    {
        var source = Enumerable.Range(1, 5);
        var tokensPassed = new ConcurrentBag<bool>();

        var results = await source.SelectParallelAsync(
            (x, ct) =>
            {
                tokensPassed.Add(ct != CancellationToken.None);
                return new ValueTask<int>(x * 2);
            });

        results.Should().HaveCount(5);
        tokensPassed.Should().OnlyContain(token => token);
    }

    [Fact]
    public async Task PerItemTimeout_WithFailFast_ThrowsOnTimeout()
    {
        var source = Enumerable.Range(1, 10);
        var options = new ParallelOptionsRivulet
        {
            PerItemTimeout = TimeSpan.FromMilliseconds(50),
            ErrorMode = ErrorMode.FailFast,
            MaxDegreeOfParallelism = 1
        };

        async Task<List<int>> Act() =>
            await source.SelectParallelAsync(async (x, ct) =>
            {
                if (x == 5)
                    await Task.Delay(500, ct);
                else
                    await Task.Delay(10, ct);
                return x * 2;
            }, options);

        await Assert.ThrowsAnyAsync<OperationCanceledException>((Func<Task<List<int>>>)Act);
    }

    [Fact]
    public async Task PerItemTimeout_WithCollectAndContinue_CollectsTimeoutErrors()
    {
        var source = Enumerable.Range(1, 10);
        var options = new ParallelOptionsRivulet
        {
            PerItemTimeout = TimeSpan.FromMilliseconds(50),
            ErrorMode = ErrorMode.CollectAndContinue,
            MaxDegreeOfParallelism = 1
        };

        async Task<List<int>> Act() =>
            await source.SelectParallelAsync(async (x, ct) =>
            {
                if (x is 3 or 7)
                    await Task.Delay(300, ct);
                else
                    await Task.Delay(5, ct);
                return x * 2;
            }, options);

        var exception = await Assert.ThrowsAsync<AggregateException>(((Func<Task<List<int>>>?)Act)!);
        exception.InnerExceptions.Count.Should().BeGreaterThanOrEqualTo(1);
        exception.InnerExceptions.Should().AllBeAssignableTo<OperationCanceledException>();
    }

    [Fact]
    public async Task CombinedCancellationAndTimeout_BothWork()
    {
        var source = Enumerable.Range(1, 100);
        using var cts = new CancellationTokenSource();
        var options = new ParallelOptionsRivulet
        {
            // Use very short timeout to ensure timeouts happen quickly
            PerItemTimeout = TimeSpan.FromMilliseconds(20),
            ErrorMode = ErrorMode.BestEffort,
            MaxDegreeOfParallelism = 2 // Limit concurrency to make timing more predictable
        };

        var task = source.SelectParallelAsync(
            async (x, ct) =>
            {
                // Use much longer delay to ensure items reliably timeout
                // 1000ms delay vs 20ms timeout = guaranteed timeout
                await Task.Delay(1000, ct);
                return x * 2;
            },
            options,
            cts.Token);

        // Wait very briefly, then cancel immediately
        // This ensures only a few items (if any) can start processing before cancellation
        await Task.Delay(10, CancellationToken.None);
        await cts.CancelAsync();

        var results = await task;

        // Due to timing variations, 0-2 items might complete before timeout/cancellation
        // The key assertion is that NOT all 100 items completed (timeout and cancellation work)
        results.Should().HaveCountLessThanOrEqualTo(2, "timeout and cancellation should prevent most items from completing");
    }

    [Fact]
    public async Task Cancellation_WriterTask_StopsProducingItems()
    {
        var source = Enumerable.Range(1, 1000);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var processedCount = 0;

        async Task<List<int>> Act() =>
            await source.SelectParallelAsync(async (x, ct) =>
            {
                Interlocked.Increment(ref processedCount);
                await Task.Delay(10, ct);
                return x * 2;
            }, cancellationToken: cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>((Func<Task<List<int>>>)Act);
        processedCount.Should().BeLessThan(1000);
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
        consumedCount.Should().BeGreaterThanOrEqualTo(10);
        consumedCount.Should().BeLessThan(1000);
    }
}
