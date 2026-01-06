using System.Diagnostics.CodeAnalysis;
using Polly;
using Rivulet.Core;

namespace Rivulet.Polly;

/// <summary>
///     Extensions for integrating Polly resilience policies with Rivulet parallel operations.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
public static class PollyParallelExtensions
{
    /// <summary>
    ///     Applies a Polly policy to each item in a parallel operation.
    /// </summary>
    /// <typeparam name="TSource">The type of source items.</typeparam>
    /// <typeparam name="TResult">The type of result items.</typeparam>
    /// <param name="source">The source items to process.</param>
    /// <param name="selector">The transformation function.</param>
    /// <param name="policy">The Polly policy to apply to each operation.</param>
    /// <param name="options">Optional parallel processing options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The results of the parallel operation with the policy applied.</returns>
    /// <remarks>
    ///     This allows you to use any Polly policy (retry, circuit breaker, timeout, bulkhead, etc.)
    ///     with Rivulet's parallel processing capabilities. The policy is applied individually to each item.
    /// </remarks>
    /// <example>
    ///     <code>
    /// var retryPolicy = Policy
    ///     .Handle&lt;HttpRequestException&gt;()
    ///     .WaitAndRetryAsync(3, retry => TimeSpan.FromSeconds(Math.Pow(2, retry)));
    /// 
    /// var results = await items.SelectParallelWithPolicyAsync(
    ///     async (item, ct) => await CallApiAsync(item, ct),
    ///     retryPolicy,
    ///     new ParallelOptionsRivulet { MaxDegreeOfParallelism = 10 });
    /// </code>
    /// </example>
    public static Task<List<TResult>> SelectParallelWithPolicyAsync<TSource, TResult>(
        this IEnumerable<TSource> source,
        Func<TSource, CancellationToken, ValueTask<TResult>> selector,
        ResiliencePipeline policy,
        ParallelOptionsRivulet? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(policy);

        return source.SelectParallelAsync((item, ct) => policy.ExecuteAsync(token => selector(item, token), ct),
            options,
            cancellationToken);
    }

    /// <summary>
    ///     Applies a Polly policy to each item in a parallel operation with result-based handling.
    /// </summary>
    /// <typeparam name="TSource">The type of source items.</typeparam>
    /// <typeparam name="TResult">The type of result items.</typeparam>
    /// <param name="source">The source items to process.</param>
    /// <param name="selector">The transformation function.</param>
    /// <param name="policy">The Polly pipeline to apply to each operation.</param>
    /// <param name="options">Optional parallel processing options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The results of the parallel operation with the policy applied.</returns>
    /// <remarks>
    ///     This overload works with Polly's ResiliencePipeline&lt;TResult&gt; which allows
    ///     result-based policies (e.g., retry when result matches a condition, not just on exceptions).
    /// </remarks>
    /// <example>
    ///     <code>
    /// var pipeline = new ResiliencePipelineBuilder&lt;HttpResponseMessage&gt;()
    ///     .AddRetry(new RetryStrategyOptions&lt;HttpResponseMessage&gt;
    ///     {
    ///         ShouldHandle = new PredicateBuilder&lt;HttpResponseMessage&gt;()
    ///             .Handle&lt;HttpRequestException&gt;()
    ///             .HandleResult(r => r.StatusCode == HttpStatusCode.TooManyRequests),
    ///         MaxRetryAttempts = 3
    ///     })
    ///     .Build();
    /// 
    /// var results = await urls.SelectParallelWithPolicyAsync(
    ///     async (url, ct) => await httpClient.GetAsync(url, ct),
    ///     pipeline);
    /// </code>
    /// </example>
    public static Task<List<TResult>> SelectParallelWithPolicyAsync<TSource, TResult>(
        this IEnumerable<TSource> source,
        Func<TSource, CancellationToken, ValueTask<TResult>> selector,
        ResiliencePipeline<TResult> policy,
        ParallelOptionsRivulet? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(policy);

        return source.SelectParallelAsync((item, ct) => policy.ExecuteAsync(token => selector(item, token), ct),
            options,
            cancellationToken);
    }

    /// <summary>
    ///     Applies a Polly policy to each item in a parallel operation with side effects only.
    /// </summary>
    /// <typeparam name="TSource">The type of source items.</typeparam>
    /// <param name="source">The source items to process.</param>
    /// <param name="action">The action to perform on each item.</param>
    /// <param name="policy">The Polly policy to apply to each operation.</param>
    /// <param name="options">Optional parallel processing options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    ///     This allows you to use any Polly policy for side-effect operations.
    ///     Internally uses SelectParallelAsync and discards results.
    /// </remarks>
    /// <example>
    ///     <code>
    /// var retryPolicy = new ResiliencePipelineBuilder()
    ///     .AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 3 })
    ///     .Build();
    /// 
    /// await items.ForEachParallelWithPolicyAsync(
    ///     async (item, ct) => await ProcessItemAsync(item, ct),
    ///     retryPolicy,
    ///     new ParallelOptionsRivulet { MaxDegreeOfParallelism = 10 });
    /// </code>
    /// </example>
    public static Task ForEachParallelWithPolicyAsync<TSource>(
        this IEnumerable<TSource> source,
        Func<TSource, CancellationToken, ValueTask> action,
        ResiliencePipeline policy,
        ParallelOptionsRivulet? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(policy);

        return source.ForEachParallelAsync(
            async (item, ct) =>
            {
                await policy.ExecuteAsync(
                        async token =>
                        {
                            await action(item, token).ConfigureAwait(false);
                            return ValueTask.CompletedTask;
                        },
                        ct)
                    .ConfigureAwait(false);
            },
            options,
            cancellationToken);
    }
}
