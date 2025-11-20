using Rivulet.Core.Resilience;

namespace Rivulet.RetryPolicies;

/// <summary>
/// Fluent builder for constructing retry policies.
/// </summary>
/// <typeparam name="TResult">The type of result returned by operations.</typeparam>
public sealed class PolicyBuilder<TResult>
{
    /// <summary>
    /// Creates a retry policy builder.
    /// </summary>
    /// <returns>A new retry policy builder.</returns>
    public static RetryPolicyBuilder Retry() => new();

    /// <summary>
    /// Creates a timeout policy builder.
    /// </summary>
    /// <param name="timeout">The timeout duration.</param>
    /// <returns>A new timeout policy.</returns>
    public static TimeoutPolicyBuilder Timeout(TimeSpan timeout) => new(timeout);

    /// <summary>
    /// Creates a fallback policy builder.
    /// </summary>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>A new fallback policy builder.</returns>
    public static FallbackPolicyBuilder Fallback(TResult fallbackValue) => new(fallbackValue);

    /// <summary>
    /// Creates a fallback policy builder with a dynamic fallback function.
    /// </summary>
    /// <param name="fallbackFunc">Function to compute the fallback value.</param>
    /// <returns>A new fallback policy builder.</returns>
    public static FallbackPolicyBuilder Fallback(Func<Exception, CancellationToken, ValueTask<TResult>> fallbackFunc)
        => new(fallbackFunc);

    /// <summary>
    /// Builder for retry policies.
    /// </summary>
    public sealed class RetryPolicyBuilder
    {
        private int _maxRetries = 3;
        private TimeSpan _baseDelay = TimeSpan.FromMilliseconds(100);
        private BackoffStrategy _backoffStrategy = BackoffStrategy.ExponentialJitter;
        private Func<Exception, bool>? _shouldRetry;
        private Func<int, Exception, ValueTask>? _onRetry;

        /// <summary>
        /// Sets the maximum number of retry attempts.
        /// </summary>
        public RetryPolicyBuilder WithMaxRetries(int maxRetries)
        {
            if (maxRetries < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetries));

            _maxRetries = maxRetries;
            return this;
        }

        /// <summary>
        /// Sets the base delay between retries.
        /// </summary>
        public RetryPolicyBuilder WithBaseDelay(TimeSpan baseDelay)
        {
            if (baseDelay < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(baseDelay));

            _baseDelay = baseDelay;
            return this;
        }

        /// <summary>
        /// Sets the backoff strategy.
        /// </summary>
        public RetryPolicyBuilder WithBackoffStrategy(BackoffStrategy strategy)
        {
            _backoffStrategy = strategy;
            return this;
        }

        /// <summary>
        /// Sets a predicate to determine which exceptions should trigger retries.
        /// </summary>
        public RetryPolicyBuilder Handle<TException>(Func<TException, bool>? predicate = null)
            where TException : Exception
        {
            _shouldRetry = ex => ex is TException tEx && (predicate?.Invoke(tEx) ?? true);
            return this;
        }

        /// <summary>
        /// Sets a predicate to determine which exceptions should trigger retries.
        /// </summary>
        public RetryPolicyBuilder HandleIf(Func<Exception, bool> predicate)
        {
            _shouldRetry = predicate ?? throw new ArgumentNullException(nameof(predicate));
            return this;
        }

        /// <summary>
        /// Sets a callback to invoke on each retry.
        /// </summary>
        public RetryPolicyBuilder OnRetry(Func<int, Exception, ValueTask> callback)
        {
            _onRetry = callback ?? throw new ArgumentNullException(nameof(callback));
            return this;
        }

        /// <summary>
        /// Builds the retry policy.
        /// </summary>
        public IPolicy<TResult> Build()
        {
            return new RetryPolicy<TResult>(_maxRetries, _baseDelay, _backoffStrategy, _shouldRetry, _onRetry);
        }
    }

    /// <summary>
    /// Builder for timeout policies.
    /// </summary>
    public sealed class TimeoutPolicyBuilder
    {
        private readonly TimeSpan _timeout;
        private Func<TimeSpan, ValueTask>? _onTimeout;

        internal TimeoutPolicyBuilder(TimeSpan timeout)
        {
            if (timeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout));

            _timeout = timeout;
        }

        /// <summary>
        /// Sets a callback to invoke when a timeout occurs.
        /// </summary>
        public TimeoutPolicyBuilder OnTimeout(Func<TimeSpan, ValueTask> callback)
        {
            _onTimeout = callback ?? throw new ArgumentNullException(nameof(callback));
            return this;
        }

        /// <summary>
        /// Builds the timeout policy.
        /// </summary>
        public IPolicy<TResult> Build()
        {
            return new TimeoutPolicy<TResult>(_timeout, _onTimeout);
        }
    }

    /// <summary>
    /// Builder for fallback policies.
    /// </summary>
    public sealed class FallbackPolicyBuilder
    {
        private readonly TResult? _fallbackValue;
        private readonly Func<Exception, CancellationToken, ValueTask<TResult>>? _fallbackFunc;
        private Func<Exception, bool>? _shouldFallback;
        private Func<Exception, ValueTask>? _onFallback;
        private readonly bool _isStatic;

        internal FallbackPolicyBuilder(TResult fallbackValue)
        {
            _fallbackValue = fallbackValue;
            _isStatic = true;
        }

        internal FallbackPolicyBuilder(Func<Exception, CancellationToken, ValueTask<TResult>> fallbackFunc)
        {
            _fallbackFunc = fallbackFunc ?? throw new ArgumentNullException(nameof(fallbackFunc));
            _isStatic = false;
        }

        /// <summary>
        /// Sets a predicate to determine which exceptions should trigger the fallback.
        /// </summary>
        public FallbackPolicyBuilder Handle<TException>(Func<TException, bool>? predicate = null)
            where TException : Exception
        {
            _shouldFallback = ex => ex is TException tEx && (predicate?.Invoke(tEx) ?? true);
            return this;
        }

        /// <summary>
        /// Sets a callback to invoke when fallback is triggered.
        /// </summary>
        public FallbackPolicyBuilder OnFallback(Func<Exception, ValueTask> callback)
        {
            _onFallback = callback ?? throw new ArgumentNullException(nameof(callback));
            return this;
        }

        /// <summary>
        /// Builds the fallback policy.
        /// </summary>
        public IPolicy<TResult> Build() => new FallbackPolicy<TResult>(
            _isStatic
                ? (_, _) => ValueTask.FromResult(_fallbackValue!)
                : _fallbackFunc!,
            _shouldFallback, _onFallback);
    }
}
