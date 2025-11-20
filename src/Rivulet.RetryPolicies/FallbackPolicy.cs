namespace Rivulet.RetryPolicies;

/// <summary>
/// A fallback policy that provides a fallback result when an operation fails.
/// </summary>
/// <typeparam name="TResult">The type of result returned by operations.</typeparam>
public sealed class FallbackPolicy<TResult> : IPolicy<TResult>
{
    private readonly Func<Exception, CancellationToken, ValueTask<TResult>> _fallbackFunc;
    private readonly Func<Exception, bool>? _shouldFallback;
    private readonly Func<Exception, ValueTask>? _onFallback;

    /// <summary>
    /// Creates a new fallback policy with a static fallback value.
    /// </summary>
    /// <param name="fallbackValue">The fallback value to return on failure.</param>
    /// <param name="shouldFallback">Optional predicate to determine if fallback should be applied.</param>
    /// <param name="onFallback">Optional callback invoked when fallback is triggered.</param>
    public FallbackPolicy(
        TResult fallbackValue,
        Func<Exception, bool>? shouldFallback = null,
        Func<Exception, ValueTask>? onFallback = null)
        : this((_, _) => ValueTask.FromResult(fallbackValue), shouldFallback, onFallback)
    {
    }

    /// <summary>
    /// Creates a new fallback policy with a dynamic fallback function.
    /// </summary>
    /// <param name="fallbackFunc">Function to compute the fallback value.</param>
    /// <param name="shouldFallback">Optional predicate to determine if fallback should be applied.</param>
    /// <param name="onFallback">Optional callback invoked when fallback is triggered.</param>
    public FallbackPolicy(
        Func<Exception, CancellationToken, ValueTask<TResult>> fallbackFunc,
        Func<Exception, bool>? shouldFallback = null,
        Func<Exception, ValueTask>? onFallback = null)
    {
        _fallbackFunc = fallbackFunc ?? throw new ArgumentNullException(nameof(fallbackFunc));
        _shouldFallback = shouldFallback ?? (_ => true);
        _onFallback = onFallback;
    }

    /// <inheritdoc/>
    public async ValueTask<TResult> ExecuteAsync(
        Func<CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        try
        {
            return await operation(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (_shouldFallback?.Invoke(ex) ?? true)
        {
            if (_onFallback != null)
            {
                await _onFallback(ex).ConfigureAwait(false);
            }

            return await _fallbackFunc(ex, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public IPolicy<TResult> Wrap(IPolicy<TResult> outerPolicy)
    {
        ArgumentNullException.ThrowIfNull(outerPolicy);
        return new CompositePolicy<TResult>(outerPolicy, this);
    }
}
