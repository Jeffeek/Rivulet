namespace Rivulet.Core.Resilience;

/// <summary>
///     Configuration options for circuit breaker pattern.
///     Prevents cascading failures by failing fast when a service is experiencing issues.
/// </summary>
public sealed class CircuitBreakerOptions
{
    /// <summary>
    ///     Number of consecutive failures required to open the circuit.
    ///     Default: 5
    /// </summary>
    public int FailureThreshold { get; init; } = 5;

    /// <summary>
    ///     Number of consecutive successes in HalfOpen state required to close the circuit.
    ///     Default: 2
    /// </summary>
    public int SuccessThreshold { get; init; } = 2;

    /// <summary>
    ///     Duration to wait in Open state before transitioning to HalfOpen to test recovery.
    ///     Default: 30 seconds
    /// </summary>
    public TimeSpan OpenTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Optional time window for tracking failures. When set, failures outside this window are ignored.
    ///     This enables percentage-based thresholds over a sliding time window.
    ///     Default: null (disabled, uses consecutive failures)
    /// </summary>
    public TimeSpan? SamplingDuration { get; init; }

    /// <summary>
    ///     Optional callback invoked when the circuit breaker state changes.
    ///     Useful for monitoring and alerting.
    /// </summary>
    public Func<CircuitBreakerState, CircuitBreakerState, ValueTask>? OnStateChange { get; init; }

    /// <summary>
    ///     Validates the circuit breaker options.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when options are invalid.</exception>
    public void Validate()
    {
        if (FailureThreshold <= 0) throw new ArgumentException("FailureThreshold must be greater than 0.", nameof(FailureThreshold));

        if (SuccessThreshold <= 0) throw new ArgumentException("SuccessThreshold must be greater than 0.", nameof(SuccessThreshold));

        if (OpenTimeout <= TimeSpan.Zero) throw new ArgumentException("OpenTimeout must be greater than zero.", nameof(OpenTimeout));

        if (SamplingDuration.HasValue && SamplingDuration.Value <= TimeSpan.Zero) throw new ArgumentException("SamplingDuration must be greater than zero when specified.", nameof(SamplingDuration));
    }
}