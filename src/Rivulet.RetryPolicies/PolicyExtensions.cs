using Rivulet.Core;

namespace Rivulet.RetryPolicies;

/// <summary>
/// Extension methods for applying policies to parallel operations.
/// </summary>
public static class PolicyExtensions
{
    /// <summary>
    /// Applies a policy to each item in a parallel operation.
    /// </summary>
    /// <typeparam name="TSource">The type of source items.</typeparam>
    /// <typeparam name="TResult">The type of result items.</typeparam>
    /// <param name="source">The source items.</param>
    /// <param name="selector">The transformation function.</param>
    /// <param name="policy">The policy to apply.</param>
    /// <param name="options">Parallel options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The results of the parallel operation with policy applied.</returns>
    public static async Task<List<TResult>> SelectParallelAsync<TSource, TResult>(
        this IEnumerable<TSource> source,
        Func<TSource, CancellationToken, ValueTask<TResult>> selector,
        IPolicy<TResult> policy,
        ParallelOptionsRivulet? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(policy);

        return await source.SelectParallelAsync(
            async (item, ct) => await policy.ExecuteAsync(ct2 => selector(item, ct2), ct).ConfigureAwait(false),
            options,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Wraps this policy with another policy, creating a composite policy.
    /// The outer policy wraps the inner policy.
    /// </summary>
    /// <typeparam name="TResult">The type of result.</typeparam>
    /// <param name="innerPolicy">The inner policy.</param>
    /// <param name="outerPolicy">The outer policy to wrap this policy with.</param>
    /// <returns>A new composite policy.</returns>
    public static IPolicy<TResult> WrapWith<TResult>(
        this IPolicy<TResult> innerPolicy,
        IPolicy<TResult> outerPolicy)
    {
        ArgumentNullException.ThrowIfNull(innerPolicy);
        ArgumentNullException.ThrowIfNull(outerPolicy);

        return new CompositePolicy<TResult>(outerPolicy, innerPolicy);
    }
}
