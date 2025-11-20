using Rivulet.Core.Resilience;

namespace Rivulet.RetryPolicies;

/// <summary>
/// A retry policy that retries failed operations with configurable backoff strategies.
/// </summary>
/// <typeparam name="TResult">The type of result returned by operations.</typeparam>
public sealed class RetryPolicy<TResult> : IPolicy<TResult>
{
    private readonly int _maxRetries;
    private readonly TimeSpan _baseDelay;
    private readonly BackoffStrategy _backoffStrategy;
    private readonly Func<Exception, bool>? _shouldRetry;
    private readonly Func<int, Exception, ValueTask>? _onRetry;

    /// <summary>
    /// Creates a new retry policy.
    /// </summary>
    /// <param name="maxRetries">Maximum number of retry attempts.</param>
    /// <param name="baseDelay">Base delay between retries.</param>
    /// <param name="backoffStrategy">Strategy for calculating retry delays.</param>
    /// <param name="shouldRetry">Optional predicate to determine if an exception should trigger a retry.</param>
    /// <param name="onRetry">Optional callback invoked on each retry.</param>
    public RetryPolicy(
        int maxRetries = 3,
        TimeSpan? baseDelay = null,
        BackoffStrategy backoffStrategy = BackoffStrategy.ExponentialJitter,
        Func<Exception, bool>? shouldRetry = null,
        Func<int, Exception, ValueTask>? onRetry = null)
    {
        if (maxRetries < 0)
            throw new ArgumentOutOfRangeException(nameof(maxRetries), "Max retries must be non-negative");

        _maxRetries = maxRetries;
        _baseDelay = baseDelay ?? TimeSpan.FromMilliseconds(100);
        _backoffStrategy = backoffStrategy;
        _shouldRetry = shouldRetry ?? (_ => true);
        _onRetry = onRetry;
    }

    /// <inheritdoc/>
    public async ValueTask<TResult> ExecuteAsync(
        Func<CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var attempt = 0;
        var previousDelay = TimeSpan.Zero;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await operation(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < _maxRetries && (_shouldRetry?.Invoke(ex) ?? true))
            {
                attempt++;

                if (_onRetry != null)
                {
                    await _onRetry(attempt, ex).ConfigureAwait(false);
                }

                var delay = CalculateDelay(_backoffStrategy, _baseDelay, attempt, ref previousDelay);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc/>
    public IPolicy<TResult> Wrap(IPolicy<TResult> outerPolicy)
    {
        ArgumentNullException.ThrowIfNull(outerPolicy);
        return new CompositePolicy<TResult>(outerPolicy, this);
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
            BackoffStrategy.Exponential => TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt - 1)),
            BackoffStrategy.ExponentialJitter => TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * baseDelayMs * Math.Pow(2, attempt - 1)),
            BackoffStrategy.DecorrelatedJitter => CalculateDecorrelatedJitter(baseDelayMs, attempt, ref previousDelay),
            BackoffStrategy.Linear => TimeSpan.FromMilliseconds(baseDelayMs * attempt),
            BackoffStrategy.LinearJitter => TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * baseDelayMs * attempt),
            _ => TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt - 1))
        };
    }

    private static TimeSpan CalculateDecorrelatedJitter(double baseDelayMs, int attempt, ref TimeSpan previousDelay)
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
}
