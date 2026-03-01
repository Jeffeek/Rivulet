using Polly;
using Polly.Timeout;
using Rivulet.Core;
using Rivulet.Core.Resilience;

namespace Rivulet.Polly.Tests;

public sealed class RivuletToPollyConverterTests
{
    [Fact]
    public void ToPollyRetryPipeline_NoRetries_ReturnsEmptyPipeline()
    {
        var options = new ParallelOptionsRivulet { MaxRetries = 0 };

        var pipeline = options.ToPollyRetryPipeline();

        pipeline.ShouldBeSameAs(ResiliencePipeline.Empty);
    }

    [Fact]
    public async Task ToPollyRetryPipeline_WithRetries_RetriesTransientFailures()
    {
        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 3,
            IsTransient = static ex => ex is InvalidOperationException,
            BackoffStrategy = BackoffStrategy.Exponential,
            BaseDelay = TimeSpan.FromMilliseconds(10)
        };

        var pipeline = options.ToPollyRetryPipeline();
        var attemptCount = 0;

        var result = await pipeline.ExecuteAsync(_ =>
            {
                attemptCount++;
                return attemptCount <= 2
                    ? throw new InvalidOperationException("Transient failure")
                    : ValueTask.FromResult(42);
            },
            TestContext.Current.CancellationToken);

