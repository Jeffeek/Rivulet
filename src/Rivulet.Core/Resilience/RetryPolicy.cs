using Rivulet.Core.Observability;

namespace Rivulet.Core.Resilience;

internal static class RetryPolicy
{
    /// <summary>
    /// Execute a function for a single item applying per-item timeout, retry with backoff for transient errors, and an optional fallback.
    /// </summary>
    /// <param name="item">The input item passed to <paramref name="func"/>.</param>
    /// <param name="func">The operation to execute for the item; receives the item and a cancellation token.</param>
    /// <param name="options">Retry, timeout, backoff and callback configuration used to control retries and fallback behavior.</param>
    /// <param name="metricsTracker">Tracker used to record metrics such as retry counts.</param>
    /// <param name="itemIndex">Index of the item (forwarded to retry/fallback callbacks).</param>
    /// <param name="ct">Cancellation token that cancels the operation and any per-item timeout.</param>
    /// <returns>
    /// The result produced by <paramref name="func"/>, or a value produced by the configured fallback.
    /// If the fallback returns null and <typeparamref name="TResult"/> is a reference type, the method returns null (default).
    /// </returns>
    /// <exception cref="OperationCanceledException">Thrown if <paramref name="ct"/> is canceled before or during execution.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a configured fallback returns a value that cannot be converted to <typeparamref name="TResult"/>.</exception>
    public static async ValueTask<TResult> ExecuteWithRetry<T, TResult>(
        T item,
        Func<T, CancellationToken, ValueTask<TResult>> func,
        ParallelOptionsRivulet options,
        MetricsTrackerBase metricsTracker,
        int itemIndex,
        CancellationToken ct
    )
    {
        var attempt = 0;
        var previousDelay = TimeSpan.Zero;

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (options.PerItemTimeout is not { } t) return await func(item, ct).ConfigureAwait(false);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(t);
                return await func(item, cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < options.MaxRetries && (options.IsTransient?.Invoke(ex) ?? false))
            {
                attempt++;
                metricsTracker.IncrementRetries();

                if (options.OnRetryAsync is not null)
                    await options.OnRetryAsync(itemIndex, attempt, ex).ConfigureAwait(false);

                var delay = BackoffCalculator.CalculateDelay(options.BackoffStrategy,
                    options.BaseDelay,
                    attempt,
                    ref previousDelay);
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException && options.OnFallback is not null)
            {
                var fallbackValue = options.OnFallback(itemIndex, ex);
                return fallbackValue switch
                {
                    TResult result => result,
                    null when !typeof(TResult).IsValueType => default!,
                    _ => throw new InvalidOperationException(
#pragma warning disable CA1508
                        $"Fallback returned {fallbackValue?.GetType().Name ?? "null"}, expected {typeof(TResult).Name}")
#pragma warning restore CA1508
                };
            }
        }
    }
}
