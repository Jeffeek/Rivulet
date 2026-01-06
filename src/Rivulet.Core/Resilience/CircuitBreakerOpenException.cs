using System.Diagnostics.CodeAnalysis;

namespace Rivulet.Core.Resilience;

/// <summary>
///     Exception thrown when a circuit breaker is in the Open state and rejects operations.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
public sealed class CircuitBreakerOpenException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the CircuitBreakerOpenException class.
    /// </summary>
    public CircuitBreakerOpenException()
        : base("Circuit breaker is open. Operation rejected to prevent cascading failures.") =>
        State = CircuitBreakerState.Open;

    /// <summary>
    ///     Initializes a new instance of the CircuitBreakerOpenException class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public CircuitBreakerOpenException(string message)
        : base(message) =>
        State = CircuitBreakerState.Open;

    /// <summary>
    ///     Initializes a new instance of the CircuitBreakerOpenException class with a specified state.
    /// </summary>
    /// <param name="state">The current circuit breaker state.</param>
    internal CircuitBreakerOpenException(CircuitBreakerState state)
        : base($"Circuit breaker is {state}. Operation rejected.") =>
        State = state;

    /// <summary>
    ///     Gets the current state of the circuit breaker.
    /// </summary>
    public CircuitBreakerState State { get; }
}