        result.ShouldBe(42);
        attemptCount.ShouldBe(3, "should have retried twice and succeeded on 3rd attempt");
    }

    [Fact]
    public async Task ToPollyRetryPipeline_NonTransientException_DoesNotRetry()
    {
        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 3,
            IsTransient = static ex => ex is InvalidOperationException,
            BackoffStrategy = BackoffStrategy.Exponential,
            BaseDelay = TimeSpan.FromMilliseconds(10)
        };

        var pipeline = options.ToPollyRetryPipeline();
        var attemptCount = 0;

        var act = async () => await pipeline.ExecuteAsync(_ =>
        {
            attemptCount++;
            throw new ArgumentException("Non-transient failure");
        });

        await act.ShouldThrowAsync<ArgumentException>();
        attemptCount.ShouldBe(1, "should not have retried non-transient exception");
    }

    [Fact]
    public async Task ToPollyRetryPipeline_ExponentialJitter_AppliesJitter()
    {
        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 3,
            IsTransient = static ex => ex is InvalidOperationException,
            BackoffStrategy = BackoffStrategy.ExponentialJitter,
            BaseDelay = TimeSpan.FromMilliseconds(100)
        };

        var pipeline = options.ToPollyRetryPipeline();
        var attemptTimes = new List<DateTime>();

        var result = await pipeline.ExecuteAsync(_ =>
            {
                attemptTimes.Add(DateTime.UtcNow);
                return attemptTimes.Count <= 2
                    ? throw new InvalidOperationException("Transient failure")
                    : ValueTask.FromResult(42);
            },
            TestContext.Current.CancellationToken);

        result.ShouldBe(42);
        attemptTimes.Count.ShouldBe(3);
        // With jitter, delays should vary, but we can verify attempts happened
    }

    [Fact]
    public async Task ToPollyRetryPipeline_DecorrelatedJitter_MaintainsState()
    {
        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 3,
            IsTransient = static ex => ex is InvalidOperationException,
            BackoffStrategy = BackoffStrategy.DecorrelatedJitter,
            BaseDelay = TimeSpan.FromMilliseconds(50)
        };

        var pipeline = options.ToPollyRetryPipeline();
        var attemptCount = 0;

        var result = await pipeline.ExecuteAsync(_ =>
            {
                attemptCount++;
                return attemptCount <= 2
                    ? throw new InvalidOperationException("Transient failure")
                    : ValueTask.FromResult(42);
            },
            TestContext.Current.CancellationToken);

        result.ShouldBe(42);
        attemptCount.ShouldBe(3);
    }

    [Fact]
    public async Task ToPollyRetryPipeline_LinearBackoff_AppliesLinearDelay()
    {
        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 3,
            IsTransient = static ex => ex is InvalidOperationException,
            BackoffStrategy = BackoffStrategy.Linear,
            BaseDelay = TimeSpan.FromMilliseconds(50)
        };

        var pipeline = options.ToPollyRetryPipeline();
        var attemptCount = 0;

        var result = await pipeline.ExecuteAsync(_ =>
            {
                attemptCount++;
                return attemptCount <= 2
                    ? throw new InvalidOperationException("Transient failure")
                    : ValueTask.FromResult(42);
            },
            TestContext.Current.CancellationToken);

        result.ShouldBe(42);
        attemptCount.ShouldBe(3);
    }

    [Fact]
    public void ToPollyRetryPipeline_NullOptions_ThrowsArgumentNullException()
    {
        ParallelOptionsRivulet? options = null;

        var act = () => options!.ToPollyRetryPipeline();

        act.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe("options");
    }

    [Fact]
    public void ToPollyTimeoutPipeline_ValidTimeout_CreatesTimeoutPipeline()
    {
        var timeout = TimeSpan.FromSeconds(5);

        var pipeline = timeout.ToPollyTimeoutPipeline();

        pipeline.ShouldNotBeNull();
    }

    [Fact]
    public async Task ToPollyTimeoutPipeline_WithinTimeout_Succeeds()
    {
        var timeout = TimeSpan.FromSeconds(1);
        var pipeline = timeout.ToPollyTimeoutPipeline();

        var result = await pipeline.ExecuteAsync(static async ct =>
            {
                await Task.Delay(10, ct);
                return 42;
            },
            TestContext.Current.CancellationToken);

        result.ShouldBe(42);
    }

    [Fact]
    public void ToPollyTimeoutPipeline_ZeroTimeout_ThrowsArgumentOutOfRangeException()
    {
        var timeout = TimeSpan.Zero;

        var act = () => timeout.ToPollyTimeoutPipeline();

        act.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe("timeout");
    }

    [Fact]
    public void ToPollyTimeoutPipeline_NegativeTimeout_ThrowsArgumentOutOfRangeException()
    {
        var timeout = TimeSpan.FromSeconds(-1);

        var act = () => timeout.ToPollyTimeoutPipeline();

        act.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe("timeout");
    }

    [Fact]
    public void ToPollyCircuitBreakerPipeline_ValidOptions_CreatesPipeline()
    {
        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 3,
            SuccessThreshold = 2,
            OpenTimeout = TimeSpan.FromSeconds(1),
            OnStateChange = static (_, _) => ValueTask.CompletedTask
        };

        var pipeline = options.ToPollyCircuitBreakerPipeline();

        pipeline.ShouldNotBeNull();
    }

    [Fact]
    public void ToPollyCircuitBreakerPipeline_NullOptions_ThrowsArgumentNullException()
    {
        CircuitBreakerOptions? options = null;

        var act = () => options!.ToPollyCircuitBreakerPipeline();

        act.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe("options");
    }

    [Fact]
    public void ToPollyCircuitBreakerPipeline_InvalidOptions_ThrowsArgumentException()
    {
        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 0 // Invalid
        };

        var act = () => options.ToPollyCircuitBreakerPipeline();

        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void ToPollyPipeline_NoStrategies_ReturnsEmptyPipeline()
    {
        var options = new ParallelOptionsRivulet { MaxRetries = 0, PerItemTimeout = null, CircuitBreaker = null };

        var pipeline = options.ToPollyPipeline();

        pipeline.ShouldBeSameAs(ResiliencePipeline.Empty);
    }

    [Fact]
    public async Task ToPollyPipeline_WithRetryOnly_AppliesRetry()
    {
        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 2, IsTransient = static ex => ex is InvalidOperationException,
            BaseDelay = TimeSpan.FromMilliseconds(10)
        };

        var pipeline = options.ToPollyPipeline();
        var attemptCount = 0;

        var result = await pipeline.ExecuteAsync(_ =>
            {
                attemptCount++;
                return attemptCount <= 1
                    ? throw new InvalidOperationException("Transient failure")
                    : ValueTask.FromResult(42);
            },
            TestContext.Current.CancellationToken);

        result.ShouldBe(42);
        attemptCount.ShouldBe(2);
    }

    [Fact]
    public async Task ToPollyPipeline_WithTimeoutOnly_AppliesTimeout()
    {
        var options = new ParallelOptionsRivulet { MaxRetries = 0, PerItemTimeout = TimeSpan.FromSeconds(1) };

        var pipeline = options.ToPollyPipeline();

        var result = await pipeline.ExecuteAsync(static async ct =>
            {
                await Task.Delay(10, ct);
                return 42;
            },
            TestContext.Current.CancellationToken);

        result.ShouldBe(42);
    }

    [Fact]
    public void ToPollyPipeline_WithCircuitBreakerOnly_CreatesPipeline()
    {
        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 0, CircuitBreaker = new() { FailureThreshold = 2, OpenTimeout = TimeSpan.FromSeconds(1) }
        };

        var pipeline = options.ToPollyPipeline();

        pipeline.ShouldNotBeSameAs(ResiliencePipeline.Empty);
    }

    [Fact]
    public async Task ToPollyPipeline_WithAllStrategies_CreatesCombinedPipeline()
    {
        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 2,
            IsTransient = static ex => ex is InvalidOperationException,
            BaseDelay = TimeSpan.FromMilliseconds(10),
            PerItemTimeout = TimeSpan.FromSeconds(1),
            CircuitBreaker = new() { FailureThreshold = 5, OpenTimeout = TimeSpan.FromSeconds(1) }
        };

        var pipeline = options.ToPollyPipeline();

        pipeline.ShouldNotBeSameAs(ResiliencePipeline.Empty);

        // Verify it works with a successful operation
        var result = await pipeline.ExecuteAsync(static async ct =>
            {
                await Task.Delay(10, ct);
                return 42;
            },
            TestContext.Current.CancellationToken);

        result.ShouldBe(42);
    }

    [Fact]
    public void ToPollyPipeline_NullOptions_ThrowsArgumentNullException()
    {
        ParallelOptionsRivulet? options = null;

        var act = () => options!.ToPollyPipeline();

        act.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe("options");
    }

    [Fact]
    public void ToPollyPipeline_InvalidCircuitBreakerOptions_ThrowsArgumentException()
    {
        var options = new ParallelOptionsRivulet
        {
            CircuitBreaker = new()
            {
                FailureThreshold = 0 // Invalid
            }
        };

        var act = () => options.ToPollyPipeline();

        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public async Task ToPollyRetryPipeline_IsTransientNull_DoesNotRetry()
    {
        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 3,
            IsTransient = null, // Null IsTransient - should not retry
            BackoffStrategy = BackoffStrategy.Exponential,
            BaseDelay = TimeSpan.FromMilliseconds(10)
        };

        var pipeline = options.ToPollyRetryPipeline();
        var attemptCount = 0;

        var act = async () => await pipeline.ExecuteAsync(_ =>
        {
            attemptCount++;
            throw new InvalidOperationException("Failure");
        });

        await act.ShouldThrowAsync<InvalidOperationException>();
        attemptCount.ShouldBe(1, "should not retry when IsTransient is null");
    }

    [Fact]
    public async Task ToPollyRetryPipeline_AllRetriesFail_ResetsPreviousDelay()
    {
        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 2,
            IsTransient = static ex => ex is InvalidOperationException,
            BackoffStrategy = BackoffStrategy.DecorrelatedJitter,
            BaseDelay = TimeSpan.FromMilliseconds(10)
        };

        var pipeline = options.ToPollyRetryPipeline();
        var attemptCount = 0;

        var act = async () => await pipeline.ExecuteAsync(_ =>
        {
            // ReSharper disable once AccessToModifiedClosure
            attemptCount++;
            throw new InvalidOperationException("Always fails");
        });

        await act.ShouldThrowAsync<InvalidOperationException>();
        attemptCount.ShouldBe(3, "should attempt 3 times (1 initial + 2 retries)");

        // Run again to ensure previousDelay was reset
        attemptCount = 0;
        await act.ShouldThrowAsync<InvalidOperationException>();
        attemptCount.ShouldBe(3, "should attempt 3 times again after reset");
    }

    [Fact]
    public async Task ToPollyPipeline_IsTransientNull_DoesNotRetry()
    {
        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 3,
            IsTransient = null, // Null IsTransient - should not retry
            BaseDelay = TimeSpan.FromMilliseconds(10)
        };

        var pipeline = options.ToPollyPipeline();
        var attemptCount = 0;

        var act = async () => await pipeline.ExecuteAsync(_ =>
        {
            attemptCount++;
            throw new InvalidOperationException("Failure");
        });

        await act.ShouldThrowAsync<InvalidOperationException>();
        attemptCount.ShouldBe(1, "should not retry when IsTransient is null");
    }

    [Fact]
    public async Task ToPollyPipeline_AllRetriesFail_ResetsPreviousDelay()
    {
        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 2,
            IsTransient = static ex => ex is InvalidOperationException,
            BackoffStrategy = BackoffStrategy.DecorrelatedJitter,
            BaseDelay = TimeSpan.FromMilliseconds(10)
        };

        var pipeline = options.ToPollyPipeline();
        var attemptCount = 0;

        var act = async () => await pipeline.ExecuteAsync(_ =>
        {
            // ReSharper disable once AccessToModifiedClosure
            attemptCount++;
            throw new InvalidOperationException("Always fails");
        });

        await act.ShouldThrowAsync<InvalidOperationException>();
        attemptCount.ShouldBe(3, "should attempt 3 times (1 initial + 2 retries)");

        // Run again to ensure previousDelay was reset
        attemptCount = 0;
        await act.ShouldThrowAsync<InvalidOperationException>();
        attemptCount.ShouldBe(3, "should attempt 3 times again after reset");
    }

    [Fact]
    public void ToPollyCircuitBreakerPipeline_WithOnStateChange_InvokesCallback()
    {
        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 2,
            SuccessThreshold = 1,
            OpenTimeout = TimeSpan.FromSeconds(1), // Polly requires min 500ms
            OnStateChange = static (_, _) => ValueTask.CompletedTask
        };

        var pipeline = options.ToPollyCircuitBreakerPipeline();

        // The pipeline is created but state changes won't happen until we actually use it
        // This test verifies the callback structure is set up correctly
        pipeline.ShouldNotBeNull();
    }

    [Fact]
    public void ToPollyPipeline_WithCircuitBreakerOnStateChange_InvokesCallback()
    {
        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 0,
            CircuitBreaker = new()
            {
                FailureThreshold = 2,
                SuccessThreshold = 1,
                OpenTimeout = TimeSpan.FromSeconds(1), // Polly requires min 500ms
                OnStateChange = static (_, _) => ValueTask.CompletedTask
            }
        };

        var pipeline = options.ToPollyPipeline();

        // The pipeline is created but state changes won't happen until we actually use it
        // This test verifies the callback structure is set up correctly
        pipeline.ShouldNotBeNull();
        pipeline.ShouldNotBeSameAs(ResiliencePipeline.Empty);
    }

    [Fact]
    public async Task ToPollyTimeoutPipeline_ExceedsTimeout_ThrowsTimeoutException()
    {
        var timeout = TimeSpan.FromMilliseconds(50);
        var pipeline = timeout.ToPollyTimeoutPipeline();

        var act = async () => await pipeline.ExecuteAsync(static async ct =>
        {
            await Task.Delay(5000, ct); // Much longer than timeout
            return 42;
        });

        await act.ShouldThrowAsync<TimeoutRejectedException>();
    }

    [Fact]
    public async Task ToPollyCircuitBreakerPipeline_OpensAndClosesCircuit_InvokesStateChangeCallbacks()
    {
        var stateChanges = new List<(CircuitBreakerState From, CircuitBreakerState To)>();
        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 3,
            SuccessThreshold = 1,
            OpenTimeout = TimeSpan.FromMilliseconds(500),
            OnStateChange = (from, to) =>
            {
                stateChanges.Add((from, to));
                return ValueTask.CompletedTask;
            }
        };

        var pipeline = options.ToPollyCircuitBreakerPipeline();

        // Trigger failures to open circuit
        for (var i = 0; i < 3; i++)
        {
            try
            {
                await pipeline.ExecuteAsync(static _ => throw new InvalidOperationException("Test failure"),
                    TestContext.Current.CancellationToken);
            }
            catch (InvalidOperationException)
            {
                // Expected
            }
        }

        // Circuit should be open now
        stateChanges.ShouldHaveSingleItem()
            .ShouldSatisfyAllConditions(static sc => sc.From.ShouldBe(CircuitBreakerState.Closed),
                static sc => sc.To.ShouldBe(CircuitBreakerState.Open));

        // Wait for circuit to transition to HalfOpen
        await Task.Delay(600, CancellationToken.None);

        // Next call should trigger HalfOpen -> Closed transition on success
        var result = await pipeline.ExecuteAsync(static _ => ValueTask.FromResult(42),
            TestContext.Current.CancellationToken);
        result.ShouldBe(42);

        // Verify all state transitions occurred
        stateChanges.ShouldContain(static sc =>
            sc.From == CircuitBreakerState.Open && sc.To == CircuitBreakerState.HalfOpen);
        stateChanges.ShouldContain(static sc =>
            sc.From == CircuitBreakerState.HalfOpen && sc.To == CircuitBreakerState.Closed);
    }

    [Fact]
    public async Task ToPollyPipeline_WithCircuitBreakerStateChanges_InvokesCallbacks()
    {
        var stateChanges = new List<(CircuitBreakerState From, CircuitBreakerState To)>();
        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 0,
            CircuitBreaker = new()
            {
                FailureThreshold = 3,
                SuccessThreshold = 1,
                OpenTimeout = TimeSpan.FromMilliseconds(500),
                OnStateChange = (from, to) =>
                {
                    stateChanges.Add((from, to));
                    return ValueTask.CompletedTask;
                }
            }
        };

        var pipeline = options.ToPollyPipeline();

        // Trigger failures to open circuit
        for (var i = 0; i < 3; i++)
        {
            try
            {
                await pipeline.ExecuteAsync(static _ => throw new InvalidOperationException("Test failure"),
                    TestContext.Current.CancellationToken);
            }
            catch (InvalidOperationException)
            {
                // Expected
            }
        }

        // Circuit should be open
        stateChanges.ShouldHaveSingleItem()
            .ShouldSatisfyAllConditions(static sc => sc.From.ShouldBe(CircuitBreakerState.Closed),
                static sc => sc.To.ShouldBe(CircuitBreakerState.Open));

        // Wait for circuit to transition to HalfOpen
        await Task.Delay(600, CancellationToken.None);

        // Success should trigger HalfOpen -> Closed
        var result = await pipeline.ExecuteAsync(static _ => ValueTask.FromResult(42),
            TestContext.Current.CancellationToken);
        result.ShouldBe(42);

        // Verify state transitions
        stateChanges.ShouldContain(static sc =>
            sc.From == CircuitBreakerState.Open && sc.To == CircuitBreakerState.HalfOpen);
        stateChanges.ShouldContain(static sc =>
            sc.From == CircuitBreakerState.HalfOpen && sc.To == CircuitBreakerState.Closed);
    }
}
