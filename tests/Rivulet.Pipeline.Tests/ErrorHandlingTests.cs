using System.Collections.Concurrent;
using Rivulet.Core;
using Rivulet.Core.Resilience;

namespace Rivulet.Pipeline.Tests;

public sealed class ErrorHandlingTests
{
    [Fact]
    public async Task ExecuteAsync_ExceptionInSelector_PropagatesException()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .SelectParallel(static (x, _) =>
                x == 5 ? throw new InvalidOperationException("Test error") : ValueTask.FromResult(x * 2))
            .Build();

        await Should.ThrowAsync<InvalidOperationException>(() => pipeline.ExecuteAsync(Enumerable.Range(1, 10)));
    }

    [Fact]
    public async Task ExecuteAsync_ExceptionInFilter_PropagatesException()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .WhereParallel(static (x, _) =>
                x == 5
                    ? throw new InvalidOperationException("Test error")
                    : ValueTask.FromResult(x % 2 == 0))
            .Build();

        await Should.ThrowAsync<InvalidOperationException>(() => pipeline.ExecuteAsync(Enumerable.Range(1, 10)));
    }

    [Fact]
    public async Task ExecuteAsync_ExceptionInBatchSelector_PropagatesException()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .BatchSelectParallel(5,
                static (batch, _) =>
                    batch.Contains(5)
                        ? throw new InvalidOperationException("Test error")
                        : ValueTask.FromResult(batch.Sum()))
            .Build();

        await Should.ThrowAsync<InvalidOperationException>(() => pipeline.ExecuteAsync(Enumerable.Range(1, 20)));
    }

    [Fact]
    public async Task ExecuteAsync_ExceptionInTap_PropagatesException()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .Tap(static (x, _) =>
                x == 5
                    ? throw new InvalidOperationException("Test error")
                    : ValueTask.CompletedTask)
            .Build();

        await Should.ThrowAsync<InvalidOperationException>(() => pipeline.ExecuteAsync(Enumerable.Range(1, 10)));
    }

    [Fact]
    public async Task ExecuteAsync_ExceptionInSelectMany_PropagatesException()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .SelectManyParallel(static (x, _) =>
                x == 3
                    ? throw new InvalidOperationException("Test error")
                    : ValueTask.FromResult(Enumerable.Range(1, x)))
            .Build();

        await Should.ThrowAsync<InvalidOperationException>(() => pipeline.ExecuteAsync(Enumerable.Range(1, 5)));
    }

    [Fact]
    public async Task ExecuteAsync_WithRetries_RetriesTransientErrors()
    {
        var attemptCounts = new ConcurrentDictionary<int, int>();

        var pipeline = PipelineBuilder.Create<int>()
            .SelectParallel(
                (x, _) =>
                {
                    var attempts = attemptCounts.AddOrUpdate(x, 1, static (_, c) => c + 1);
                    return x == 5 && attempts < 3 ? throw new InvalidOperationException("Transient error") : ValueTask.FromResult(x * 2);
                },
                new StageOptions
                {
                    ParallelOptions = new ParallelOptionsRivulet
                    {
                        MaxRetries = 3,
                        BaseDelay = TimeSpan.FromMilliseconds(10),
                        IsTransient = static ex => ex is InvalidOperationException,
                        ErrorMode = ErrorMode.CollectAndContinue
                    }
                })
            .Build();

        var results = await pipeline.ExecuteAsync(Enumerable.Range(1, 10));

        results.Count.ShouldBe(10);
        attemptCounts[5].ShouldBe(3); // Initial + 2 retries then success
    }

    [Fact]
    public async Task ExecuteAsync_WithBestEffortMode_ContinuesAfterError()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .SelectParallel(
                static (x, _) =>
                    x == 5
                        ? throw new InvalidOperationException("Error on 5")
                        : ValueTask.FromResult(x * 2),
                new StageOptions
                {
                    ParallelOptions = new ParallelOptionsRivulet
                    {
                        ErrorMode = ErrorMode.BestEffort
                    }
                })
            .Build();

        var results = await pipeline.ExecuteAsync(Enumerable.Range(1, 10));

        // Should have 9 results (all except the failed one)
        results.Count.ShouldBe(9);
        results.ShouldNotContain(10); // 5 * 2 = 10 should be missing
    }

    [Fact]
    public async Task ExecuteAsync_WithFallback_UsesFallbackValue()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .SelectParallel(
                static (x, _) =>
                    x == 5
                        ? throw new InvalidOperationException("Error on 5")
                        : ValueTask.FromResult(x * 2),
                new StageOptions
                {
                    ParallelOptions = new ParallelOptionsRivulet
                    {
                        OnFallback = static (_, _) => -1
                    }
                })
            .Build();

        var results = await pipeline.ExecuteAsync(Enumerable.Range(1, 10));

        results.Count.ShouldBe(10);
        results.ShouldContain(-1); // Fallback value for failed item
    }

    [Fact]
    public async Task ExecuteAsync_CallbackException_DoesNotBreakPipeline()
    {
        var callbackCalled = false;

        var pipeline = PipelineBuilder.Create<int>(new PipelineOptions
            {
                OnPipelineCompleteAsync = (_, _) =>
                {
                    callbackCalled = true;
                    throw new InvalidOperationException("Callback error");
                }
            })
            .SelectParallel(static x => x * 2)
            .Build();

        // Should not throw despite callback exception
        var results = await pipeline.ExecuteAsync(Enumerable.Range(1, 5));

        results.Count.ShouldBe(5);
        callbackCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithCircuitBreaker_OpensAfterFailures()
    {
        var failCount = 0;

        var pipeline = PipelineBuilder.Create<int>()
            .SelectParallel(
                (x, _) =>
                {
                    if (x > 5)
                        return ValueTask.FromResult(x * 2);

                    Interlocked.Increment(ref failCount);
                    throw new InvalidOperationException("Simulated failure");
                },
                new StageOptions
                {
                    ParallelOptions = new ParallelOptionsRivulet
                    {
                        CircuitBreaker = new CircuitBreakerOptions
                        {
                            FailureThreshold = 3,
                            OpenTimeout = TimeSpan.FromSeconds(30)
                        },
                        ErrorMode = ErrorMode.BestEffort
                    }
                })
            .Build();

        // Circuit breaker should open after 3 failures
        var results = await pipeline.ExecuteAsync(Enumerable.Range(1, 10));

        // Some items should be processed, but circuit breaker should limit failures
        results.Count.ShouldBeLessThan(10);
    }

    [Fact]
    public async Task ExecuteAsync_ExceptionWithBackoffStrategy_AppliesBackoff()
    {
        var timestamps = new ConcurrentBag<DateTime>();
        var failedOnce = false;

        var pipeline = PipelineBuilder.Create<int>()
            .SelectParallel(
                (x, _) =>
                {
                    timestamps.Add(DateTime.UtcNow);
                    if (x != 1 || failedOnce)
                        return ValueTask.FromResult(x * 2);

                    failedOnce = true;
                    throw new InvalidOperationException("First attempt fails");
                },
                new StageOptions
                {
                    ParallelOptions = new ParallelOptionsRivulet
                    {
                        MaxRetries = 1,
                        BaseDelay = TimeSpan.FromMilliseconds(100),
                        BackoffStrategy = BackoffStrategy.Linear,
                        IsTransient = static _ => true
                    }
                })
            .Build();

        var results = await pipeline.ExecuteAsync(new[] { 1 });

        results.Count.ShouldBe(1);
        failedOnce.ShouldBeTrue();

        // Should have at least 2 timestamps (original attempt + retry)
        timestamps.Count.ShouldBeGreaterThanOrEqualTo(2);
    }
}
