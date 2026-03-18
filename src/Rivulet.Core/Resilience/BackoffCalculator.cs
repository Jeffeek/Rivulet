using System.Diagnostics.CodeAnalysis;

namespace Rivulet.Core.Resilience;

/// <summary>
///     Provides backoff delay calculations for retry strategies.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
public static class BackoffCalculator
{
    private static readonly double MaxDelayMs = TimeSpan.FromMinutes(5).TotalMilliseconds;

    /// <summary>
    /// Computes the retry delay for a given attempt using the specified backoff strategy.
    /// </summary>
    /// <param name="strategy">The backoff algorithm to use.</param>
    /// <param name="baseDelay">The base delay used by the strategy.</param>
    /// <param name="attempt">The one-based retry attempt number.</param>
    /// <param name="previousDelay">Reference to the previous delay; updated by strategies that maintain state (for example, decorrelated jitter).</param>
    /// <returns>The computed delay for this retry attempt, clamped to the configured maximum delay.</returns>
    public static TimeSpan CalculateDelay(
        BackoffStrategy strategy,
        TimeSpan baseDelay,
        int attempt,
        ref TimeSpan previousDelay
    )
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
    /// Compute the exponential backoff delay for the specified retry attempt, clamped to the configured maximum.
    /// </summary>
    /// <param name="baseDelayMs">Base delay in milliseconds used as the exponential multiplier.</param>
    /// <param name="attempt">1-based retry attempt number (1 yields the base delay).</param>
    /// <returns>A TimeSpan equal to baseDelayMs * 2^(attempt - 1), clamped to the maximum allowed delay.</returns>
    private static TimeSpan CalculateExponential(double baseDelayMs, int attempt)
    {
        var delayMs = Math.Min(baseDelayMs * Math.Pow(2, attempt - 1), MaxDelayMs);
        return TimeSpan.FromMilliseconds(delayMs);
    }

    /// <summary>
    /// Computes an exponential backoff delay with uniform jitter.
    /// </summary>
    /// <param name="baseDelayMs">Base delay in milliseconds used as the exponentiation seed.</param>
    /// <param name="attempt">One-based retry attempt number.</param>
    /// <returns>A TimeSpan representing a delay between 0 and the smaller of baseDelayMs * 2^(attempt - 1) and the configured maximum delay.</returns>
    private static TimeSpan CalculateExponentialJitter(double baseDelayMs, int attempt)
    {
        var maxDelayMs = Math.Min(baseDelayMs * Math.Pow(2, attempt - 1), MaxDelayMs);
        var jitteredDelayMs = Random.Shared.NextDouble() * maxDelayMs;
        return TimeSpan.FromMilliseconds(jitteredDelayMs);
    }

    /// <summary>
    /// Computes a decorrelated jitter delay for the given retry attempt and updates <paramref name="previousDelay"/> with the chosen delay.
    /// </summary>
    /// <param name="baseDelayMs">The base delay in milliseconds used as the minimum delay and jitter baseline.</param>
    /// <param name="attempt">The 1-based retry attempt index.</param>
    /// <param name="previousDelay">Reference to the previously returned delay; on the first attempt or when zero, it is initialized to <paramref name="baseDelayMs"/>. This value is updated to the computed delay.</param>
    /// <returns>The chosen delay as a <see cref="TimeSpan"/>, constrained by the configured maximum delay.</returns>
    private static TimeSpan CalculateDecorrelatedJitter(double baseDelayMs, int attempt, ref TimeSpan previousDelay)
    {
        if (attempt == 1 || previousDelay == TimeSpan.Zero)
        {
            previousDelay = TimeSpan.FromMilliseconds(baseDelayMs);
            return previousDelay;
        }

        var maxDelayMs = Math.Min(Math.Max(baseDelayMs, previousDelay.TotalMilliseconds * 3), MaxDelayMs);
        var delayMs = baseDelayMs + (Random.Shared.NextDouble() * (maxDelayMs - baseDelayMs));

        previousDelay = TimeSpan.FromMilliseconds(delayMs);
        return previousDelay;
    }

    /// <summary>
    /// Compute the linear backoff delay as the base delay multiplied by the retry attempt, capped to the class maximum.
    /// </summary>
    /// <param name="baseDelayMs">Base delay in milliseconds.</param>
    /// <param name="attempt">Retry attempt number (1-based).</param>
    /// <returns>A <see cref="TimeSpan"/> equal to <c>baseDelayMs * attempt</c>, capped at the class maximum delay.</returns>
    private static TimeSpan CalculateLinear(double baseDelayMs, int attempt)
    {
        var delayMs = Math.Min(baseDelayMs * attempt, MaxDelayMs);
        return TimeSpan.FromMilliseconds(delayMs);
    }

    /// <summary>
    /// Computes a linear backoff delay with jitter, selecting a random value between 0 and the capped linear delay.
    /// </summary>
    /// <param name="baseDelayMs">Base delay in milliseconds used as the unit step for linear scaling.</param>
    /// <param name="attempt">1-based retry attempt number used to scale the maximum linear delay.</param>
    /// <returns>A TimeSpan representing a randomized delay between 0 and min(baseDelayMs * attempt, MaxDelayMs) milliseconds.</returns>
    private static TimeSpan CalculateLinearJitter(double baseDelayMs, int attempt)
    {
        var maxDelayMs = Math.Min(baseDelayMs * attempt, MaxDelayMs);
        var jitteredDelayMs = Random.Shared.NextDouble() * maxDelayMs;
        return TimeSpan.FromMilliseconds(jitteredDelayMs);
    }
}
