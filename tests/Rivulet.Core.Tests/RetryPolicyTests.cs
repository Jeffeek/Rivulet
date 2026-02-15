using System.Collections.Concurrent;
using System.Diagnostics;
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
            options,
            cancellationToken: TestContext.Current.CancellationToken);

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
            options,
            cancellationToken: TestContext.Current.CancellationToken);

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
            options,
            cancellationToken: TestContext.Current.CancellationToken);

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
            options,
            cancellationToken: TestContext.Current.CancellationToken);

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
                options,
                cancellationToken: TestContext.Current.CancellationToken);

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
                    return x == 3 && attempts == 1
                        ? throw new InvalidOperationException("Transient error")
                        : new ValueTask<int>(x * 2);
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
            options,
            cancellationToken: TestContext.Current.CancellationToken);

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
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Count.ShouldBe(2);
        attemptCounts[1].ShouldBe(3);
        attemptCounts[2].ShouldBe(1);
    }

    [Fact]
    public async Task WithRetryPolicy_PerItemTimeout_EnforcesTimeout()
    {
        var source = new[] { 1, 2, 3 };
        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.BestEffort,
            PerItemTimeout = TimeSpan.FromMilliseconds(100),
            MaxRetries = 0
        };

        var results = await source.SelectParallelAsync(static async (x, ct) =>
            {
                if (x == 2)
                {
                    // This should timeout
                    await Task.Delay(TimeSpan.FromSeconds(10), ct);
                }

                return x * 2;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        // Item 2 should timeout and be excluded
        results.Count.ShouldBe(2);
        results.ShouldContain(2); // 1 * 2
        results.ShouldContain(6); // 3 * 2
    }

    [Fact]
    public async Task WithRetryPolicy_PerItemTimeout_WithSuccessfulCompletion()
    {
        var source = new[] { 1, 2, 3 };
        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.BestEffort,
            PerItemTimeout = TimeSpan.FromSeconds(5),
            MaxRetries = 0
        };

        var results = await source.SelectParallelAsync(static async (x, ct) =>
            {
                await Task.Delay(10, ct); // Short delay, well within timeout
                return x * 2;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        // All items should complete successfully
        results.Count.ShouldBe(3);
    }

    [Fact]
    public async Task WithRetryPolicy_OnRetryAsync_IsCalledOnRetry()
    {
        var source = new[] { 1 };
        var retryCallbacks = new List<(int itemIndex, int attemptNumber, Exception exception)>();
        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.BestEffort,
            MaxRetries = 3,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            IsTransient = static _ => true,
            OnRetryAsync = (itemIndex, attemptNumber, exception) =>
            {
                retryCallbacks.Add((itemIndex, attemptNumber, exception));
                return ValueTask.CompletedTask;
            }
        };

        var attemptCount = 0;
        var results = await source.SelectParallelAsync(
            (x, _) =>
            {
                attemptCount++;
                return attemptCount <= 2
                    ? throw new InvalidOperationException($"Attempt {attemptCount}")
                    : new ValueTask<int>(x * 2);
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Count.ShouldBe(1);
        retryCallbacks.Count.ShouldBe(2); // First retry (attempt 2) and second retry (attempt 3)
        retryCallbacks[0].itemIndex.ShouldBe(0);
        retryCallbacks[0].attemptNumber.ShouldBe(1);
        retryCallbacks[0].exception.Message.ShouldBe("Attempt 1");
        retryCallbacks[1].attemptNumber.ShouldBe(2);
        retryCallbacks[1].exception.Message.ShouldBe("Attempt 2");
    }

    [Fact]
    public async Task WithRetryPolicy_OnFallback_ReturnsCorrectType()
    {
        var source = new[] { 1, 2, 3 };
        var fallbackCalls = new ConcurrentBag<(int itemIndex, Exception exception)>();
        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.BestEffort,
            MaxRetries = 1,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            IsTransient = static _ => true,
            OnFallback = (itemIndex, exception) =>
            {
                fallbackCalls.Add((itemIndex, exception));
                return -1; // Fallback value
            }
        };

        var results = await source.SelectParallelAsync(static (x, _) => x == 2
                ? throw new InvalidOperationException("Always fails")
                : new ValueTask<int>(x * 2),
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Count.ShouldBe(3);
        results.ShouldContain(2);  // 1 * 2
        results.ShouldContain(-1); // Fallback value for 2
        results.ShouldContain(6);  // 3 * 2

        fallbackCalls.Count.ShouldBe(1);
        fallbackCalls.Single().exception.Message.ShouldBe("Always fails");
    }

    [Fact]
    public async Task WithRetryPolicy_OnFallback_ReturnsNullForReferenceType()
    {
        var source = new[] { 1, 2, 3 };
        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.BestEffort,
            MaxRetries = 1,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            IsTransient = static _ => true,
            OnFallback = static (_, _) => null // Null fallback value for reference type
        };

        var results = await source.SelectParallelAsync(static (x, _) => x == 2
                ? throw new InvalidOperationException("Always fails")
                : new ValueTask<string?>($"Item{x}"),
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Count.ShouldBe(3);
        results.ShouldContain("Item1");
        results.ShouldContain(null as string); // Null fallback value
        results.ShouldContain("Item3");
    }

    [Fact]
    public async Task WithRetryPolicy_OnFallback_WithWrongType_ThrowsInvalidOperationException()
    {
        var source = new[] { 1 };
        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.FailFast,
            MaxRetries = 1,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            IsTransient = static _ => true,
            OnFallback = static (_, _) => "WrongType" // Returns string instead of int
        };

        var exception = await Should.ThrowAsync<InvalidOperationException>(Act);
        exception.Message.ShouldContain("Fallback returned");
        exception.Message.ShouldContain("String");
        exception.Message.ShouldContain("Int32");
        return;

        Task<List<int>> Act() =>
            source.SelectParallelAsync<int, int>(
                static (_, _) => throw new InvalidOperationException("Always fails"),
                options,
                cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task WithRetryPolicy_OnFallback_WithNullForValueType_ThrowsInvalidOperationException()
    {
        var source = new[] { 1 };
        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.FailFast,
            MaxRetries = 1,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            IsTransient = static _ => true,
            OnFallback = static (_, _) => null // Returns null for value type (int)
        };

        var exception = await Should.ThrowAsync<InvalidOperationException>(Act);
        exception.Message.ShouldContain("Fallback returned");
        exception.Message.ShouldContain("null");
        exception.Message.ShouldContain("Int32");
        return;

        Task<List<int>> Act() =>
            source.SelectParallelAsync<int, int>(
                static (_, _) => throw new InvalidOperationException("Always fails"),
                options,
                cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task WithRetryPolicy_OnRetryAsync_WithAsyncWork()
    {
        var source = new[] { 1 };
        var retryDelayMs = new List<long>();
        var stopwatch = Stopwatch.StartNew();

        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.BestEffort,
            MaxRetries = 2,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            IsTransient = static _ => true,
            OnRetryAsync = async (_, _, _) =>
            {
                await Task.Delay(50);
                retryDelayMs.Add(stopwatch.ElapsedMilliseconds);
            }
        };

        var attemptCount = 0;
        _ = await source.SelectParallelAsync(
            (x, _) =>
            {
                attemptCount++;
                return attemptCount <= 2
                    ? throw new InvalidOperationException("Retry me")
                    : new ValueTask<int>(x * 2);
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        retryDelayMs.Count.ShouldBe(2);
        // Verify async work was awaited by checking elapsed time
        stopwatch.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(100); // 2 retries * 50ms each
    }

    [Fact]
    public async Task WithRetryPolicy_NoPerItemTimeout_UsesRegularPath()
    {
        var source = new[] { 1, 2, 3 };
        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.BestEffort,
            PerItemTimeout = null, // No timeout
            MaxRetries = 0
        };

        var results = await source.SelectParallelAsync(static async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x * 2;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        // All items should complete successfully
        results.Count.ShouldBe(3);
        results.ShouldContain(2);
        results.ShouldContain(4);
        results.ShouldContain(6);
    }
}
