using System.Runtime.CompilerServices;
using Polly;
using Polly.Timeout;
using Rivulet.Core;
using Rivulet.Core.Resilience;

namespace Rivulet.Polly;

/// <summary>
/// Converts Rivulet resilience options to Polly resilience pipelines.
/// </summary>
public static class RivuletToPollyConverter
{
    /// <summary>
    /// Converts ParallelOptionsRivulet retry configuration to a Polly retry pipeline.
    /// </summary>
    /// <param name="options">The Rivulet parallel options containing retry configuration.</param>
    /// <returns>A Polly resilience pipeline with retry strategy.</returns>
    /// <remarks>
    /// This converts Rivulet's retry configuration (MaxRetries, IsTransient, BackoffStrategy, etc.)
    /// into an equivalent Polly retry policy that can be used independently of Rivulet.
    /// </remarks>
    /// <example>
    /// <code>
    /// var rivuletOptions = new ParallelOptionsRivulet
    /// {
    ///     MaxRetries = 3,
    ///     IsTransient = ex => ex is HttpRequestException,
    ///     BackoffStrategy = BackoffStrategy.ExponentialJitter,
    ///     BaseDelay = TimeSpan.FromMilliseconds(100)
    /// };
    ///
    /// var pollyPipeline = rivuletOptions.ToPollyRetryPipeline();
    ///
    /// // Use with any operation
    /// var result = await pollyPipeline.ExecuteAsync(async ct => await CallApiAsync(ct));
    /// </code>
    /// </example>
    public static ResiliencePipeline ToPollyRetryPipeline(this ParallelOptionsRivulet options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaxRetries == 0)
        {
            // No retries configured, return empty pipeline
            return ResiliencePipeline.Empty;
        }

        // For DecorrelatedJitter, we need to maintain state across retries
        // Use a closure to capture previousDelay for each operation
        var previousDelayHolder = new StrongBox<TimeSpan>(TimeSpan.Zero);

