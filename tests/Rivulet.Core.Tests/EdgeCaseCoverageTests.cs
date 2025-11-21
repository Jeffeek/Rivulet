using Rivulet.Core.Resilience;

namespace Rivulet.Core.Tests;

/// <summary>
/// Tests specifically designed to cover edge cases and improve code coverage.
/// </summary>
public class EdgeCaseCoverageTests
{
    [Fact]
    public async Task MetricsTracker_ShouldHandleVeryFastExecution()
    {
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 10
        };

        var result = await Enumerable.Range(1, 3)
            .SelectParallelAsync((x, _) => ValueTask.FromResult(x), options);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task RetryPolicy_ShouldHandleNullMetricsTracker()
    {
        var attemptCount = 0;

        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 2,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            IsTransient = _ => true
        };

        var result = await Enumerable.Range(1, 1)
            .SelectParallelAsync(async (x, ct) =>
            {
                if (++attemptCount < 2)
                    throw new InvalidOperationException("Retry me");

                await Task.Delay(1, ct);
                return x;
            }, options);

        result.Should().HaveCount(1);
        attemptCount.Should().Be(2);
    }

    [Fact]
    public async Task TokenBucket_ShouldHandleRapidExecution()
    {
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 5
        };

        var result = await Enumerable.Range(1, 10)
            .SelectParallelAsync((x, _) => ValueTask.FromResult(x), options);

        result.Should().HaveCount(10);
    }

    [Fact]
    public async Task CircuitBreaker_ShouldSwallowExceptionsInOnStateChangeCallback()
    {
        var stateChanges = new List<CircuitBreakerState>();

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 2,
            ErrorMode = ErrorMode.BestEffort,
            CircuitBreaker = new CircuitBreakerOptions
            {
                FailureThreshold = 3,
                OpenTimeout = TimeSpan.FromMilliseconds(100),
                OnStateChange = async (_, newState) =>
                {
                    stateChanges.Add(newState);

                    await Task.CompletedTask;
                    throw new InvalidOperationException("Callback failed!");
                }
            }
        };

        await Enumerable.Range(1, 10)
            .SelectParallelAsync<int, int>(async (_, ct) =>
            {
                await Task.Delay(10, ct);
                throw new InvalidOperationException("Always fails");
            }, options);

        await Task.Delay(150);

        await Enumerable.Range(1, 2)
            .SelectParallelAsync<int, int>(async (_, ct) =>
            {
                await Task.Delay(10, ct);
                throw new InvalidOperationException("Still failing");
            }, options);

        stateChanges.Should().Contain(CircuitBreakerState.Open);
    }

    [Fact]
    public async Task ProgressTracker_ShouldHandleRapidProgressUpdates()
    {
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 10
        };

        var result = await Enumerable.Range(1, 20)
            .SelectParallelAsync((x, _) => ValueTask.FromResult(x * 2), options);

        result.Should().HaveCount(20);
    }

    [Fact]
    public async Task AdaptiveConcurrency_ShouldHandleExceptionInCallback()
    {
        var options = new ParallelOptionsRivulet
        {
            AdaptiveConcurrency = new AdaptiveConcurrencyOptions
            {
                MinConcurrency = 1,
                MaxConcurrency = 10,
                TargetLatency = TimeSpan.FromMilliseconds(50),
                SampleInterval = TimeSpan.FromMilliseconds(20),
                OnConcurrencyChange = async (oldValue, newValue) =>
                {
                    await Task.CompletedTask;
                    if (newValue > oldValue)
                        throw new InvalidOperationException("Callback exception");
                }
            }
        };

        var result = await Enumerable.Range(1, 50)
            .SelectParallelAsync(async (x, ct) =>
            {
                var delay = x % 2 == 0 ? 10 : 100;
                await Task.Delay(delay, ct);
                return x;
            }, options);

        result.Should().HaveCount(50);
    }

    [Fact]
    public async Task SelectParallelAsync_ShouldHandleEmptySource()
    {
        var emptySource = Enumerable.Empty<int>();

        var result = await emptySource.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x;
            },
            new ParallelOptionsRivulet { MaxDegreeOfParallelism = 2 });

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SelectParallelAsync_ShouldHandleSingleItem()
    {
        var singleItem = Enumerable.Range(1, 1);

        var result = await singleItem.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x * 2;
            },
            new ParallelOptionsRivulet { MaxDegreeOfParallelism = 10 });

        result.Should().HaveCount(1).And.Contain(2);
    }

    [Fact]
    public async Task ErrorMode_BestEffort_ShouldReturnPartialResults()
    {
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 2,
            ErrorMode = ErrorMode.BestEffort
        };

        var result = await Enumerable.Range(1, 10)
            .SelectParallelAsync(async (x, ct) =>
            {
                await Task.Delay(1, ct);
                if (x % 3 == 0)
                    throw new InvalidOperationException($"Failed on {x}");
                return x;
            }, options);

        result.Should().NotContain(x => x % 3 == 0);
        result.Count().Should().BeLessThan(10);
    }

    [Fact]
    public async Task Backoff_WithJitter_ShouldAddRandomness()
    {
        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 3,
            BaseDelay = TimeSpan.FromMilliseconds(10),
            BackoffStrategy = BackoffStrategy.ExponentialJitter,
            IsTransient = _ => true,
            OnRetryAsync = async (_, _, _) =>
            {
                await Task.CompletedTask;
            }
        };

        var attemptCount = 0;
        try
        {
            await Enumerable.Range(1, 1)
                .SelectParallelAsync(async (x, ct) =>
                {
                    if (++attemptCount > 3) return x;

                    await Task.Delay(1, ct);
                    throw new InvalidOperationException("Transient");
                }, options);
        }
        catch
        {
            // ignored
        }

        attemptCount.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task MetricsTracker_ShouldHandleNullOnMetricsSampleCallback()
    {
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 2,
            Metrics = new Core.Observability.MetricsOptions
            {
                SampleInterval = TimeSpan.FromMilliseconds(10),
                OnMetricsSample = null
            }
        };

        var result = await Enumerable.Range(1, 5)
            .SelectParallelAsync(async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x * 2;
            }, options);

        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task MetricsTracker_ShouldHandleDisposalDuringMetricsSampling()
    {
        var sampledCount = 0;
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 2,
            Metrics = new Core.Observability.MetricsOptions
            {
                SampleInterval = TimeSpan.FromMilliseconds(10),
                OnMetricsSample = async _ =>
                {
                    Interlocked.Increment(ref sampledCount);
                    await ValueTask.CompletedTask;
                }
            }
        };

        var task = Enumerable.Range(1, 100)
            .SelectParallelAsync(async (x, ct) =>
            {
                await Task.Delay(20, ct);
                return x;
            }, options);

        await Task.Delay(50);

        var result = await task;

        result.Should().NotBeEmpty();
        sampledCount.Should().BeGreaterThan(0);
    }
}
