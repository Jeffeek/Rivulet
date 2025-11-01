using Rivulet.Core.Observability;

namespace Rivulet.Core.Resilience;

internal static class RetryPolicy
{
    public static async ValueTask<TResult> ExecuteWithRetry<T, TResult>(
        T item,
        Func<T, CancellationToken, ValueTask<TResult>> func,
        ParallelOptionsRivulet options,
        MetricsTracker? metricsTracker,
        int itemIndex,
        CancellationToken ct)
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
                metricsTracker?.IncrementRetries();

                if (options.OnRetryAsync is not null)
                {
                    await options.OnRetryAsync(itemIndex, attempt, ex).ConfigureAwait(false);
                }

                var delay = CalculateDelay(options.BackoffStrategy, options.BaseDelay, attempt, ref previousDelay);
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }
    }

    private static TimeSpan CalculateDelay(
        BackoffStrategy strategy,
        TimeSpan baseDelay,
        int attempt,
        ref TimeSpan previousDelay)
    {
        var baseDelayMs = baseDelay.TotalMilliseconds;

        return strategy switch
        {
            BackoffStrategy.Exponential => CalculateExponentialDelay(baseDelayMs, attempt),
            BackoffStrategy.ExponentialJitter => CalculateExponentialJitterDelay(baseDelayMs, attempt),
            BackoffStrategy.DecorrelatedJitter => CalculateDecorrelatedJitterDelay(baseDelayMs, attempt, ref previousDelay),
            BackoffStrategy.Linear => CalculateLinearDelay(baseDelayMs, attempt),
            BackoffStrategy.LinearJitter => CalculateLinearJitterDelay(baseDelayMs, attempt),
            _ => CalculateExponentialDelay(baseDelayMs, attempt)
        };
    }

    private static TimeSpan CalculateExponentialDelay(double baseDelayMs, int attempt)
    {
        var delayMs = baseDelayMs * Math.Pow(2, attempt - 1);
        return TimeSpan.FromMilliseconds(delayMs);
    }

    private static TimeSpan CalculateExponentialJitterDelay(double baseDelayMs, int attempt)
    {
        var maxDelayMs = baseDelayMs * Math.Pow(2, attempt - 1);
        var jitteredDelayMs = Random.Shared.NextDouble() * maxDelayMs;
        return TimeSpan.FromMilliseconds(jitteredDelayMs);
    }

    private static TimeSpan CalculateDecorrelatedJitterDelay(double baseDelayMs, int attempt, ref TimeSpan previousDelay)
    {
        if (attempt == 1 || previousDelay == TimeSpan.Zero)
        {
            var firstDelayMs = Random.Shared.NextDouble() * baseDelayMs;
            previousDelay = TimeSpan.FromMilliseconds(firstDelayMs);
            return previousDelay;
        }

        var maxDelayMs = previousDelay.TotalMilliseconds * 3;
        var delayMs = baseDelayMs + Random.Shared.NextDouble() * (maxDelayMs - baseDelayMs);

        previousDelay = TimeSpan.FromMilliseconds(delayMs);
        return previousDelay;
    }

    private static TimeSpan CalculateLinearDelay(double baseDelayMs, int attempt)
    {
        var delayMs = baseDelayMs * attempt;
        return TimeSpan.FromMilliseconds(delayMs);
    }

    private static TimeSpan CalculateLinearJitterDelay(double baseDelayMs, int attempt)
    {
        var maxDelayMs = baseDelayMs * attempt;
        var jitteredDelayMs = Random.Shared.NextDouble() * maxDelayMs;
        return TimeSpan.FromMilliseconds(jitteredDelayMs);
    }
}