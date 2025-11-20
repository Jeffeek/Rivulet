namespace Rivulet.RetryPolicies;

/// <summary>
/// Represents a resilience policy that can be applied to operations.
/// </summary>
/// <typeparam name="TResult">The type of result returned by operations.</typeparam>
public interface IPolicy<TResult>
{
    /// <summary>
    /// Executes an operation with the policy applied.
    /// </summary>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    ValueTask<TResult> ExecuteAsync(
        Func<CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Wraps this policy with another policy, creating a composite policy.
    /// </summary>
    /// <param name="outerPolicy">The outer policy to wrap this policy with.</param>
    /// <returns>A new composite policy.</returns>
    IPolicy<TResult> Wrap(IPolicy<TResult> outerPolicy);
}
