namespace Rivulet.RetryPolicies;

/// <summary>
/// A composite policy that combines multiple policies, applying them in sequence.
/// The outer policy wraps the inner policy.
/// </summary>
/// <typeparam name="TResult">The type of result returned by operations.</typeparam>
public sealed class CompositePolicy<TResult> : IPolicy<TResult>
{
    private readonly IPolicy<TResult> _outerPolicy;
    private readonly IPolicy<TResult> _innerPolicy;

    /// <summary>
    /// Creates a new composite policy.
    /// </summary>
    /// <param name="outerPolicy">The outer policy (executed first).</param>
    /// <param name="innerPolicy">The inner policy (executed second).</param>
    public CompositePolicy(IPolicy<TResult> outerPolicy, IPolicy<TResult> innerPolicy)
    {
        _outerPolicy = outerPolicy ?? throw new ArgumentNullException(nameof(outerPolicy));
        _innerPolicy = innerPolicy ?? throw new ArgumentNullException(nameof(innerPolicy));
    }

    /// <inheritdoc/>
    public async ValueTask<TResult> ExecuteAsync(
        Func<CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return await _outerPolicy.ExecuteAsync(
            ct => _innerPolicy.ExecuteAsync(operation, ct),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public IPolicy<TResult> Wrap(IPolicy<TResult> outerPolicy)
    {
        ArgumentNullException.ThrowIfNull(outerPolicy);
        return new CompositePolicy<TResult>(outerPolicy, this);
    }
}
