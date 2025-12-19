using System.Diagnostics;
using Rivulet.Core.Internal;

namespace Rivulet.Core.Resilience;

/// <summary>
///     Thread-safe implementation of the token bucket algorithm for rate limiting.
///     Tokens are added to the bucket at a fixed rate, and operations consume tokens.
/// </summary>
internal sealed class TokenBucket
{
    private readonly RateLimitOptions _options;
    private readonly Stopwatch _stopwatch;
    private readonly object _lock = LockFactory.CreateLock();

    private double _availableTokens;
    private long _lastRefillTicks;

    /// <summary>
    ///     Initializes a new instance of the TokenBucket class.
    /// </summary>
    /// <param name="options">Rate limit configuration options.</param>
    public TokenBucket(RateLimitOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();

        _availableTokens = options.BurstCapacity;
        _stopwatch = Stopwatch.StartNew();
        _lastRefillTicks = _stopwatch.ElapsedTicks;
    }

    /// <summary>
    ///     Acquires tokens from the bucket, waiting asynchronously if necessary.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when tokens are acquired.</returns>
    public async ValueTask AcquireAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (TryAcquire()) return;

            var delayMs = CalculateDelayUntilNextToken();

            await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Attempts to acquire tokens from the bucket without waiting.
    /// </summary>
    /// <returns>True if tokens were acquired; otherwise, false.</returns>
    internal bool TryAcquire() =>
        LockHelper.Execute(_lock,
            () =>
            {
                RefillTokens();

                if (_availableTokens < _options.TokensPerOperation) return false;

                _availableTokens -= _options.TokensPerOperation;

                return true;
            });

    /// <summary>
    ///     Refills tokens based on elapsed time since last refill.
    ///     Must be called while holding the lock.
    /// </summary>
    private void RefillTokens()
    {
        var currentTicks = _stopwatch.ElapsedTicks;
        var elapsedTicks = currentTicks - _lastRefillTicks;

        if (elapsedTicks <= 0) return;

        var elapsedSeconds = (double)elapsedTicks / Stopwatch.Frequency;
        var tokensToAdd = elapsedSeconds * _options.TokensPerSecond;

        _availableTokens = Math.Min(_availableTokens + tokensToAdd, _options.BurstCapacity);
        _lastRefillTicks = currentTicks;
    }

    /// <summary>
    ///     Calculates the delay in milliseconds until the next token becomes available.
    /// </summary>
    /// <returns>Delay in milliseconds.</returns>
    private int CalculateDelayUntilNextToken() =>
        LockHelper.Execute(_lock,
            () =>
            {
                RefillTokens();

                var tokensNeeded = _options.TokensPerOperation - _availableTokens;

                if (tokensNeeded <= 0) return 0;

                var secondsNeeded = tokensNeeded / _options.TokensPerSecond;
                var millisecondsNeeded = (int)Math.Ceiling(secondsNeeded * 1000);

                return Math.Max(millisecondsNeeded, 1);
            });

    /// <summary>
    ///     Gets the current number of available tokens (for testing/diagnostics).
    /// </summary>
    internal double GetAvailableTokens() =>
        LockHelper.Execute(_lock,
            () =>
            {
                RefillTokens();
                return _availableTokens;
            });
}