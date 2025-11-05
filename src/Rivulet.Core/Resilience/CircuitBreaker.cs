using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Rivulet.Core.Internal;

namespace Rivulet.Core.Resilience;

/// <summary>
/// Thread-safe implementation of the circuit breaker pattern.
/// Prevents cascading failures by failing fast when a threshold of failures is reached.
/// </summary>
[SuppressMessage("ReSharper", "InconsistentlySynchronizedField")]
internal sealed class CircuitBreaker
{
    private readonly CircuitBreakerOptions _options;
    private readonly ConcurrentQueue<DateTime> _failureTimestamps;
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    private CircuitBreakerState _state;
    private int _consecutiveFailures;
    private int _consecutiveSuccesses;
    private DateTime _openedAt;

    /// <summary>
    /// Gets the current state of the circuit breaker.
    /// </summary>
    public CircuitBreakerState State => LockHelper.Execute(_lock, () => _state);

    /// <summary>
    /// Initializes a new instance of the CircuitBreaker class.
    /// </summary>
    /// <param name="options">Circuit breaker configuration options.</param>
    public CircuitBreaker(CircuitBreakerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();

        _state = CircuitBreakerState.Closed;
        _failureTimestamps = new ConcurrentQueue<DateTime>();
        _consecutiveFailures = 0;
        _consecutiveSuccesses = 0;
        _openedAt = DateTime.MinValue;
    }

    /// <summary>
    /// Executes an operation with circuit breaker protection.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    /// <exception cref="CircuitBreakerOpenException">Thrown when circuit is open.</exception>
    public async ValueTask<T> ExecuteAsync<T>(Func<ValueTask<T>> operation, CancellationToken cancellationToken = default)
    {
        await BeforeExecuteAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var result = await operation().ConfigureAwait(false);
            OnSuccess();
            return result;
        }
        catch
        {
            OnFailure();
            throw;
        }
    }

    /// <summary>
    /// Checks circuit state before executing operation.
    /// </summary>
    private ValueTask BeforeExecuteAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        LockHelper.Execute(_lock, () =>
        {
            if (_state == CircuitBreakerState.Open && DateTime.UtcNow >= _openedAt.Add(_options.OpenTimeout))
            {
                TransitionToHalfOpen();
            }

            if (_state == CircuitBreakerState.Open)
            {
                throw new CircuitBreakerOpenException(_state);
            }
        });

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Records a successful operation.
    /// </summary>
    private void OnSuccess()
    {
        LockHelper.Execute(_lock, () =>
        {
            _consecutiveFailures = 0;

            if (_state != CircuitBreakerState.HalfOpen) return;

            _consecutiveSuccesses++;

            if (_consecutiveSuccesses >= _options.SuccessThreshold)
            {
                TransitionToClosed();
            }
        });
    }

    /// <summary>
    /// Records a failed operation.
    /// </summary>
    private void OnFailure()
    {
        LockHelper.Execute(_lock, () =>
        {
            _consecutiveSuccesses = 0;

            switch (_state)
            {
                case CircuitBreakerState.HalfOpen:
                    TransitionToOpen();
                    return;
                case CircuitBreakerState.Closed:
                {
                    _consecutiveFailures++;

                    if (_options.SamplingDuration.HasValue)
                    {
                        var now = DateTime.UtcNow;
                        _failureTimestamps.Enqueue(now);

                        var cutoff = now.Subtract(_options.SamplingDuration.Value);
                        while (_failureTimestamps.TryPeek(out var timestamp) && timestamp < cutoff)
                        {
                            _failureTimestamps.TryDequeue(out _);
                        }

                        if (_failureTimestamps.Count >= _options.FailureThreshold)
                        {
                            TransitionToOpen();
                        }
                    }
                    else
                    {
                        if (_consecutiveFailures >= _options.FailureThreshold)
                        {
                            TransitionToOpen();
                        }
                    }

                    break;
                }
            }
        });
    }

    /// <summary>
    /// Transitions to the Closed state.
    /// </summary>
    private void TransitionToClosed()
    {
        var oldState = _state;
        _state = CircuitBreakerState.Closed;
        _consecutiveFailures = 0;
        _consecutiveSuccesses = 0;
        _failureTimestamps.Clear();

        if (_options.OnStateChange is not null && oldState != CircuitBreakerState.Closed)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _options.OnStateChange(oldState, CircuitBreakerState.Closed).ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            }, CancellationToken.None);
        }
    }

    /// <summary>
    /// Transitions to the Open state.
    /// </summary>
    private void TransitionToOpen()
    {
        var oldState = _state;
        _state = CircuitBreakerState.Open;
        _openedAt = DateTime.UtcNow;
        _consecutiveSuccesses = 0;

        if (_options.OnStateChange is not null && oldState != CircuitBreakerState.Open)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _options.OnStateChange(oldState, CircuitBreakerState.Open).ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            }, CancellationToken.None);
        }
    }

    /// <summary>
    /// Transitions to the HalfOpen state.
    /// </summary>
    private void TransitionToHalfOpen()
    {
        var oldState = _state;
        _state = CircuitBreakerState.HalfOpen;
        _consecutiveSuccesses = 0;
        _consecutiveFailures = 0;

        if (_options.OnStateChange is not null && oldState != CircuitBreakerState.HalfOpen)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _options.OnStateChange(oldState, CircuitBreakerState.HalfOpen).ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            }, CancellationToken.None);
        }
    }

    /// <summary>
    /// Resets the circuit breaker to the Closed state.
    /// Useful for testing or manual intervention.
    /// </summary>
    internal void Reset()
    {
        LockHelper.Execute(_lock, TransitionToClosed);
    }
}
