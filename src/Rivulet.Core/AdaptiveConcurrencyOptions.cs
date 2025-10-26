namespace Rivulet.Core;

/// <summary>
/// Configuration options for adaptive concurrency control.
/// Dynamically adjusts parallelism based on system performance and load.
/// </summary>
public sealed class AdaptiveConcurrencyOptions
{
    /// <summary>
    /// Minimum degree of parallelism (lower bound).
    /// Default: 1
    /// </summary>
    public int MinConcurrency { get; init; } = 1;

    /// <summary>
    /// Maximum degree of parallelism (upper bound).
    /// Default: Environment.ProcessorCount * 2
    /// </summary>
    public int MaxConcurrency { get; init; } = Math.Max(1, Environment.ProcessorCount * 2);

    /// <summary>
    /// Initial concurrency level to start with.
    /// If null, starts at MinConcurrency.
    /// Default: null
    /// </summary>
    public int? InitialConcurrency { get; init; }

    /// <summary>
    /// How often to sample performance and adjust concurrency.
    /// Default: 1 second
    /// </summary>
    public TimeSpan SampleInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Target latency percentile (p50, p95, p99) for measuring performance.
    /// When average latency exceeds this target, concurrency is decreased.
    /// If null, latency-based adjustments are disabled.
    /// Default: null (disabled)
    /// </summary>
    public TimeSpan? TargetLatency { get; init; }

    /// <summary>
    /// Minimum success rate (0.0 to 1.0) required to increase concurrency.
    /// When success rate drops below this threshold, concurrency is decreased.
    /// Default: 0.95 (95%)
    /// </summary>
    public double MinSuccessRate { get; init; } = 0.95;

    /// <summary>
    /// Strategy for adjusting concurrency when performance is good.
    /// - AIMD (default): Additive Increase (add 1 worker per sample interval)
    /// - Aggressive: Faster increase (add 10% of current concurrency)
    /// </summary>
    public AdaptiveConcurrencyStrategy IncreaseStrategy { get; init; } = AdaptiveConcurrencyStrategy.AIMD;

    /// <summary>
    /// Strategy for adjusting concurrency when performance degrades.
    /// - AIMD (default): Multiplicative Decrease (reduce by 50%)
    /// - Gradual: Slower decrease (reduce by 25%)
    /// </summary>
    public AdaptiveConcurrencyStrategy DecreaseStrategy { get; init; } = AdaptiveConcurrencyStrategy.AIMD;

    /// <summary>
    /// Optional callback invoked when concurrency level changes.
    /// Receives old concurrency and new concurrency.
    /// Useful for monitoring and alerting.
    /// </summary>
    public Func<int, int, ValueTask>? OnConcurrencyChange { get; init; }

    /// <summary>
    /// Validates the adaptive concurrency options.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when options are invalid.</exception>
    internal void Validate()
    {
        if (MinConcurrency <= 0)
            throw new ArgumentException("MinConcurrency must be greater than 0.", nameof(MinConcurrency));

        if (MaxConcurrency < MinConcurrency)
            throw new ArgumentException("MaxConcurrency must be greater than or equal to MinConcurrency.", nameof(MaxConcurrency));

        if (InitialConcurrency.HasValue && (InitialConcurrency.Value < MinConcurrency || InitialConcurrency.Value > MaxConcurrency))
            throw new ArgumentException("InitialConcurrency must be between MinConcurrency and MaxConcurrency.", nameof(InitialConcurrency));

        if (SampleInterval <= TimeSpan.Zero)
            throw new ArgumentException("SampleInterval must be greater than zero.", nameof(SampleInterval));

        if (TargetLatency.HasValue && TargetLatency.Value <= TimeSpan.Zero)
            throw new ArgumentException("TargetLatency must be greater than zero when specified.", nameof(TargetLatency));

        if (MinSuccessRate is < 0.0 or > 1.0)
            throw new ArgumentException("MinSuccessRate must be between 0.0 and 1.0.", nameof(MinSuccessRate));
    }
}

/// <summary>
/// Strategy for adjusting adaptive concurrency.
/// </summary>
public enum AdaptiveConcurrencyStrategy
{
    /// <summary>
    /// AIMD (Additive Increase Multiplicative Decrease) strategy.
    /// Increase: Add 1 worker per interval.
    /// Decrease: Reduce by 50%.
    /// Similar to TCP congestion control.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    AIMD,

    /// <summary>
    /// Aggressive strategy for faster adjustments.
    /// Increase: Add 10% of current workers.
    /// Decrease: Reduce by 50% (same as AIMD).
    /// </summary>
    Aggressive,

    /// <summary>
    /// Gradual strategy for slower, smoother adjustments.
    /// Increase: Add 1 worker per interval (same as AIMD).
    /// Decrease: Reduce by 25%.
    /// </summary>
    Gradual
}
