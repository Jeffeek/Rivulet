using System.Diagnostics.CodeAnalysis;

namespace Rivulet.Core.Resilience;

/// <summary>
///     Provides backoff delay calculations for retry strategies.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
public static class BackoffCalculator
{
    /// <summary>
    ///     Calculates the delay for a retry attempt based on the specified backoff strategy.
    /// </summary>
    /// <param name="strategy">The backoff strategy to use.</param>
    /// <param name="baseDelay">The base delay for calculations.</param>
    /// <param name="attempt">The retry attempt number (1-based).</param>
    /// <param name="previousDelay">Reference to the previous delay (used for decorrelated jitter).</param>
    /// <returns>The calculated delay for this retry attempt.</returns>
    public static TimeSpan CalculateDelay(
        BackoffStrategy strategy,
        TimeSpan baseDelay,
        int attempt,
        ref TimeSpan previousDelay)
    {
        var baseDelayMs = baseDelay.TotalMilliseconds;

        return strategy switch
        {
            BackoffStrategy.Exponential => CalculateExponential(baseDelayMs, attempt),
            BackoffStrategy.ExponentialJitter => CalculateExponentialJitter(baseDelayMs, attempt),
            BackoffStrategy.DecorrelatedJitter => CalculateDecorrelatedJitter(baseDelayMs, attempt, ref previousDelay),
            BackoffStrategy.Linear => CalculateLinear(baseDelayMs, attempt),
            BackoffStrategy.LinearJitter => CalculateLinearJitter(baseDelayMs, attempt),
            _ => CalculateExponential(baseDelayMs, attempt)
        };
    }

    /// <summary>
    ///     Calculates exponential backoff delay: BaseDelay * 2^(attempt - 1).
    /// </summary>
    private static TimeSpan CalculateExponential(double baseDelayMs, int attempt)
    {
        var delayMs = baseDelayMs * Math.Pow(2, attempt - 1);
        return TimeSpan.FromMilliseconds(delayMs);
    }

    /// <summary>
    ///     Calculates exponential backoff with full jitter: Random(0, BaseDelay * 2^(attempt - 1)).
    /// </summary>
    private static TimeSpan CalculateExponentialJitter(double baseDelayMs, int attempt)
    {
        var maxDelayMs = baseDelayMs * Math.Pow(2, attempt - 1);
        var jitteredDelayMs = Random.Shared.NextDouble() * maxDelayMs;
        return TimeSpan.FromMilliseconds(jitteredDelayMs);
    }

    /// <summary>
    ///     Calculates decorrelated jitter delay: Random(BaseDelay, PreviousDelay * 3).
    /// </summary>
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

    /// <summary>
    ///     Calculates linear backoff delay: BaseDelay * attempt.
    /// </summary>
    private static TimeSpan CalculateLinear(double baseDelayMs, int attempt)
    {
        var delayMs = baseDelayMs * attempt;
        return TimeSpan.FromMilliseconds(delayMs);
    }

    /// <summary>
    ///     Calculates linear backoff with jitter: Random(0, BaseDelay * attempt).
    /// </summary>
    private static TimeSpan CalculateLinearJitter(double baseDelayMs, int attempt)
    {
        var maxDelayMs = baseDelayMs * attempt;
        var jitteredDelayMs = Random.Shared.NextDouble() * maxDelayMs;
        return TimeSpan.FromMilliseconds(jitteredDelayMs);
    }
}