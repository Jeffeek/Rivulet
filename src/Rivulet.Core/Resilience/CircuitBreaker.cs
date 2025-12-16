using System.Collections.Concurrent;
using Rivulet.Core.Internal;
using Rivulet.Core.Observability;

namespace Rivulet.Core.Resilience;

/// <summary>
///     Thread-safe implementation of the circuit breaker pattern.
///     Prevents cascading failures by failing fast when a threshold of failures is reached.
/// </summary>
internal sealed class CircuitBreaker
{
    private readonly CircuitBreakerOptions _options;
    private readonly ConcurrentQueue<long> _failureTimestamps;
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    private CircuitBreakerState _state;
    private int _consecutiveFailures;
    private int _consecutiveSuccesses;
    private long _openedAtTicks;

    /// <summary>
    ///     Gets the current state of the circuit breaker.
    /// </summary>
    public CircuitBreakerState State => LockHelper.Execute(_lock, () => _state);

    /// <summary>
    ///     Initializes a new instance of the CircuitBreaker class.
    /// </summary>
    /// <param name="options">Circuit breaker configuration options.</param>
    public CircuitBreaker(CircuitBreakerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();

        _state = CircuitBreakerState.Closed;
        _failureTimestamps = new();
        _consecutiveFailures = 0;
        _consecutiveSuccesses = 0;
        _openedAtTicks = 0;
    }

    /// <summary>
    ///     Executes an operation with circuit breaker protection.
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

    private ValueTask BeforeExecuteAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        LockHelper.Execute(_lock,
            () =>
            {
                if (_state == CircuitBreakerState.Open &&
                    Environment.TickCount64 - _openedAtTicks >= (long)_options.OpenTimeout.TotalMilliseconds)
                    TransitionToHalfOpen();

                if (_state == CircuitBreakerState.Open) throw new CircuitBreakerOpenException(_state);
            });

        return ValueTask.CompletedTask;
    }

    private void OnSuccess() =>
        LockHelper.Execute(_lock,
            () =>
            {
                _consecutiveFailures = 0;

                if (_state != CircuitBreakerState.HalfOpen) return;

                _consecutiveSuccesses++;

                if (_consecutiveSuccesses >= _options.SuccessThreshold) TransitionToClosed();
            });

    private void OnFailure() =>
        LockHelper.Execute(_lock,
            () =>
            {
                _consecutiveSuccesses = 0;

                // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
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
                            var now = Environment.TickCount64;
                            _failureTimestamps.Enqueue(now);

                            var cutoffMs = (long)_options.SamplingDuration.Value.TotalMilliseconds;
                            while (_failureTimestamps.TryPeek(out var timestamp) && now - timestamp > cutoffMs) _failureTimestamps.TryDequeue(out _);

                            if (_failureTimestamps.Count >= _options.FailureThreshold) TransitionToOpen();
                        }
                        else
                        {
                            if (_consecutiveFailures >= _options.FailureThreshold) TransitionToOpen();
                        }

                        break;
                    }
                }
            });

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
                    catch (Exception ex)
                    {
                        RivuletEventSource.Log.CallbackFailed(nameof(CircuitBreakerOptions.OnStateChange), ex.GetType().Name, ex.Message);
                    }
                },
                CancellationToken.None);
        }
    }

    private void TransitionToOpen()
    {
        var oldState = _state;
        _state = CircuitBreakerState.Open;
        _openedAtTicks = Environment.TickCount64;
        _consecutiveSuccesses = 0;

        if (_options.OnStateChange is not null && oldState != CircuitBreakerState.Open)
        {
            _ = Task.Run(async () =>
                {
                    try
                    {
                        await _options.OnStateChange(oldState, CircuitBreakerState.Open).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        RivuletEventSource.Log.CallbackFailed(nameof(CircuitBreakerOptions.OnStateChange), ex.GetType().Name, ex.Message);
                    }
                },
                CancellationToken.None);
        }
    }

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
                    catch (Exception ex)
                    {
                        RivuletEventSource.Log.CallbackFailed(nameof(CircuitBreakerOptions.OnStateChange), ex.GetType().Name, ex.Message);
                    }
                },
                CancellationToken.None);
        }
    }

    /// <summary>
    ///     Resets the circuit breaker to the Closed state.
    ///     Useful for testing or manual intervention.
    /// </summary>
    internal void Reset() =>
        LockHelper.Execute(_lock, TransitionToClosed);
}