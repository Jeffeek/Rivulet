using System.Diagnostics.CodeAnalysis;
using Rivulet.Core.Observability;
using Rivulet.Core.Resilience;

namespace Rivulet.Core.Tests;

/// <summary>
///     Regression tests for high-severity bug fixes.
/// </summary>
[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public sealed class BugFixRegressionTests
{
    #region Fix #1: Copy constructor preserves null sub-options

    [Fact]
    public void ParallelOptionsRivulet_CopyConstructor_NullSubOptions_RemainNull()
    {
        var original = new ParallelOptionsRivulet();

        // All sub-options should be null by default
        original.Progress.ShouldBeNull();
        original.Metrics.ShouldBeNull();
        original.RateLimit.ShouldBeNull();
        original.CircuitBreaker.ShouldBeNull();
        original.AdaptiveConcurrency.ShouldBeNull();

        // Copy should preserve nulls
        var copy = new ParallelOptionsRivulet(original);

        copy.Progress.ShouldBeNull();
        copy.Metrics.ShouldBeNull();
        copy.RateLimit.ShouldBeNull();
        copy.CircuitBreaker.ShouldBeNull();
        copy.AdaptiveConcurrency.ShouldBeNull();
    }

    [Fact]
    public void ParallelOptionsRivulet_CopyConstructor_NonNullSubOptions_AreCopied()
    {
        var original = new ParallelOptionsRivulet
        {
            Progress = new ProgressOptions { ReportInterval = TimeSpan.FromSeconds(3) },
            Metrics = new MetricsOptions { SampleInterval = TimeSpan.FromSeconds(7) },
            RateLimit = new RateLimitOptions { TokensPerSecond = 50 },
            CircuitBreaker = new CircuitBreakerOptions { FailureThreshold = 10 },
            AdaptiveConcurrency = new AdaptiveConcurrencyOptions { MinConcurrency = 2 }
        };

        var copy = new ParallelOptionsRivulet(original);

        copy.Progress.ShouldNotBeNull();
        copy.Progress!.ReportInterval.ShouldBe(TimeSpan.FromSeconds(3));

        copy.Metrics.ShouldNotBeNull();
        copy.Metrics!.SampleInterval.ShouldBe(TimeSpan.FromSeconds(7));

        copy.RateLimit.ShouldNotBeNull();
        copy.RateLimit!.TokensPerSecond.ShouldBe(50);

        copy.CircuitBreaker.ShouldNotBeNull();
        copy.CircuitBreaker!.FailureThreshold.ShouldBe(10);

        copy.AdaptiveConcurrency.ShouldNotBeNull();
        copy.AdaptiveConcurrency!.MinConcurrency.ShouldBe(2);
    }

    [Fact]
    public async Task ParallelOptionsRivulet_CopyConstructor_NullRateLimit_DoesNotThrottle()
    {
        // If the copy constructor incorrectly creates a non-null RateLimitOptions from null,
        // a TokenBucket would be created and throttle operations.
        var original = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            RateLimit = null // Explicitly null — no rate limiting
        };

        var copy = new ParallelOptionsRivulet(original);
        copy.RateLimit.ShouldBeNull();

        // Process many items quickly — should not be rate-limited
        var source = Enumerable.Range(1, 200);
        var results = await source.SelectParallelAsync(
            static (x, _) => new ValueTask<int>(x * 2),
            copy,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Count.ShouldBe(200);
    }

    [Fact]
    public async Task ParallelOptionsRivulet_CopyConstructor_NullCircuitBreaker_DoesNotActivate()
    {
        var original = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 1,
            ErrorMode = ErrorMode.BestEffort,
            CircuitBreaker = null
        };

        var copy = new ParallelOptionsRivulet(original);
        copy.CircuitBreaker.ShouldBeNull();

        // All items should fail without circuit breaker tripping
        var source = Enumerable.Range(1, 20);
        var results = await source.SelectParallelAsync(
            static (x, _) => x <= 10
                ? throw new InvalidOperationException("fail")
                : new ValueTask<int>(x * 2),
            copy,
            cancellationToken: TestContext.Current.CancellationToken);

        // Without a circuit breaker, all non-failing items should succeed
        results.Count.ShouldBe(10);
    }

    #endregion

    #region Fix #2: Fallback does not swallow OperationCanceledException

    [Fact]
    public async Task OnFallback_WithCancellation_PropagatesCancellation()
    {
        var source = new[] { 1, 2, 3 };
        using var cts = new CancellationTokenSource();
        var fallbackCalled = false;

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 1,
            ErrorMode = ErrorMode.FailFast,
            OnFallback = (_, _) =>
            {
                fallbackCalled = true;
                return -1;
            }
        };

        // Cancel immediately
        await cts.CancelAsync();

        var act = () => source.SelectParallelAsync(
            static async (x, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(10, ct);
                return x * 2;
            },
            options,
            cts.Token);

        await act.ShouldThrowAsync<OperationCanceledException>();
        fallbackCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task OnFallback_WithCancellationDuringProcessing_PropagatesCancellation()
    {
        var source = Enumerable.Range(1, 50);
        using var cts = new CancellationTokenSource();
        var fallbackCount = 0;

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 1,
            ErrorMode = ErrorMode.FailFast,
            OnFallback = (_, _) =>
            {
                Interlocked.Increment(ref fallbackCount);
                return -1;
            }
        };

        var processedCount = 0;

        var task = source.SelectParallelAsync(
            async (x, ct) =>
            {
                var count = Interlocked.Increment(ref processedCount);
                if (count == 5) await cts.CancelAsync();

                ct.ThrowIfCancellationRequested();
                await Task.Delay(10, ct);
                return x * 2;
            },
            options,
            cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);

        // Fallback should NOT have been called for cancellation
        fallbackCount.ShouldBe(0);
    }

    [Fact]
    public async Task OnFallback_WithNonCancellationError_StillInvokesFallback()
    {
        var source = new[] { 1, 2, 3 };
        var fallbackCalled = false;

        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.BestEffort,
            MaxRetries = 0,
            OnFallback = (_, _) =>
            {
                fallbackCalled = true;
                return -1;
            }
        };

        var results = await source.SelectParallelAsync(
            static (x, _) => x == 2
                ? throw new InvalidOperationException("real error")
                : new ValueTask<int>(x * 2),
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Count.ShouldBe(3);
        results.ShouldContain(-1);
        fallbackCalled.ShouldBeTrue();
    }

    #endregion

    #region Fix #3: AdaptiveConcurrencyController SemaphoreSlim overflow protection

    [Fact]
    public async Task AdaptiveConcurrencyController_ConcurrencyIncrease_WhenAllWorkersIdle_DoesNotThrow()
    {
        // Create controller with small initial concurrency
        await using var controller = new AdaptiveConcurrencyController(new AdaptiveConcurrencyOptions
        {
            MinConcurrency = 1,
            MaxConcurrency = 10,
            InitialConcurrency = 2,
            SampleInterval = TimeSpan.FromMilliseconds(50),
            MinSuccessRate = 0.5
        });

        // Acquire and immediately release to generate success samples
        // This ensures all semaphore slots are released (idle workers)
        for (var i = 0; i < 5; i++)
        {
            await controller.AcquireAsync(TestContext.Current.CancellationToken);
            controller.Release(TimeSpan.FromMilliseconds(1), true);
        }

        // Wait for sampling timer to fire and attempt to increase concurrency
        // The SemaphoreSlim.Release(delta) should not throw SemaphoreFullException
        await Task.Delay(200, CancellationToken.None);

        // Verify the controller is still functional
        var act = async () =>
        {
            await controller.AcquireAsync(TestContext.Current.CancellationToken);
            controller.Release(TimeSpan.FromMilliseconds(1), true);
        };

        await act.ShouldNotThrowAsync();
    }

    [Fact]
    public async Task AdaptiveConcurrency_RapidSuccesses_DoesNotCrashOnSemaphoreOverflow()
    {
        var source = Enumerable.Range(1, 100);
        var options = new ParallelOptionsRivulet
        {
            AdaptiveConcurrency = new AdaptiveConcurrencyOptions
            {
                MinConcurrency = 1,
                MaxConcurrency = 20,
                InitialConcurrency = 1,
                SampleInterval = TimeSpan.FromMilliseconds(30),
                MinSuccessRate = 0.1,
                IncreaseStrategy = AdaptiveConcurrencyStrategy.Aggressive
            }
        };

        // Should complete without SemaphoreFullException
        var results = await source.SelectParallelAsync(
            static (x, _) => new ValueTask<int>(x * 2),
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Count.ShouldBe(100);
    }

    #endregion

    #region Fix #4: CircuitBreaker HalfOpen limits concurrent probe requests

    [Fact]
    public async Task CircuitBreaker_HalfOpen_LimitsConcurrentProbeRequests()
    {
        const int successThreshold = 2;
        var cb = new CircuitBreaker(new CircuitBreakerOptions
        {
            FailureThreshold = 2,
            SuccessThreshold = successThreshold,
            OpenTimeout = TimeSpan.FromMilliseconds(100)
        });

        // Open the circuit
        for (var i = 0; i < 2; i++)
        {
            try
            {
                await cb.ExecuteAsync<int>(
                    static () => throw new InvalidOperationException("fail"),
                    CancellationToken.None);
            }
            catch (InvalidOperationException) { }
        }

        cb.State.ShouldBe(CircuitBreakerState.Open);

        // Wait for OpenTimeout to allow HalfOpen transition
        await Task.Delay(150, CancellationToken.None);

        // Now fire many concurrent requests — only SuccessThreshold should be allowed through
        var allowedCount = 0;
        var rejectedCount = 0;
        var tasks = Enumerable.Range(0, 20).Select(async _ =>
        {
            try
            {
                await cb.ExecuteAsync(static async () =>
                    {
                        await Task.Delay(50, CancellationToken.None); // Hold the slot
                        return 1;
                    },
                    CancellationToken.None);

                Interlocked.Increment(ref allowedCount);
            }
            catch (CircuitBreakerOpenException)
            {
                Interlocked.Increment(ref rejectedCount);
            }
        }).ToArray();

        await Task.WhenAll(tasks);

        // Only SuccessThreshold requests should have been allowed through
        allowedCount.ShouldBe(successThreshold);
        rejectedCount.ShouldBe(20 - successThreshold);
    }

    [Fact]
    public async Task CircuitBreaker_HalfOpen_PermitsRefreshedOnReopen()
    {
        var cb = new CircuitBreaker(new CircuitBreakerOptions
        {
            FailureThreshold = 1,
            SuccessThreshold = 2,
            OpenTimeout = TimeSpan.FromMilliseconds(50)
        });

        // Open the circuit
        try
        {
            await cb.ExecuteAsync<int>(
                static () => throw new InvalidOperationException("fail"),
                CancellationToken.None);
        }
        catch (InvalidOperationException) { }

        cb.State.ShouldBe(CircuitBreakerState.Open);

        // Wait for HalfOpen
        await Task.Delay(100, CancellationToken.None);

        // First probe — fail it to re-open
        try
        {
            await cb.ExecuteAsync<int>(
                static () => throw new InvalidOperationException("probe fail"),
                CancellationToken.None);
        }
        catch (InvalidOperationException) { }

        cb.State.ShouldBe(CircuitBreakerState.Open);

        // Wait for HalfOpen again
        await Task.Delay(100, CancellationToken.None);

        // Permits should be refreshed — should be able to probe again
        var result = await cb.ExecuteAsync(static () => ValueTask.FromResult(42), CancellationToken.None);
        result.ShouldBe(42);
    }

    [Fact]
    public async Task CircuitBreaker_HalfOpen_ExhaustedPermits_ThrowsWithHalfOpenState()
    {
        var cb = new CircuitBreaker(new CircuitBreakerOptions
        {
            FailureThreshold = 2,
            SuccessThreshold = 1,
            OpenTimeout = TimeSpan.FromMilliseconds(50)
        });

        // Open the circuit
        for (var i = 0; i < 2; i++)
        {
            try
            {
                await cb.ExecuteAsync<int>(
                    static () => throw new InvalidOperationException("fail"),
                    CancellationToken.None);
            }
            catch (InvalidOperationException) { }
        }

        // Wait for HalfOpen
        await Task.Delay(100, CancellationToken.None);

        // Use the one permit (SuccessThreshold = 1)
        // Hold it open with a long-running task so it doesn't close the circuit
        var holdPermit = cb.ExecuteAsync(static async () =>
            {
                await Task.Delay(500, CancellationToken.None);
                return 1;
            },
            CancellationToken.None);

        // Additional request while permit is held should be rejected with HalfOpen state
        var ex = await Should.ThrowAsync<CircuitBreakerOpenException>(async () =>
            await cb.ExecuteAsync(
                static () => ValueTask.FromResult(1),
                CancellationToken.None));

        ex.State.ShouldBe(CircuitBreakerState.HalfOpen);

        // Clean up the held task
        await holdPermit;
    }

    #endregion
}
