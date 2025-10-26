using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Rivulet.Core;

/// <summary>
/// Thread-safe implementation of the token bucket algorithm for rate limiting.
/// Tokens are added to the bucket at a fixed rate, and operations consume tokens.
/// </summary>
[SuppressMessage("ReSharper", "InconsistentlySynchronizedField")]
internal sealed class TokenBucket
{
    private readonly RateLimitOptions _options;
    private readonly Stopwatch _stopwatch;
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    private double _availableTokens;
    private long _lastRefillTicks;

    /// <summary>
    /// Initializes a new instance of the TokenBucket class.
    /// </summary>
    /// <param name="options">Rate limit configuration options.</param>
    public TokenBucket(RateLimitOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();

        _availableTokens = options.BurstCapacity; // Start with full bucket
        _stopwatch = Stopwatch.StartNew();
        _lastRefillTicks = _stopwatch.ElapsedTicks;
    }

    /// <summary>
    /// Acquires tokens from the bucket, waiting asynchronously if necessary.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when tokens are acquired.</returns>
    public async ValueTask AcquireAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

#if NET9_0_OR_GREATER
            _lock.Enter();
            RefillTokens();

            if (_availableTokens >= _options.TokensPerOperation)
            {
                _availableTokens -= _options.TokensPerOperation;
                _lock.Exit();
                return;
            }

            _lock.Exit();
#else
            lock (_lock)
            {
                RefillTokens();

                if (_availableTokens >= _options.TokensPerOperation)
                {
                    _availableTokens -= _options.TokensPerOperation;
                    return;
                }
            }
#endif

            var delayMs = CalculateDelayUntilNextToken();

            await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Attempts to acquire tokens from the bucket without waiting.
    /// </summary>
    /// <returns>True if tokens were acquired; otherwise, false.</returns>
    public bool TryAcquire()
    {
#if NET9_0_OR_GREATER
        _lock.Enter();
        RefillTokens();

        if (!(_availableTokens >= _options.TokensPerOperation))
        {
            _lock.Exit();
            return false;
        }

        _availableTokens -= _options.TokensPerOperation;
        _lock.Exit();

        return true;
#else
        lock (_lock)
        {
            RefillTokens();

            if (!(_availableTokens >= _options.TokensPerOperation)) return false;
            _availableTokens -= _options.TokensPerOperation;

            return true;
        }
#endif
    }

    /// <summary>
    /// Refills tokens based on elapsed time since last refill.
    /// Must be called while holding the lock.
    /// </summary>
    private void RefillTokens()
    {
        var currentTicks = _stopwatch.ElapsedTicks;
        var elapsedTicks = currentTicks - _lastRefillTicks;

        if (elapsedTicks <= 0)
            return;

        var elapsedSeconds = (double)elapsedTicks / Stopwatch.Frequency;
        var tokensToAdd = elapsedSeconds * _options.TokensPerSecond;

        _availableTokens = Math.Min(_availableTokens + tokensToAdd, _options.BurstCapacity);
        _lastRefillTicks = currentTicks;
    }

    /// <summary>
    /// Calculates the delay in milliseconds until the next token becomes available.
    /// </summary>
    /// <returns>Delay in milliseconds.</returns>
    private int CalculateDelayUntilNextToken()
    {
#if NET9_0_OR_GREATER
        _lock.Enter();
        RefillTokens();

        var tokensNeeded = _options.TokensPerOperation - _availableTokens;

        if (tokensNeeded <= 0)
        {
            _lock.Exit();
            return 0;
        }

        var secondsNeeded = tokensNeeded / _options.TokensPerSecond;
        var millisecondsNeeded = (int)Math.Ceiling(secondsNeeded * 1000);
        _lock.Exit();

        return Math.Max(millisecondsNeeded, 1);

#else
        lock (_lock)
        {
            RefillTokens();

            var tokensNeeded = _options.TokensPerOperation - _availableTokens;

            if (tokensNeeded <= 0)
                return 0;

            var secondsNeeded = tokensNeeded / _options.TokensPerSecond;
            var millisecondsNeeded = (int)Math.Ceiling(secondsNeeded * 1000);

            return Math.Max(millisecondsNeeded, 1);
        }
#endif
    }

    /// <summary>
    /// Gets the current number of available tokens (for testing/diagnostics).
    /// </summary>
    internal double GetAvailableTokens()
    {
#if NET9_0_OR_GREATER
        _lock.Enter();
        RefillTokens();
        var tokens = _availableTokens;
        _lock.Exit();

        return tokens;

#else
        lock (_lock)
        {
            RefillTokens();
            return _availableTokens;
        }
#endif
    }
}
