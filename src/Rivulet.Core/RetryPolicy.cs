namespace Rivulet.Core;

internal static class RetryPolicy
{
    public static async ValueTask<TResult> ExecuteWithRetry<T, TResult>(
        T item,
        int index,
        Func<T, CancellationToken, ValueTask<TResult>> func,
        ParallelOptionsRivulet options,
        CancellationToken ct)
    {
        var attempt = 0;
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
                var delay = TimeSpan.FromMilliseconds(options.BaseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }
    }
}