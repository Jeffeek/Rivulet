namespace Rivulet.RetryPolicies;

/// <summary>
/// A timeout policy that enforces a maximum execution time for operations.
/// </summary>
/// <typeparam name="TResult">The type of result returned by operations.</typeparam>
public sealed class TimeoutPolicy<TResult> : IPolicy<TResult>
{
    private readonly TimeSpan _timeout;
    private readonly Func<TimeSpan, ValueTask>? _onTimeout;

    /// <summary>
    /// Creates a new timeout policy.
    /// </summary>
    /// <param name="timeout">The maximum time allowed for operation execution.</param>
    /// <param name="onTimeout">Optional callback invoked when a timeout occurs.</param>
    public TimeoutPolicy(TimeSpan timeout, Func<TimeSpan, ValueTask>? onTimeout = null)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive");

        _timeout = timeout;
        _onTimeout = onTimeout;
    }

    /// <inheritdoc/>
    public async ValueTask<TResult> ExecuteAsync(
        Func<CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_timeout);

        try
        {
            return await operation(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            if (_onTimeout != null)
            {
                await _onTimeout(_timeout).ConfigureAwait(false);
            }

            throw new TimeoutException($"Operation timed out after {_timeout.TotalMilliseconds}ms");
        }
    }

    /// <inheritdoc/>
    public IPolicy<TResult> Wrap(IPolicy<TResult> outerPolicy)
    {
        ArgumentNullException.ThrowIfNull(outerPolicy);
        return new CompositePolicy<TResult>(outerPolicy, this);
    }
}
