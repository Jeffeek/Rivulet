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
    /// Calculates an exponential backoff delay capped at the configured maximum.
    /// Formula: BaseDelay * 2^(attempt - 1).
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
    ///     Calculates exponential backoff with full jitter: Random(0, BaseDelay * 2^(attempt - 1)).
    /// <summary>
    /// Compute an exponential backoff delay with full jitter, capped by the configured maximum delay.
    /// </summary>
    /// <param name="baseDelayMs">Base delay in milliseconds used as the multiplier for exponential growth.</param>
    /// <param name="attempt">1-based retry attempt number.</param>
    /// <returns>A TimeSpan representing a jittered delay in milliseconds between 0 and the smaller of baseDelayMs * 2^(attempt - 1) and the maximum allowed delay.</returns>
    private static TimeSpan CalculateExponentialJitter(double baseDelayMs, int attempt)
    {
        var maxDelayMs = Math.Min(baseDelayMs * Math.Pow(2, attempt - 1), MaxDelayMs);
        var jitteredDelayMs = Random.Shared.NextDouble() * maxDelayMs;
        return TimeSpan.FromMilliseconds(jitteredDelayMs);
    }

    /// <summary>
    ///     Calculates decorrelated jitter delay: Random(BaseDelay, max(BaseDelay, PreviousDelay * 3)).
    /// </summary>
    /// <param name="baseDelayMs">Base delay in milliseconds used as the minimum scale for the jitter.</param>
    /// <param name="attempt">One-based retry attempt number.</param>
    /// <param name="previousDelay">Reference to the previously computed delay; updated to the newly computed delay.</param>
    /// <summary>
    /// Calculates a decorrelated-jitter retry delay and updates the running previous delay to reduce retry synchronization.
    /// </summary>
    /// <param name="baseDelayMs">The base delay in milliseconds used as a minimum for the calculation.</param>
    /// <param name="attempt">The 1-based retry attempt number.</param>
    /// <param name="previousDelay">Reference to the previous delay; initialized to <paramref name="baseDelayMs"/> on the first attempt and updated with the computed delay for subsequent calls.</param>
    /// <returns>The computed delay as a <see cref="TimeSpan"/> for this retry attempt, capped to the configured maximum delay.</returns>
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
    ///     Calculates linear backoff delay: BaseDelay * attempt.
    /// <summary>
    /// Compute a linearly scaled retry delay and clamp it to the configured maximum.
    /// </summary>
    /// <param name="baseDelayMs">Base delay in milliseconds used as the unit for scaling.</param>
    /// <param name="attempt">1-based retry attempt number used to scale the base delay.</param>
    /// <returns>A <see cref="TimeSpan"/> equal to <c>baseDelayMs * attempt</c>, capped at the class maximum delay.</returns>
    private static TimeSpan CalculateLinear(double baseDelayMs, int attempt)
    {
        var delayMs = Math.Min(baseDelayMs * attempt, MaxDelayMs);
        return TimeSpan.FromMilliseconds(delayMs);
    }

    /// <summary>
    ///     Calculates linear backoff with jitter: Random(0, BaseDelay * attempt).
    /// <summary>
    /// Computes a linear backoff delay with jitter, capped by the global maximum delay.
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
