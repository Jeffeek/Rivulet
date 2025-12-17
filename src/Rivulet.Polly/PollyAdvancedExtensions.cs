using Polly;
using Rivulet.Core;

namespace Rivulet.Polly;

/// <summary>
///     Advanced Polly features integrated with Rivulet parallel processing.
/// </summary>
public static class PollyAdvancedExtensions
{
    /// <summary>
    ///     Processes items in parallel with hedging strategy - sends parallel requests and returns the first successful
    ///     result.
    /// </summary>
    /// <typeparam name="TSource">The type of source items.</typeparam>
    /// <typeparam name="TResult">The type of result items.</typeparam>
    /// <param name="source">The source items to process.</param>
    /// <param name="selector">The transformation function.</param>
    /// <param name="maxHedgedAttempts">Maximum number of hedged attempts (default: 2).</param>
    /// <param name="hedgingDelay">Delay before sending hedged request (default: 100ms).</param>
    /// <param name="options">Optional parallel processing options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The results of the parallel operation with hedging applied.</returns>
    /// <remarks>
    ///     Hedging sends multiple parallel requests for the same operation and uses the first successful result.
    ///     This is useful for reducing tail latency when calling unreliable services.
    ///     If the first request is slow, a hedged request is sent after the delay.
    /// </remarks>
    /// <example>
    ///     <code>
    /// // If first API call is slow, send a hedged request after 100ms
    /// var results = await urls.SelectParallelWithHedgingAsync(
    ///     async (url, ct) => await httpClient.GetAsync(url, ct),
    ///     maxHedgedAttempts: 2,
    ///     hedgingDelay: TimeSpan.FromMilliseconds(100));
    /// </code>
    /// </example>
    public static Task<List<TResult>> SelectParallelWithHedgingAsync<TSource, TResult>(
        this IEnumerable<TSource> source,
        Func<TSource, CancellationToken, ValueTask<TResult>> selector,
        int maxHedgedAttempts = 2,
        TimeSpan? hedgingDelay = null,
        ParallelOptionsRivulet? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        if (maxHedgedAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maxHedgedAttempts), "Must be at least 1");

        var delay = hedgingDelay ?? TimeSpan.FromMilliseconds(100);