        return new ResiliencePipelineBuilder()
            .AddRetry(new()
            {
                MaxRetryAttempts = options.MaxRetries,
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(ex => options.IsTransient?.Invoke(ex) ?? false),
                DelayGenerator = args =>
                {
                    var delay = BackoffCalculator.CalculateDelay(
                        options.BackoffStrategy,
                        options.BaseDelay,
                        args.AttemptNumber + 1, // Polly is 0-based, Rivulet is 1-based
                        ref previousDelayHolder.Value);
                    return new(delay);
                },
                OnRetry = args =>
                {
                    // Note: Rivulet's OnRetryAsync expects (itemIndex, attemptNumber, exception)
                    // but in standalone Polly context, we don't have itemIndex
                    // Users should handle this in their own callback if needed

                    // Reset previousDelay on final retry to prepare for next operation
                    if (args.AttemptNumber >= options.MaxRetries)
                    {
                        previousDelayHolder.Value = TimeSpan.Zero;
                    }
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
    /// <param name="options">The circuit breaker options.</param>
    /// <returns>A Polly resilience pipeline with circuit breaker strategy.</returns>
    /// <example>
    /// <code>
    /// var circuitBreakerPipeline = new CircuitBreakerOptions
    /// {
    ///     FailureThreshold = 5,
    ///     OpenTimeout = TimeSpan.FromSeconds(30),
    ///     SuccessThreshold = 2
    /// }.ToPollyCircuitBreakerPipeline();
    ///
    /// var result = await circuitBreakerPipeline.ExecuteAsync(async ct => await CallApiAsync(ct));
    /// </code>
    /// </example>
    public static ResiliencePipeline ToPollyCircuitBreakerPipeline(this CircuitBreakerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        return new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new()
            {
                FailureRatio = 1.0, // Open immediately after threshold
                MinimumThroughput = options.FailureThreshold,
                BreakDuration = options.OpenTimeout,
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                OnOpened = _ =>
                {
                    // Circuit opened: Closed -> Open
                    options.OnStateChange?.Invoke(CircuitBreakerState.Closed, CircuitBreakerState.Open);
                    return default;
                },
                OnClosed = _ =>
                {
                    // Circuit closed: HalfOpen -> Closed
                    options.OnStateChange?.Invoke(CircuitBreakerState.HalfOpen, CircuitBreakerState.Closed);
                    return default;
                },
                OnHalfOpened = _ =>
                {
                    // Circuit half-opened: Open -> HalfOpen
                    options.OnStateChange?.Invoke(CircuitBreakerState.Open, CircuitBreakerState.HalfOpen);
                    return default;
                }
            })
            .Build();
    }

    /// <summary>
    /// Converts ParallelOptionsRivulet to a complete Polly pipeline with all configured resilience strategies.
    /// </summary>
    /// <param name="options">The Rivulet parallel options.</param>
    /// <returns>A Polly resilience pipeline with all applicable strategies.</returns>
    /// <remarks>
    /// This creates a complete Polly pipeline that combines:
    /// - Timeout (if PerItemTimeout is set)
    /// - Circuit Breaker (if CircuitBreaker is configured)
    /// - Retry (if MaxRetries > 0)
    ///
    /// The order is important: Timeout -> CircuitBreaker -> Retry (innermost to outermost)
    /// </remarks>
    /// <example>
    /// <code>
    /// var rivuletOptions = new ParallelOptionsRivulet
    /// {
    ///     MaxRetries = 3,
    ///     IsTransient = ex => ex is HttpRequestException,
    ///     PerItemTimeout = TimeSpan.FromSeconds(5),
    ///     CircuitBreaker = new CircuitBreakerOptions { FailureThreshold = 5 }
    /// };
    ///
    /// var pollyPipeline = rivuletOptions.ToPollyPipeline();
    /// var result = await pollyPipeline.ExecuteAsync(async ct => await CallApiAsync(ct));
    /// </code>
    /// </example>
    public static ResiliencePipeline ToPollyPipeline(this ParallelOptionsRivulet options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var builder = new ResiliencePipelineBuilder();
        var hasAnyStrategy = false;

        // Add timeout first (innermost - closest to the operation)
        if (options.PerItemTimeout.HasValue)
        {
            builder.AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = options.PerItemTimeout.Value
            });
            hasAnyStrategy = true;
        }

        // Add circuit breaker
        if (options.CircuitBreaker != null)
        {
            options.CircuitBreaker.Validate();
            builder.AddCircuitBreaker(new()
            {
                FailureRatio = 1.0,
                MinimumThroughput = options.CircuitBreaker.FailureThreshold,
                BreakDuration = options.CircuitBreaker.OpenTimeout,
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                OnOpened = _ =>
                {
                    // Circuit opened: Closed -> Open
                    options.CircuitBreaker.OnStateChange?.Invoke(CircuitBreakerState.Closed, CircuitBreakerState.Open);
                    return default;
                },
                OnClosed = _ =>
                {
                    // Circuit closed: HalfOpen -> Closed
                    options.CircuitBreaker.OnStateChange?.Invoke(CircuitBreakerState.HalfOpen, CircuitBreakerState.Closed);
                    return default;
                },
                OnHalfOpened = _ =>
                {
                    // Circuit half-opened: Open -> HalfOpen
                    options.CircuitBreaker.OnStateChange?.Invoke(CircuitBreakerState.Open, CircuitBreakerState.HalfOpen);
                    return default;
                }
            });
            hasAnyStrategy = true;
        }

        // Add retry last (outermost - wraps everything)
        if (options.MaxRetries > 0)
        {
            // For DecorrelatedJitter, we need to maintain state across retries
            var previousDelayHolder = new StrongBox<TimeSpan>(TimeSpan.Zero);

            builder.AddRetry(new()
            {
                MaxRetryAttempts = options.MaxRetries,
                ShouldHandle = new PredicateBuilder().Handle<Exception>(ex => options.IsTransient?.Invoke(ex) ?? false),
                DelayGenerator = args =>
                {
                    var delay = BackoffCalculator.CalculateDelay(
                        options.BackoffStrategy,
                        options.BaseDelay,
                        args.AttemptNumber + 1,
                        ref previousDelayHolder.Value);
                    return new(delay);
                },
                OnRetry = args =>
                {
                    // Reset previousDelay after final retry to prepare for next operation
                    if (args.AttemptNumber >= options.MaxRetries)
                    {
                        previousDelayHolder.Value = TimeSpan.Zero;
                    }
                    return default;
                }
            });
            hasAnyStrategy = true;
        }

        return hasAnyStrategy ? builder.Build() : ResiliencePipeline.Empty;
    }
}
