using Rivulet.Core.Observability;
using Rivulet.Core.Resilience;

namespace Rivulet.Core;

/// <summary>
/// Configuration options for controlling parallel async operations, including concurrency limits,
/// error handling modes, retry policies, timeouts, and lifecycle hooks.
/// </summary>
public sealed class ParallelOptionsRivulet
{
    /// <summary>
    /// Gets the maximum number of concurrent tasks to execute in parallel.
    /// Defaults to the number of processor cores.
    /// </summary>
    public int MaxDegreeOfParallelism { get; init; } = Math.Max(1, Environment.ProcessorCount);
    /// <summary>
    /// Gets the timeout duration for processing each individual item.
    /// If null, no per-item timeout is enforced. Defaults to null.
    /// </summary>
    public TimeSpan? PerItemTimeout { get; init; }
    /// <summary>
    /// Gets the error handling mode that determines how failures are managed during parallel processing.
    /// Defaults to <see cref="Core.ErrorMode.FailFast"/>.
    /// </summary>
    public ErrorMode ErrorMode { get; init; } = ErrorMode.FailFast;

    /// <summary>
    /// Gets a callback invoked when an error occurs during processing.
    /// The callback receives the item index and the exception.
    /// Return true to continue processing, false to cancel remaining work.
    /// In <see cref="Core.ErrorMode.CollectAndContinue"/> and <see cref="Core.ErrorMode.BestEffort"/> modes,
    /// this only affects flow control and does not prevent error collection.
    /// </summary>
    public Func<int, Exception, ValueTask<bool>>? OnErrorAsync { get; init; }
    /// <summary>
    /// Gets a callback invoked when processing of an item starts.
    /// Receives the item index.
    /// </summary>
    public Func<int, ValueTask>? OnStartItemAsync { get; init; }
    /// <summary>
    /// Gets a callback invoked when processing of an item completes successfully.
    /// Receives the item index.
    /// </summary>
    public Func<int, ValueTask>? OnCompleteItemAsync { get; init; }
    /// <summary>
    /// Gets a callback invoked when a retry attempt is made for a transient error.
    /// Receives the item index, the attempt number (1-based), and the exception that triggered the retry.
    /// This is called before the backoff delay.
    /// </summary>
    public Func<int, int, Exception, ValueTask>? OnRetryAsync { get; init; }
    /// <summary>
    /// Gets a callback invoked periodically when the processing pipeline is throttling due to backpressure.
    /// Receives the current item count.
    /// </summary>
    public Func<int, ValueTask>? OnThrottleAsync { get; init; }
    /// <summary>
    /// Gets a callback invoked when the processing pipeline is draining remaining items.
    /// Receives the current item count.
    /// </summary>
    public Func<int, ValueTask>? OnDrainAsync { get; init; }

