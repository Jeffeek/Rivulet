namespace Rivulet.Core.Resilience;

/// <summary>
/// Represents the state of a circuit breaker.
/// </summary>
public enum CircuitBreakerState
{
    /// <summary>
    /// Circuit is closed. Requests flow normally.
    /// Failures are tracked and may transition to Open state if threshold is exceeded.
    /// </summary>
    Closed,

    /// <summary>
    /// Circuit is open. Requests fail fast without executing the operation.
    /// After a timeout period, transitions to HalfOpen state to test if service recovered.
    /// </summary>
    Open,

    /// <summary>
    /// Circuit is testing recovery. Limited requests are allowed to test if service is healthy.
    /// Success transitions to Closed state. Failure transitions back to Open state.
    /// </summary>
    HalfOpen
}
