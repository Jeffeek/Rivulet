using Polly;
using Polly.Timeout;
using Rivulet.Core;
using Rivulet.Core.Resilience;
using PollyCircuitBreaker = Polly.CircuitBreaker;

namespace Rivulet.Polly;

/// <summary>
/// Converts Rivulet resilience options to Polly resilience pipelines.
/// </summary>
public static class RivuletToPollyConverter
{
    /// <summary>
    /// Converts ParallelOptionsRivulet retry configuration to a Polly retry pipeline.
    /// </summary>
    /// <remarks>
    /// The returned pipeline uses ThreadLocal storage for DecorrelatedJitter state,
    /// making it safe to reuse across concurrent operations.
    /// </remarks>
    public static ResiliencePipeline ToPollyRetryPipeline(this ParallelOptionsRivulet options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaxRetries == 0)
            return ResiliencePipeline.Empty;

#pragma warning disable CA2000 // ThreadLocal is intentionally not disposed - its lifetime is tied to the pipeline
        var previousDelayLocal = new ThreadLocal<TimeSpan>(() => TimeSpan.Zero);
#pragma warning restore CA2000

        return new ResiliencePipelineBuilder()
            .AddRetry(new()
            {
                MaxRetryAttempts = options.MaxRetries,
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(ex => options.IsTransient?.Invoke(ex) ?? false),
                DelayGenerator = args =>
                {
                    var prev = previousDelayLocal.Value;
                    var delay = BackoffCalculator.CalculateDelay(
                        options.BackoffStrategy,
                        options.BaseDelay,
                        args.AttemptNumber + 1,
                        ref prev);
                    previousDelayLocal.Value = prev;
                    return new(delay);
                },
                OnRetry = args =>
                {
                    if (args.AttemptNumber >= options.MaxRetries)
                        previousDelayLocal.Value = TimeSpan.Zero;
                    return default;
                }
            })
            .Build();
    }

    /// <summary>
    /// Converts Rivulet timeout configuration to a Polly timeout pipeline.
    /// </summary>
    /// <param name="timeout">The timeout duration.</param>
    /// <returns>A Polly resilience pipeline with timeout strategy.</returns>
    /// <example>
    /// <code>
    /// var timeoutPipeline = TimeSpan.FromSeconds(5).ToPollyTimeoutPipeline();
    /// var result = await timeoutPipeline.ExecuteAsync(async ct => await LongRunningOperation(ct));
    /// </code>
    /// </example>
    public static ResiliencePipeline ToPollyTimeoutPipeline(this TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive");

        return new ResiliencePipelineBuilder()
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = timeout
            })
            .Build();
    }

    /// <summary>
    /// Converts Rivulet circuit breaker options to a Polly circuit breaker pipeline.
    /// </summary>
    public static ResiliencePipeline ToPollyCircuitBreakerPipeline(this CircuitBreakerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        return new ResiliencePipelineBuilder()
            .AddCircuitBreaker(CreateCircuitBreakerOptions(options))
            .Build();
    }

    /// <summary>
    /// Converts ParallelOptionsRivulet to a complete Polly pipeline with all configured resilience strategies.
    /// </summary>
    /// <remarks>
    /// Creates a pipeline combining Timeout, Circuit Breaker, and Retry (in that order, innermost to outermost).
    /// Uses ThreadLocal storage for DecorrelatedJitter state, making it thread-safe.
    /// </remarks>
    public static ResiliencePipeline ToPollyPipeline(this ParallelOptionsRivulet options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var builder = new ResiliencePipelineBuilder();
        var hasAnyStrategy = false;

        if (options.PerItemTimeout.HasValue)
        {
            builder.AddTimeout(new TimeoutStrategyOptions { Timeout = options.PerItemTimeout.Value });
            hasAnyStrategy = true;
        }

        if (options.CircuitBreaker is not null)
        {
            options.CircuitBreaker.Validate();
            builder.AddCircuitBreaker(CreateCircuitBreakerOptions(options.CircuitBreaker));
            hasAnyStrategy = true;
        }

        if (options.MaxRetries > 0)
        {
#pragma warning disable CA2000 // ThreadLocal is intentionally not disposed - its lifetime is tied to the pipeline
            var previousDelayLocal = new ThreadLocal<TimeSpan>(() => TimeSpan.Zero);
#pragma warning restore CA2000

            builder.AddRetry(new()
            {
                MaxRetryAttempts = options.MaxRetries,
                ShouldHandle = new PredicateBuilder().Handle<Exception>(ex => options.IsTransient?.Invoke(ex) ?? false),
                DelayGenerator = args =>
                {
                    var prev = previousDelayLocal.Value;
                    var delay = BackoffCalculator.CalculateDelay(
                        options.BackoffStrategy,
                        options.BaseDelay,
                        args.AttemptNumber + 1,
                        ref prev);
                    previousDelayLocal.Value = prev;
                    return new(delay);
                },
                OnRetry = args =>
                {
                    if (args.AttemptNumber >= options.MaxRetries)
                        previousDelayLocal.Value = TimeSpan.Zero;
                    return default;
                }
            });
            hasAnyStrategy = true;
        }

        return hasAnyStrategy ? builder.Build() : ResiliencePipeline.Empty;
    }

    private static PollyCircuitBreaker.CircuitBreakerStrategyOptions CreateCircuitBreakerOptions(CircuitBreakerOptions options) =>
        new()
        {
            FailureRatio = 1.0,
            MinimumThroughput = options.FailureThreshold,
            BreakDuration = options.OpenTimeout,
            ShouldHandle = new PredicateBuilder().Handle<Exception>(),
            OnOpened = _ =>
            {
                var unused = options.OnStateChange?.Invoke(CircuitBreakerState.Closed, CircuitBreakerState.Open);
                return ValueTask.CompletedTask;
            },
            OnClosed = _ =>
            {
                var unused = options.OnStateChange?.Invoke(CircuitBreakerState.HalfOpen, CircuitBreakerState.Closed);
                return ValueTask.CompletedTask;
            },
            OnHalfOpened = _ =>
            {
                var unused = options.OnStateChange?.Invoke(CircuitBreakerState.Open, CircuitBreakerState.HalfOpen);
                return ValueTask.CompletedTask;
            }
        };
}