    /// <summary>
    /// Gets a predicate to determine if an exception is transient and should be retried.
    /// Return true for transient errors, false for permanent failures.
    /// If null, no retries are performed. Defaults to null.
    /// </summary>
    public Func<Exception, bool>? IsTransient { get; init; }
    /// <summary>
    /// Gets the maximum number of retry attempts for transient failures.
    /// Defaults to 0 (no retries).
    /// </summary>
    public int MaxRetries { get; init; } = 0;
    /// <summary>
    /// Gets the base delay for backoff between retry attempts.
    /// The actual delay calculation depends on the <see cref="BackoffStrategy"/>.
    /// Defaults to 100 milliseconds.
    /// </summary>
    public TimeSpan BaseDelay { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets the backoff strategy to use when calculating retry delays.
    /// Defaults to <see cref="Resilience.BackoffStrategy.Exponential"/> for backward compatibility.
    /// </summary>
    /// <remarks>
    /// Different strategies provide different trade-offs:
    /// - <see cref="Resilience.BackoffStrategy.Exponential"/>: Predictable exponential growth without jitter (default).
    /// - <see cref="Resilience.BackoffStrategy.ExponentialJitter"/>: Recommended for rate-limited APIs to reduce thundering herd.
    /// - <see cref="Resilience.BackoffStrategy.DecorrelatedJitter"/>: Best for preventing synchronization across multiple clients.
    /// - <see cref="Resilience.BackoffStrategy.Linear"/>: Gentler, predictable linear growth.
    /// - <see cref="Resilience.BackoffStrategy.LinearJitter"/>: Linear growth with randomization.
    /// </remarks>
    public BackoffStrategy BackoffStrategy { get; init; } = BackoffStrategy.Exponential;

    /// <summary>
    /// Gets the channel capacity for buffering items in streaming operations.
    /// Controls backpressure by limiting how many items can be queued.
    /// Defaults to 1024.
    /// </summary>
    public int ChannelCapacity { get; init; } = 1024;

    /// <summary>
    /// Gets a value indicating whether results should be returned in the same order as the input sequence.
    /// When true, results are buffered and emitted in input order, which may increase memory usage.
    /// When false (default), results are returned as they complete for maximum throughput.
    /// Defaults to false.
    /// </summary>
    public bool OrderedOutput { get; init; } = false;

    /// <summary>
    /// Gets the progress reporting options for tracking operation progress.
    /// When null, no progress reporting is performed. Defaults to null.
    /// </summary>
    /// <remarks>
    /// Progress reporting provides real-time visibility into long-running operations with metrics including:
    /// items processed, processing rate, estimated time remaining, and error counts.
    /// Useful for ETL jobs, bulk imports, data migrations, and other long-running batch operations.
    /// </remarks>
    public ProgressOptions? Progress { get; init; }

    /// <summary>
    /// Gets the runtime metrics collection options for monitoring parallel operations.
    /// When null, metrics are still exposed via EventCounters but no callback is invoked. Defaults to null.
    /// </summary>
    /// <remarks>
    /// Runtime metrics provide visibility into operational characteristics including:
    /// active workers, queue depth, throughput, error rates, retries, and backpressure events.
    /// Metrics are always available via .NET EventCounters for zero-cost monitoring.
    /// The optional callback enables custom metric export to monitoring systems like Prometheus, Application Insights, or DataDog.
    /// Useful for production monitoring, performance tuning, alerting, and capacity planning.
    /// </remarks>
    public MetricsOptions? Metrics { get; init; }

    /// <summary>
    /// Gets the rate limiting options using the token bucket algorithm.
    /// When null, no rate limiting is applied. Defaults to null.
    /// </summary>
    /// <remarks>
    /// Rate limiting controls the maximum rate at which operations can be executed,
    /// useful for respecting API rate limits, preventing resource exhaustion, and smoothing traffic bursts.
    /// The token bucket algorithm allows for controlled bursts while maintaining an average rate limit.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Limit to 100 requests per second with burst capacity of 200
    /// RateLimit = new RateLimitOptions
    /// {
    ///     TokensPerSecond = 100,
    ///     BurstCapacity = 200
    /// }
    /// </code>
    /// </example>
    public RateLimitOptions? RateLimit { get; init; }

    /// <summary>
    /// Gets the circuit breaker options for preventing cascading failures.
    /// When null, no circuit breaker is used. Defaults to null.
    /// </summary>
    /// <remarks>
    /// Circuit breaker prevents cascading failures by failing fast when a service is experiencing issues.
    /// It has three states: Closed (normal operation), Open (failing fast), and HalfOpen (testing recovery).
    /// Useful for protecting downstream services, preventing resource exhaustion, and improving system resilience.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Open circuit after 5 consecutive failures, test recovery after 30 seconds
    /// CircuitBreaker = new CircuitBreakerOptions
    /// {
    ///     FailureThreshold = 5,
    ///     SuccessThreshold = 2,
    ///     OpenTimeout = TimeSpan.FromSeconds(30)
    /// }
    /// </code>
    /// </example>
    public CircuitBreakerOptions? CircuitBreaker { get; init; }

    /// <summary>
    /// Gets the adaptive concurrency options for dynamically adjusting parallelism based on performance.
    /// When null, static concurrency (MaxDegreeOfParallelism) is used. Defaults to null.
    /// </summary>
    /// <remarks>
    /// Adaptive concurrency automatically adjusts the degree of parallelism based on system performance metrics
    /// such as latency and success rate. Uses AIMD (Additive Increase Multiplicative Decrease) algorithm similar to TCP congestion control.
    /// When enabled, takes precedence over MaxDegreeOfParallelism for controlling active workers.
    /// Useful for auto-scaling to optimal throughput, handling variable load, and preventing overload.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Auto-adjust between 1-32 workers based on latency and success rate
    /// AdaptiveConcurrency = new AdaptiveConcurrencyOptions
    /// {
    ///     MinConcurrency = 1,
    ///     MaxConcurrency = 32,
    ///     TargetLatency = TimeSpan.FromMilliseconds(100),
    ///     MinSuccessRate = 0.95
    /// }
    /// </code>
    /// </example>
    public AdaptiveConcurrencyOptions? AdaptiveConcurrency { get; init; }
}
