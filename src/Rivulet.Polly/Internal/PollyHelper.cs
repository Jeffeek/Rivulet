using Polly;
using Polly.Retry;
using Rivulet.Core;
using Rivulet.Core.Resilience;

namespace Rivulet.Polly.Internal;

/// <summary>
///     Internal helper to reduce code duplication in Polly integrations.
/// </summary>
internal static class PollyHelper
{
    /// <summary>
    ///     Creates retry options with decorrelated jitter backoff strategy.
    /// </summary>
    public static RetryStrategyOptions CreateRetryOptions(
        ParallelOptionsRivulet options,
        ThreadLocal<TimeSpan> previousDelayLocal
    ) => new()
    {
        MaxRetryAttempts = options.MaxRetries,
        ShouldHandle = new PredicateBuilder().Handle<Exception>(ex => options.IsTransient?.Invoke(ex) ?? false),
        DelayGenerator = args =>
        {
            var prev = previousDelayLocal.Value;
            var delay = BackoffCalculator.CalculateDelay(
                options.BackoffStrategy,
                options.BaseDelay,
                args.AttemptNumber + 1,
                ref prev);
            previousDelayLocal.Value = prev;
            return new(delay);
        },
        OnRetry = args =>
        {
            if (args.AttemptNumber >= options.MaxRetries)
                previousDelayLocal.Value = TimeSpan.Zero;
            return default;
        }
    };
}