        return source.SelectParallelAsync((item, ct) =>
            {
                // Create a hedging pipeline specifically for this item
                // Note: We need to create per-item because the ActionGenerator must capture 'item'
                var pipeline = new ResiliencePipelineBuilder<TResult>()
                    .AddHedging(new()
                    {
                        MaxHedgedAttempts = maxHedgedAttempts,
                        Delay = delay,
                        ActionGenerator = args =>
                        {
                            // Create a hedged action that executes the selector with the captured item
                            return async () =>
                                Outcome.FromResult(await selector(item, args.ActionContext.CancellationToken)
                                    .ConfigureAwait(false));
                        }
                    })
                    .Build();

                return pipeline.ExecuteAsync(token => selector(item, token),
                    ct);
            },
            options,
            cancellationToken);
    }

    /// <summary>
    ///     Processes items in parallel with result-based retry - retries based on the returned value, not just exceptions.
    /// </summary>
    /// <typeparam name="TSource">The type of source items.</typeparam>
    /// <typeparam name="TResult">The type of result items.</typeparam>
    /// <param name="source">The source items to process.</param>
    /// <param name="selector">The transformation function.</param>
    /// <param name="shouldRetry">Predicate to determine if a result should trigger a retry.</param>
    /// <param name="maxRetries">Maximum number of retry attempts (default: 3).</param>
    /// <param name="delayBetweenRetries">Delay between retries (default: 100ms).</param>
    /// <param name="options">Optional parallel processing options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The results of the parallel operation with result-based retry applied.</returns>
    /// <remarks>
    ///     Unlike exception-based retry, this allows retrying when the operation succeeds but returns an undesirable result.
    ///     Useful for APIs that return error status codes in the response body rather than throwing exceptions.
    /// </remarks>
    /// <example>
    ///     <code>
    /// // Retry if API returns 429 (TooManyRequests) status code
    /// var results = await urls.SelectParallelWithResultRetryAsync(
    ///     async (url, ct) => await httpClient.GetAsync(url, ct),
    ///     shouldRetry: response => response.StatusCode == HttpStatusCode.TooManyRequests,
    ///     maxRetries: 3,
    ///     delayBetweenRetries: TimeSpan.FromSeconds(1));
    /// </code>
    /// </example>
    public static Task<List<TResult>> SelectParallelWithResultRetryAsync<TSource, TResult>(
        this IEnumerable<TSource> source,
        Func<TSource, CancellationToken, ValueTask<TResult>> selector,
        Func<TResult, bool> shouldRetry,
        int maxRetries = 3,
        TimeSpan? delayBetweenRetries = null,
        ParallelOptionsRivulet? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(shouldRetry);

        if (maxRetries < 0) throw new ArgumentOutOfRangeException(nameof(maxRetries), "Must be non-negative");

        var delay = delayBetweenRetries ?? TimeSpan.FromMilliseconds(100);

        var pipeline = new ResiliencePipelineBuilder<TResult>()
            .AddRetry(new()
            {
                MaxRetryAttempts = maxRetries,
                ShouldHandle = new PredicateBuilder<TResult>()
                    .Handle<Exception>()
                    .HandleResult(shouldRetry),
                Delay = delay,
                BackoffType = DelayBackoffType.Constant
            })
            .Build();

        return source.SelectParallelAsync((item, ct) =>
            {
                return pipeline.ExecuteAsync(token => selector(item, token),
                    ct);
            },
            options,
            cancellationToken);
    }

    /// <summary>
    ///     Processes items in parallel with exponential result-based retry.
    /// </summary>
    /// <typeparam name="TSource">The type of source items.</typeparam>
    /// <typeparam name="TResult">The type of result items.</typeparam>
    /// <param name="source">The source items to process.</param>
    /// <param name="selector">The transformation function.</param>
    /// <param name="shouldRetry">Predicate to determine if a result should trigger a retry.</param>
    /// <param name="maxRetries">Maximum number of retry attempts (default: 3).</param>
    /// <param name="baseDelay">Base delay for exponential backoff (default: 100ms).</param>
    /// <param name="options">Optional parallel processing options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The results of the parallel operation with exponential result-based retry applied.</returns>
    /// <example>
    ///     <code>
    /// // Retry with exponential backoff if result is null
    /// var results = await ids.SelectParallelWithExponentialResultRetryAsync(
    ///     async (id, ct) => await FetchDataAsync(id, ct),
    ///     shouldRetry: data => data == null,
    ///     maxRetries: 3,
    ///     baseDelay: TimeSpan.FromMilliseconds(100));
    /// </code>
    /// </example>
    public static Task<List<TResult>> SelectParallelWithExponentialResultRetryAsync<TSource, TResult>(
        this IEnumerable<TSource> source,
        Func<TSource, CancellationToken, ValueTask<TResult>> selector,
        Func<TResult, bool> shouldRetry,
        int maxRetries = 3,
        TimeSpan? baseDelay = null,
        ParallelOptionsRivulet? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(shouldRetry);

        if (maxRetries < 0) throw new ArgumentOutOfRangeException(nameof(maxRetries), "Must be non-negative");

        var delay = baseDelay ?? TimeSpan.FromMilliseconds(100);

        var pipeline = new ResiliencePipelineBuilder<TResult>()
            .AddRetry(new()
            {
                MaxRetryAttempts = maxRetries,
                ShouldHandle = new PredicateBuilder<TResult>()
                    .Handle<Exception>()
                    .HandleResult(shouldRetry),
                Delay = delay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            })
            .Build();

        return source.SelectParallelAsync((item, ct) =>
            {
                return pipeline.ExecuteAsync(token => selector(item, token),
                    ct);
            },
            options,
            cancellationToken);
    }
}