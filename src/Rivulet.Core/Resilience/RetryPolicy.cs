using Rivulet.Core.Observability;

namespace Rivulet.Core.Resilience;

internal static class RetryPolicy
{
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
            catch (Exception ex) when (options.OnFallback is not null)
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
