namespace Rivulet.Core.Resilience;

/// <summary>
///     Defines the strategy for calculating retry delays between failed attempts.
/// </summary>
public enum BackoffStrategy
{
    /// <summary>
    ///     Exponential backoff without jitter. Delay = BaseDelay * 2^(attempt - 1).
    ///     This is the default strategy and maintains backward compatibility.
    /// </summary>
    /// <remarks>
    ///     Example with BaseDelay=100ms:
    ///     - Attempt 1: 100ms
    ///     - Attempt 2: 200ms
    ///     - Attempt 3: 400ms
    ///     - Attempt 4: 800ms
    /// </remarks>
    Exponential = 0,

    /// <summary>
    ///     Exponential backoff with full jitter. Delay = Random(0, BaseDelay * 2^(attempt - 1)).
    ///     Reduces thundering herd effect by randomizing retry delays across all concurrent operations.
    /// </summary>
    /// <remarks>
    ///     This strategy is recommended for rate-limited or flaky services where synchronized retries
    ///     can amplify load. The random distribution spreads retry attempts over time.
    ///     Example with BaseDelay=100ms:
    ///     - Attempt 1: Random(0, 100ms)
    ///     - Attempt 2: Random(0, 200ms)
    ///     - Attempt 3: Random(0, 400ms)
    ///     - Attempt 4: Random(0, 800ms)
    /// </remarks>
    ExponentialJitter = 1,

    /// <summary>
    ///     Exponential backoff with decorrelated jitter. Each delay is based on the previous delay
    ///     with randomization to prevent synchronization: Delay = Random(BaseDelay, PreviousDelay * 3).
    /// </summary>
    /// <remarks>
    ///     This strategy provides better distribution than full jitter while maintaining exponential growth.
    ///     It's particularly effective when multiple clients are retrying simultaneously.
    ///     The decorrelated approach prevents clients from synchronizing on the same backoff pattern.
    /// </remarks>
    DecorrelatedJitter = 2,

    /// <summary>
    ///     Linear backoff without jitter. Delay = BaseDelay * attempt.
    ///     Provides a gentler increase in delay compared to exponential strategies.
    /// </summary>
    /// <remarks>
    ///     Use this when you want predictable, linearly increasing delays.
    ///     Example with BaseDelay=100ms:
    ///     - Attempt 1: 100ms
    ///     - Attempt 2: 200ms
    ///     - Attempt 3: 300ms
    ///     - Attempt 4: 400ms
    /// </remarks>
    Linear = 3,

    /// <summary>
    ///     Linear backoff with jitter. Delay = Random(0, BaseDelay * attempt).
    ///     Combines linear growth with randomization to reduce synchronized retries.
    /// </summary>
    /// <remarks>
    ///     Example with BaseDelay=100ms:
    ///     - Attempt 1: Random(0, 100ms)
    ///     - Attempt 2: Random(0, 200ms)
    ///     - Attempt 3: Random(0, 300ms)
    ///     - Attempt 4: Random(0, 400ms)
    /// </remarks>
    LinearJitter = 4
}