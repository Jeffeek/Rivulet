using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Rivulet.Base.Tests;
using Rivulet.Core.Resilience;

namespace Rivulet.Core.Tests;

[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public sealed class CircuitBreakerTests
{
    [Fact]
    public void CircuitBreakerOptions_Validation_ThrowsOnInvalidFailureThreshold()
    {
        var act = static () => new CircuitBreakerOptions { FailureThreshold = 0 }.Validate();
        act.ShouldThrow<ArgumentException>().Message.ShouldContain("FailureThreshold");
    }

    [Fact]
    public void CircuitBreakerOptions_Validation_ThrowsOnInvalidSuccessThreshold()
    {
        var act = static () => new CircuitBreakerOptions { SuccessThreshold = 0 }.Validate();
        act.ShouldThrow<ArgumentException>().Message.ShouldContain("SuccessThreshold");
    }

    [Fact]
    public void CircuitBreakerOptions_Validation_ThrowsOnInvalidOpenTimeout()
    {
        var act = static () => new CircuitBreakerOptions { OpenTimeout = TimeSpan.Zero }.Validate();
        act.ShouldThrow<ArgumentException>().Message.ShouldContain("OpenTimeout");
    }

    [Fact]
    public void CircuitBreakerOptions_Validation_ThrowsOnInvalidSamplingDuration()
    {
        var act = () => new CircuitBreakerOptions { SamplingDuration = TimeSpan.Zero }.Validate();
        act.ShouldThrow<ArgumentException>().Message.ShouldContain("SamplingDuration");
    }

    [Fact]
    public void CircuitBreaker_Constructor_ThrowsOnNullOptions()
    {
        var act = static () => new CircuitBreaker(null!);
        act.ShouldThrow<ArgumentNullException>().Message.ShouldContain("options");
    }

    [Fact]
    public void CircuitBreaker_Constructor_ValidatesOptions()
    {
        var act = static () => new CircuitBreaker(new() { FailureThreshold = 0 });
        act.ShouldThrow<ArgumentException>().Message.ShouldContain("FailureThreshold");
    }

    [Fact]
    public void CircuitBreaker_InitialState_IsClosed()
    {
        var cb = new CircuitBreaker(new());
        cb.State.ShouldBe(CircuitBreakerState.Closed);
    }

    [Fact]
    public async Task CircuitBreaker_Opens_AfterFailureThreshold()
    {
        // Circuit should open after 3 consecutive failures
        var cb = new CircuitBreaker(new() { FailureThreshold = 3, OpenTimeout = TimeSpan.FromSeconds(1) });

        var failureCount = 0;

        // Execute 3 failures
        for (var i = 0; i < 3; i++)
        {
            try
            {
                await cb.ExecuteAsync(static async () =>
                {
                    await Task.Delay(1);
                    throw new InvalidOperationException("Test failure");
#pragma warning disable CS0162 // Unreachable code detected
                    return 0;
#pragma warning restore CS0162 // Unreachable code detected
                });
            }
            catch (InvalidOperationException)
            {
                failureCount++;
            }
        }

        failureCount.ShouldBe(3);

        // Circuit should now be open
        cb.State.ShouldBe(CircuitBreakerState.Open);

        // Next call should fail fast without executing
        var act = async () => await cb.ExecuteAsync(static async () =>
        {
            await Task.Delay(1);
            return 1;
        });

        await act.ShouldThrowAsync<CircuitBreakerOpenException>();
    }

    [Fact]
    public async Task CircuitBreaker_TransitionsToHalfOpen_AfterTimeout()
    {
        var cb = new CircuitBreaker(new() { FailureThreshold = 2, OpenTimeout = TimeSpan.FromMilliseconds(100) });

        // Cause 2 failures to open circuit
        for (var i = 0; i < 2; i++)
        {
            try
            {
                await cb.ExecuteAsync(static async () =>
                {
                    await Task.Delay(1);
                    throw new InvalidOperationException("Test failure");
#pragma warning disable CS0162 // Unreachable code detected
                    return 0;
#pragma warning restore CS0162 // Unreachable code detected
                });
            }
            catch (InvalidOperationException)
            {
                /* Expected - test intentionally throws */
            }
        }

        cb.State.ShouldBe(CircuitBreakerState.Open);

        // Wait for timeout
        await Task.Delay(150);

        // Next execution attempt should transition to HalfOpen
        var result = await cb.ExecuteAsync(static async () =>
        {
            await Task.Delay(1);
            return 42;
        });

        result.ShouldBe(42);
        cb.State.ShouldBe(CircuitBreakerState.HalfOpen);
    }

    [Fact]
    public async Task CircuitBreaker_ClosesFromHalfOpen_AfterSuccessThreshold()
    {
        var cb = new CircuitBreaker(new() { FailureThreshold = 2, SuccessThreshold = 2, OpenTimeout = TimeSpan.FromMilliseconds(100) });

        for (var i = 0; i < 2; i++)
        {
            try
            {
                await cb.ExecuteAsync(static () =>
                    ValueTask.FromException<InvalidOperationException>(new InvalidOperationException("Test failure")));
            }
            catch (InvalidOperationException)
            {
                /* Expected - test intentionally throws */
            }
        }

        cb.State.ShouldBe(CircuitBreakerState.Open);

        // Wait and transition to HalfOpen
        await Task.Delay(150);

        // Execute 2 successful operations
        for (var i = 0; i < 2; i++)
        {
            var result = await cb.ExecuteAsync(async () =>
            {
                await Task.Delay(1);
                return i;
            });
            result.ShouldBe(i);
        }

        cb.State.ShouldBe(CircuitBreakerState.Closed);
    }

    [Fact]
    public async Task CircuitBreaker_ReopensFromHalfOpen_OnFailure()
    {
        var cb = new CircuitBreaker(new() { FailureThreshold = 2, SuccessThreshold = 2, OpenTimeout = TimeSpan.FromMilliseconds(100) });

        // Open the circuit
        for (var i = 0; i < 2; i++)
        {
            try
            {
                await cb.ExecuteAsync(static () =>
                    ValueTask.FromException<InvalidOperationException>(new InvalidOperationException("Test failure")));
            }
            catch (InvalidOperationException)
            {
                /* Expected - test intentionally throws */
            }
        }

        // Wait and transition to HalfOpen
        await Task.Delay(150);

        // Execute one successful operation
        await cb.ExecuteAsync(static async () =>
        {
            await Task.Delay(1);
            return 1;
        });

        cb.State.ShouldBe(CircuitBreakerState.HalfOpen);

        // Execute one failed operation - should reopen circuit
        try
        {
            await cb.ExecuteAsync(() => ValueTask.FromException<InvalidOperationException>(new InvalidOperationException("Test failure")));
        }
        catch (InvalidOperationException)
        {
            // Expected - test intentionally throws to trigger circuit breaker
        }

        cb.State.ShouldBe(CircuitBreakerState.Open);
    }

    [Fact]
    public async Task CircuitBreaker_WithSamplingDuration_TracksFailuresInWindow()
    {
        var cb = new CircuitBreaker(new()
        {
            FailureThreshold = 3, SamplingDuration = TimeSpan.FromMilliseconds(200), OpenTimeout = TimeSpan.FromSeconds(1)
        });

        // Execute 2 failures
        for (var i = 0; i < 2; i++)
        {
            try
            {
                await cb.ExecuteAsync(() =>
                    ValueTask.FromException<InvalidOperationException>(new InvalidOperationException("Test failure")));
            }
            catch (InvalidOperationException)
            {
                /* Expected - test intentionally throws */
            }
        }

        cb.State.ShouldBe(CircuitBreakerState.Closed);

        // Wait for sampling window to expire
        await Task.Delay(250);

        // Old failures should be outside window, circuit should remain closed
        // Execute 2 more failures (total 2 in current window)
        for (var i = 0; i < 2; i++)
        {
            try
            {
                await cb.ExecuteAsync(() =>
                    ValueTask.FromException<InvalidOperationException>(new InvalidOperationException("Test failure")));
            }
            catch (InvalidOperationException)
            {
                /* Expected - test intentionally throws */
            }
        }

        cb.State.ShouldBe(CircuitBreakerState.Closed);

        // One more failure (3 in window) should open circuit
        try
        {
            await cb.ExecuteAsync(() => ValueTask.FromException<InvalidOperationException>(new InvalidOperationException("Test failure")));
        }
        catch (InvalidOperationException)
        {
            // Expected - test intentionally throws to trigger circuit breaker
        }

        cb.State.ShouldBe(CircuitBreakerState.Open);
    }

    [Fact]
    public async Task CircuitBreaker_OnStateChange_InvokesCallback()
    {
        var stateChanges = new List<(CircuitBreakerState from, CircuitBreakerState to)>();
        var allTransitionsComplete = new TaskCompletionSource<bool>();

        var cb = new CircuitBreaker(new()
        {
            FailureThreshold = 2,
            SuccessThreshold = 1,
            OpenTimeout = TimeSpan.FromMilliseconds(100),
            OnStateChange = (from, to) =>
            {
                stateChanges.Add((from, to));
                // Signal when we have all expected transitions
                if (stateChanges.Count >= 3) allTransitionsComplete.TrySetResult(true);

                return ValueTask.CompletedTask;
            }
        });

        // Open the circuit
        for (var i = 0; i < 2; i++)
        {
            try
            {
                await cb.ExecuteAsync(static () =>
                    ValueTask.FromException<InvalidOperationException>(new InvalidOperationException("Test failure")));
            }
            catch (InvalidOperationException)
            {
                /* Expected - test intentionally throws */
            }
        }

        // Wait long enough for HalfOpen timeout (100ms + buffer)
        await Task.Delay(200);

        // Execute successful operation - this triggers HalfOpen transition and then closes circuit
        await cb.ExecuteAsync(static async () =>
        {
            await Task.Delay(1);
            return 1;
        });

        // Wait for all transitions to complete with timeout
        // Callbacks are executed via Task.Run in fire-and-forget mode, which can be delayed under load
        // Increased from 500ms → 2000ms for Windows CI/CD reliability
        var completedTask = await Task.WhenAny(allTransitionsComplete.Task, Task.Delay(2000));
        (completedTask == allTransitionsComplete.Task).ShouldBeTrue("all transitions should complete within 2000ms");

        // Verify state transitions
        stateChanges.ShouldContain((CircuitBreakerState.Closed, CircuitBreakerState.Open));
        stateChanges.ShouldContain((CircuitBreakerState.Open, CircuitBreakerState.HalfOpen));
        stateChanges.ShouldContain((CircuitBreakerState.HalfOpen, CircuitBreakerState.Closed));
    }

    [Fact]
    public async Task CircuitBreaker_Reset_ClosesCircuit()
    {
        var cb = new CircuitBreaker(new() { FailureThreshold = 2, OpenTimeout = TimeSpan.FromSeconds(10) });

        // Open the circuit
        for (var i = 0; i < 2; i++)
        {
            try
            {
                await cb.ExecuteAsync(() =>
                    ValueTask.FromException<InvalidOperationException>(new InvalidOperationException("Test failure")));
            }
            catch (InvalidOperationException)
            {
                /* Expected - test intentionally throws */
            }
        }

        cb.State.ShouldBe(CircuitBreakerState.Open);

        // Reset circuit
        cb.Reset();

        cb.State.ShouldBe(CircuitBreakerState.Closed);

        // Should execute normally
        var result = await cb.ExecuteAsync(static async () =>
        {
            await Task.Delay(1);
            return 42;
        });

        result.ShouldBe(42);
    }

    [Fact]
    public async Task CircuitBreaker_WithSelectParallelAsync_OpensOnFailures()
    {
        var source = Enumerable.Range(1, 10);
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 1, // Sequential to ensure predictable failure order
            CircuitBreaker = new() { FailureThreshold = 3, OpenTimeout = TimeSpan.FromSeconds(1) },
            ErrorMode = ErrorMode.BestEffort
        };

        var results = await source.SelectParallelAsync(
            static async (x, ct) =>
            {
                await Task.Delay(1, ct);
                // Fail for items 1, 2, 3
                if (x <= 3) throw new InvalidOperationException($"Item {x} failed");

                return x * 2;
            },
            options);

        // After 3 failures, circuit opens and remaining items fail fast
        // Should get results only for items that were attempted before circuit opened
        // or succeeded before failure threshold
        results.Count.ShouldBeLessThan(10);
    }

    [Fact]
    public async Task CircuitBreaker_WithStreamingOperations_HandlesStateTransitions()
    {
        var source = AsyncEnumerable.Range(1, 20);
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 1,
            CircuitBreaker = new() { FailureThreshold = 5, SuccessThreshold = 2, OpenTimeout = TimeSpan.FromMilliseconds(100) },
            ErrorMode = ErrorMode.BestEffort
        };

        var results = await source.SelectParallelStreamAsync(static async (x, ct) =>
                {
                    await Task.Delay(1, ct);

                    // Fail for items 1-3, then succeed
                    if (x <= 3) throw new InvalidOperationException($"Item {x} failed");

                    return x * 2;
                },
                options)
            .ToListAsync();

        // Should have successful results (items 4-20)
        results.ShouldNotBeEmpty();
        results.Count.ShouldBeGreaterThan(10);
    }

    [Fact]
    public async Task CircuitBreaker_WithRetries_OpensAfterExhaustedRetries()
    {
        var source = Enumerable.Range(1, 5);
        var attemptCounts = new ConcurrentDictionary<int, int>();

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 1,
            MaxRetries = 2,
            IsTransient = static ex => ex is InvalidOperationException,
            CircuitBreaker = new()
            {
                FailureThreshold = 2, // Open after 2 items fully fail (after retries)
                OpenTimeout = TimeSpan.FromSeconds(1)
            },
            ErrorMode = ErrorMode.BestEffort
        };

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                attemptCounts.AddOrUpdate(x, 1, (_, count) => count + 1);
                await Task.Delay(1, ct);

                // Always fail for items 1 and 2
                if (x <= 2) throw new InvalidOperationException($"Item {x} failed");

                return x * 2;
            },
            options);

        // Items 1 and 2 should be retried
        attemptCounts[1].ShouldBe(3); // Initial + 2 retries
        attemptCounts[2].ShouldBe(3); // Initial + 2 retries

        // After items 1 and 2 fail (after retries), circuit should open
        results.Count.ShouldBeLessThan(5);
    }

    [Fact]
    public async Task CircuitBreaker_WithCancellation_CancelsCorrectly()
    {
        var source = Enumerable.Range(1, 100);
        using var cts = new CancellationTokenSource();

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 1, CircuitBreaker = new() { FailureThreshold = 50, OpenTimeout = TimeSpan.FromSeconds(1) }
        };

        var processedCount = 0;

        var task = source.SelectParallelAsync(
            async (x, ct) =>
            {
                Interlocked.Increment(ref processedCount);
                if (processedCount >= 5) await cts.CancelAsync();

                await Task.Delay(1, ct);
                return x * 2;
            },
            options,
            cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);

        processedCount.ShouldBeLessThan(100);
    }

    [Fact]
    public async Task CircuitBreaker_WithOrderedOutput_MaintainsOrder()
    {
        var source = Enumerable.Range(1, 10);
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            OrderedOutput = true,
            CircuitBreaker = new()
            {
                FailureThreshold = 100, // High threshold to not interfere
                OpenTimeout = TimeSpan.FromSeconds(1)
            }
        };

        var results = await source.SelectParallelAsync(
            static async (x, ct) =>
            {
                await Task.Delay(Random.Shared.Next(1, 10), ct);
                return x * 2;
            },
            options);

        results.Count.ShouldBe(10);
        results.ShouldBeInOrder();
        results.ShouldBe(Enumerable.Range(1, 10).Select(static x => x * 2));
    }

    [Fact]
    public void CircuitBreakerOpenException_HasCorrectState()
    {
        var ex = new CircuitBreakerOpenException();
        ex.State.ShouldBe(CircuitBreakerState.Open);
        ex.Message.ShouldContain("open");
    }

    [Fact]
    public void CircuitBreakerOpenException_WithCustomMessage()
    {
        var ex = new CircuitBreakerOpenException("Custom message");
        ex.Message.ShouldBe("Custom message");
        ex.State.ShouldBe(CircuitBreakerState.Open);
    }

    [Fact]
    public async Task CircuitBreaker_OnStateChangeCallback_ThrowsException_DoesNotCrash()
    {
        var callbackInvoked = 0;
        var cb = new CircuitBreaker(new()
        {
            FailureThreshold = 2,
            SuccessThreshold = 1,
            OpenTimeout = TimeSpan.FromMilliseconds(100),
            OnStateChange = (_, _) =>
            {
                Interlocked.Increment(ref callbackInvoked);
                throw new InvalidOperationException("Callback error!");
            }
        });

        for (var i = 0; i < 2; i++)
        {
            try
            {
                await cb.ExecuteAsync<int>(() => throw new InvalidOperationException("Test"), CancellationToken.None);
            }
            catch (InvalidOperationException)
            {
                /* Expected - test intentionally throws */
            }
        }

        cb.State.ShouldBe(CircuitBreakerState.Open);

        // OnStateChange callback is executed via Task.Run in fire-and-forget mode (CircuitBreaker.cs:176)
        // On slow CI/CD machines, the callback might not execute immediately
        // Poll for up to 2 seconds to ensure callback has been invoked
        await DeadlineExtensions.ApplyDeadlineAsync(
            DateTime.UtcNow.AddMilliseconds(2000),
            () => Task.Delay(50),
            () => callbackInvoked == 0);

        callbackInvoked.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task CircuitBreaker_OnStateChange_AllTransitions_InvokeCallback()
    {
        var transitions = new List<(CircuitBreakerState, CircuitBreakerState)>();
        var cb = new CircuitBreaker(new()
        {
            FailureThreshold = 2,
            SuccessThreshold = 1,
            OpenTimeout = TimeSpan.FromMilliseconds(50),
            OnStateChange = (oldState, newState) =>
            {
                lock (transitions) transitions.Add((oldState, newState));

                return ValueTask.CompletedTask;
            }
        });

        // Closed -> Open
        for (var i = 0; i < 2; i++)
        {
            try
            {
                await cb.ExecuteAsync<int>(() => throw new InvalidOperationException(), CancellationToken.None);
            }
            catch
            {
                // ignored
            }
        }

        await Task.Delay(100);
        cb.State.ShouldBe(CircuitBreakerState.Open);

        // Open -> HalfOpen (after timeout)
        await Task.Delay(100);
        cb.State.ShouldBe(CircuitBreakerState.Open);

        // Trigger HalfOpen by successful execution after timeout
        await Task.Delay(50);
        await cb.ExecuteAsync(() => ValueTask.FromResult(1), CancellationToken.None);

        // Poll for expected transitions to be captured (state changes may be delayed in CI)
        await DeadlineExtensions.ApplyDeadlineAsync(
            DateTime.UtcNow.AddMilliseconds(500),
            () => Task.Delay(20),
            () =>
            {
                lock (transitions)
                {
                    var hasOpen = transitions.Any(t => t.Item2 == CircuitBreakerState.Open);
                    var hasOther = transitions.Any(t => t.Item2 is CircuitBreakerState.HalfOpen or CircuitBreakerState.Closed);
                    return !hasOpen || !hasOther;
                }
            });

        lock (transitions)
        {
            transitions.ShouldContain(t => t.Item2 == CircuitBreakerState.Open);
            transitions.ShouldContain(t => t.Item2 == CircuitBreakerState.HalfOpen || t.Item2 == CircuitBreakerState.Closed);
        }
    }
}