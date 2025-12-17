using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Rivulet.Core.Tests;

[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public sealed class RetryPolicyTests
{
    [Fact]
    public async Task NoRetryPolicy_ErrorsAreNotRetried()
    {
        var source = Enumerable.Range(1, 5);
        var attemptCounts = new ConcurrentDictionary<int, int>();
        var options = new ParallelOptionsRivulet
            { ErrorMode = ErrorMode.BestEffort, MaxRetries = 0, IsTransient = static _ => true };

        var results = await source.SelectParallelAsync(
            (x, _) =>
            {
                attemptCounts.AddOrUpdate(x, 1, static (_, count) => count + 1);
                return x == 3 ? throw new InvalidOperationException("Transient error") : new ValueTask<int>(x * 2);
            },
            options);

        results.Count.ShouldBe(4);
        attemptCounts[3].ShouldBe(1);
    }

    [Fact]
    public async Task WithRetryPolicy_TransientErrorsAreRetried()
    {
        var source = Enumerable.Range(1, 5);
        var attemptCounts = new ConcurrentDictionary<int, int>();
        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.BestEffort,
            MaxRetries = 3,
            BaseDelay = TimeSpan.FromMilliseconds(10),
            IsTransient = static ex => ex is InvalidOperationException
        };

        var results = await source.SelectParallelAsync(
            (x, _) =>
            {
                var attempts = attemptCounts.AddOrUpdate(x, 1, static (_, count) => count + 1);
                return x == 3 && attempts <= 2
                    ? throw new InvalidOperationException("Transient error")
                    : new ValueTask<int>(x * 2);
            },
            options);

        results.Count.ShouldBe(5);
        attemptCounts[3].ShouldBe(3);
    }

    [Fact]
    public async Task WithRetryPolicy_NonTransientErrorsAreNotRetried()
    {
        var source = Enumerable.Range(1, 5);
        var attemptCounts = new ConcurrentDictionary<int, int>();
        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.BestEffort,
            MaxRetries = 3,
            BaseDelay = TimeSpan.FromMilliseconds(10),
            IsTransient = static ex => ex is TimeoutException
        };

        var results = await source.SelectParallelAsync(
            (x, _) =>
            {
                attemptCounts.AddOrUpdate(x, 1, static (_, count) => count + 1);
                return x == 3 ? throw new InvalidOperationException("Non-transient error") : new ValueTask<int>(x * 2);
            },
            options);

        results.Count.ShouldBe(4);
        attemptCounts[3].ShouldBe(1);
    }

    [Fact]
    public async Task WithRetryPolicy_ExponentialBackoff_IsApplied()
    {
        var source = new[] { 1 };
        var attemptTimestamps = new List<DateTime>();
        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.BestEffort, MaxRetries = 3, BaseDelay = TimeSpan.FromMilliseconds(100),
            IsTransient = static _ => true
        };

        _ = await source.SelectParallelAsync(
            (x, _) =>
            {
                attemptTimestamps.Add(DateTime.UtcNow);
                return attemptTimestamps.Count < 4
                    ? throw new InvalidOperationException("Transient error")
                    : new ValueTask<int>(x * 2);
            },
            options);

        attemptTimestamps.Count.ShouldBe(4);

        var delay1 = (attemptTimestamps[1] - attemptTimestamps[0]).TotalMilliseconds;
        var delay2 = (attemptTimestamps[2] - attemptTimestamps[1]).TotalMilliseconds;
        var delay3 = (attemptTimestamps[3] - attemptTimestamps[2]).TotalMilliseconds;

        delay1.ShouldBeGreaterThanOrEqualTo(90);
        delay2.ShouldBeGreaterThanOrEqualTo(180);
        delay3.ShouldBeGreaterThanOrEqualTo(360);
    }

    [Fact]
    public async Task WithRetryPolicy_MaxRetriesExceeded_ErrorPropagates()
    {
        var source = new[] { 1 };
        var attemptCount = 0;
        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.FailFast, MaxRetries = 2, BaseDelay = TimeSpan.FromMilliseconds(10),
            IsTransient = static _ => true
        };

        Task<List<int>> Act() =>
            source.SelectParallelAsync((x, _) =>
                {
                    attemptCount++;
                    throw new InvalidOperationException("Always fails");
#pragma warning disable CS0162 // Unreachable code detected
                    return new ValueTask<int>(x);
#pragma warning restore CS0162 // Unreachable code detected
                },
                options);

        await Assert.ThrowsAsync<InvalidOperationException>(((Func<Task<List<int>>>?)Act)!);
        attemptCount.ShouldBe(3);
    }

    [Fact]
    public async Task WithRetryPolicy_SelectParallelStreamAsync_RetriesTransientErrors()
    {
        var source = Enumerable.Range(1, 5).ToAsyncEnumerable();
        var attemptCounts = new ConcurrentDictionary<int, int>();
        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.BestEffort,
            MaxRetries = 2,
            BaseDelay = TimeSpan.FromMilliseconds(10),
            IsTransient = static ex => ex is InvalidOperationException
        };

        var results = await source.SelectParallelStreamAsync((x, _) =>
                {
                    var attempts = attemptCounts.AddOrUpdate(x, 1, static (_, count) => count + 1);
                    if (x == 3 && attempts == 1) throw new InvalidOperationException("Transient error");

                    return new ValueTask<int>(x * 2);
                },
                options)
            .ToListAsync();

        results.Count.ShouldBe(5);
        attemptCounts[3].ShouldBe(2);
    }

    [Fact]
    public async Task WithRetryPolicy_ForEachParallelAsync_RetriesTransientErrors()
    {
        var source = Enumerable.Range(1, 5).ToAsyncEnumerable();
        var attemptCounts = new ConcurrentDictionary<int, int>();
        var processedItems = new ConcurrentBag<int>();
        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.BestEffort, MaxRetries = 2, BaseDelay = TimeSpan.FromMilliseconds(10),
            IsTransient = static _ => true
        };

        await source.ForEachParallelAsync(
            (x, _) =>
            {
                var attempts = attemptCounts.AddOrUpdate(x, 1, static (_, count) => count + 1);
                if (x == 3 && attempts == 1) throw new InvalidOperationException("Transient error");

                processedItems.Add(x);
                return ValueTask.CompletedTask;
            },
            options);

        processedItems.Count.ShouldBe(5);
        attemptCounts[3].ShouldBe(2);
    }

    [Fact]
    public async Task WithRetryPolicy_NullIsTransient_NoRetries()
    {
        var source = new[] { 1 };
        var attemptCount = 0;
        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.BestEffort, MaxRetries = 3, BaseDelay = TimeSpan.FromMilliseconds(10),
            IsTransient = null
        };

        var results = await source.SelectParallelAsync(
            (x, _) =>
            {
                attemptCount++;
                return attemptCount == 1 ? throw new InvalidOperationException("Error") : new ValueTask<int>(x * 2);
            },
            options);

        results.ShouldBeEmpty();
        attemptCount.ShouldBe(1);
    }

    [Fact]
    public async Task WithRetryPolicy_CancellationDuringRetry_ThrowsOperationCanceledException()
    {
        var source = new[] { 1 };
        using var cts = new CancellationTokenSource();
        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.FailFast, MaxRetries = 5, BaseDelay = TimeSpan.FromMilliseconds(500),
            IsTransient = static _ => true
        };

        Task<List<int>> Act() =>
            source.SelectParallelAsync(static (x, _) =>
                {
                    throw new InvalidOperationException("Transient error");
#pragma warning disable CS0162
                    return new ValueTask<int>(x);
#pragma warning restore CS0162
                },
                options,
                cts.Token);

        var task = Act();
        await Task.Delay(100, cts.Token);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task WithRetryPolicy_DifferentExceptionTypes_OnlyTransientAreRetried()
    {
        var source = Enumerable.Range(1, 4);
        var attemptCounts = new ConcurrentDictionary<int, int>();
        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.BestEffort,
            MaxRetries = 2,
            BaseDelay = TimeSpan.FromMilliseconds(10),
            IsTransient = static ex => ex is TimeoutException
        };

        var results = await source.SelectParallelAsync(
            (x, _) =>
            {
                attemptCounts.AddOrUpdate(x, 1, static (_, count) => count + 1);

                return x switch
                {
                    1 => throw new TimeoutException("Transient"),
                    2 => throw new InvalidOperationException("Non-transient"),
                    _ => new ValueTask<int>(x * 2)
                };
            },
            options);

        results.Count.ShouldBe(2);
        attemptCounts[1].ShouldBe(3);
        attemptCounts[2].ShouldBe(1);
    }
}