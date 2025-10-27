namespace Rivulet.Core.Resilience;

/// <summary>
/// Configuration options for rate limiting using the token bucket algorithm.
/// Controls the maximum rate at which operations can be executed.
/// </summary>
/// <remarks>
/// Token bucket algorithm allows for controlled bursts while maintaining
/// an average rate limit. Tokens are added to the bucket at a steady rate,
/// and each operation consumes one or more tokens.
/// </remarks>
public sealed class RateLimitOptions
{
    /// <summary>
    /// Gets the number of tokens added to the bucket per second.
    /// This determines the sustained rate limit for operations.
    /// </summary>
    /// <example>
    /// <code>
    /// // Allow 100 operations per second on average
    /// TokensPerSecond = 100
    /// </code>
    /// </example>
    public double TokensPerSecond { get; init; } = 100;

    /// <summary>
    /// Gets the maximum number of tokens the bucket can hold.
    /// This determines the maximum burst size for operations.
    /// </summary>
    /// <remarks>
    /// A larger burst size allows for brief spikes in activity while still
    /// maintaining the overall rate limit. Default is equal to TokensPerSecond,
    /// allowing a 1-second burst.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Allow bursts up to 200 operations
    /// BurstCapacity = 200
    /// </code>
    /// </example>
    public double BurstCapacity { get; init; } = 100;

    /// <summary>
    /// Gets the number of tokens consumed per operation.
    /// Default is 1 token per operation.
    /// </summary>
    /// <remarks>
    /// Can be used to give different weights to different operations.
    /// For example, heavy operations might consume more tokens.
    /// </remarks>
    public double TokensPerOperation { get; init; } = 1.0;

    /// <summary>
    /// Validates the rate limit options.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when configuration is invalid.</exception>
    internal void Validate()
    {
        if (TokensPerSecond <= 0)
            throw new ArgumentException("TokensPerSecond must be greater than 0.", nameof(TokensPerSecond));

        if (BurstCapacity <= 0)
            throw new ArgumentException("BurstCapacity must be greater than 0.", nameof(BurstCapacity));

        if (TokensPerOperation <= 0)
            throw new ArgumentException("TokensPerOperation must be greater than 0.", nameof(TokensPerOperation));

        if (BurstCapacity < TokensPerOperation)
            throw new ArgumentException("BurstCapacity must be at least TokensPerOperation.", nameof(BurstCapacity));
    }
}
