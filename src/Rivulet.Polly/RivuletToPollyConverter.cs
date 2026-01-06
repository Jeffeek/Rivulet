using System.Diagnostics.CodeAnalysis;
using Polly;
using Polly.Timeout;
using Rivulet.Core;
using Rivulet.Core.Resilience;
using Rivulet.Polly.Internal;
using PollyCircuitBreaker = Polly.CircuitBreaker;

namespace Rivulet.Polly;

/// <summary>
///     Converts Rivulet resilience options to Polly resilience pipelines.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
public static class RivuletToPollyConverter
{
    /// <summary>
    ///     Converts ParallelOptionsRivulet retry configuration to a Polly retry pipeline.
    /// </summary>
    /// <remarks>
    ///     The returned pipeline uses ThreadLocal storage for DecorrelatedJitter state,
    ///     making it safe to reuse across concurrent operations.
    ///     Note: ThreadLocal is not disposed as its lifetime is bound to the pipeline's closure.
    /// </remarks>
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP001:Dispose created", Justification = "ThreadLocal lifetime is bound to ResiliencePipeline closure - no accessible disposal point")]
    public static ResiliencePipeline ToPollyRetryPipeline(this ParallelOptionsRivulet options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaxRetries == 0) return ResiliencePipeline.Empty;

#pragma warning disable CA2000 // ThreadLocal is intentionally not disposed - its lifetime is tied to the pipeline
        var previousDelayLocal = new ThreadLocal<TimeSpan>(static () => TimeSpan.Zero);
#pragma warning restore CA2000

        return new ResiliencePipelineBuilder()
            .AddRetry(PollyHelper.CreateRetryOptions(options, previousDelayLocal))
            .Build();
    }

    /// <summary>
    ///     Converts Rivulet timeout configuration to a Polly timeout pipeline.
    /// </summary>
    /// <param name="timeout">The timeout duration.</param>
    /// <returns>A Polly resilience pipeline with timeout strategy.</returns>
    /// <example>
    ///     <code>
    /// var timeoutPipeline = TimeSpan.FromSeconds(5).ToPollyTimeoutPipeline();
    /// var result = await timeoutPipeline.ExecuteAsync(async ct => await LongRunningOperation(ct));
    /// </code>
    /// </example>
    public static ResiliencePipeline ToPollyTimeoutPipeline(this TimeSpan timeout) =>
        timeout <= TimeSpan.Zero
            ? throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive")
            : new ResiliencePipelineBuilder()
                .AddTimeout(new TimeoutStrategyOptions { Timeout = timeout })
                .Build();

    /// <summary>
    ///     Converts Rivulet circuit breaker options to a Polly circuit breaker pipeline.
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
    ///     Converts ParallelOptionsRivulet to a complete Polly pipeline with all configured resilience strategies.
    /// </summary>
    /// <remarks>
    ///     Creates a pipeline combining Timeout, Circuit Breaker, and Retry (in that order, innermost to outermost).
    ///     Uses ThreadLocal storage for DecorrelatedJitter state, making it thread-safe.
    ///     Note: ThreadLocal is not disposed as its lifetime is bound to the pipeline's closure.
    /// </remarks>
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP001:Dispose created", Justification = "ThreadLocal lifetime is bound to ResiliencePipeline closure - no accessible disposal point")]
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

        if (options.MaxRetries <= 0) return hasAnyStrategy ? builder.Build() : ResiliencePipeline.Empty;

#pragma warning disable CA2000 // ThreadLocal is intentionally not disposed - its lifetime is tied to the pipeline
        var previousDelayLocal = new ThreadLocal<TimeSpan>(static () => TimeSpan.Zero);
#pragma warning restore CA2000

        builder.AddRetry(PollyHelper.CreateRetryOptions(options, previousDelayLocal));
        hasAnyStrategy = true;

        return hasAnyStrategy ? builder.Build() : ResiliencePipeline.Empty;
    }

    private static PollyCircuitBreaker.CircuitBreakerStrategyOptions CreateCircuitBreakerOptions(
        CircuitBreakerOptions options
    ) => new()
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
