using System.Diagnostics.CodeAnalysis;
using System.Net;
using Rivulet.Core;

namespace Rivulet.Http;

/// <summary>
///     Configuration options specific to HTTP operations with parallel processing.
///     Extends <see cref="ParallelOptionsRivulet" /> with HTTP-specific retry policies and resilience features.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
public sealed class HttpOptions
{
    internal const int DefaultRetryCount = 3;

    /// <summary>
    ///     Gets the parallel processing options for controlling concurrency, retries, and error handling.
    ///     If null, defaults are used.
    /// </summary>
    public ParallelOptionsRivulet? ParallelOptions { get; init; }

    /// <summary>
    ///     Gets the timeout for each HTTP request.
    ///     If null, uses the HttpClient's default timeout. Defaults to 30 seconds.
    /// </summary>
    public TimeSpan? RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Gets the set of HTTP status codes that should be treated as transient errors and retried.
    ///     Defaults to common transient HTTP errors (408, 429, 500, 502, 503, 504).
    /// </summary>
    public HashSet<HttpStatusCode> RetriableStatusCodes { get; init; } =
    [
        HttpStatusCode.RequestTimeout,      // 408
        HttpStatusCode.TooManyRequests,     // 429
        HttpStatusCode.InternalServerError, // 500
        HttpStatusCode.BadGateway,          // 502
        HttpStatusCode.ServiceUnavailable,  // 503
        HttpStatusCode.GatewayTimeout
    ];

    /// <summary>
    ///     Gets a value indicating whether to automatically respect Retry-After headers from HTTP 429 and 503 responses.
    ///     When true, the retry delay will use the server-specified Retry-After value instead of the configured backoff
    ///     strategy.
    ///     Defaults to true.
    /// </summary>
    public bool RespectRetryAfterHeader { get; init; } = true;

    /// <summary>
    ///     Gets the maximum buffer size for HTTP content reads.
    ///     Defaults to 81920 bytes (80 KB).
    /// </summary>
    public int BufferSize { get; init; } = 81920;

    /// <summary>
    ///     Gets a value indicating whether to follow HTTP redirects automatically.
    ///     Defaults to true.
    /// </summary>
    public bool FollowRedirects { get; init; } = true;

    /// <summary>
    ///     Gets the maximum number of HTTP redirects to follow.
    ///     Defaults to 50 (same as HttpClientHandler default).
    /// </summary>
    public int MaxRedirects { get; init; } = 50;

    /// <summary>
    ///     Gets a callback invoked when an HTTP-specific error occurs.
    ///     Receives the request URI, the HTTP status code (if available), and the exception.
    /// </summary>
    public Func<Uri, HttpStatusCode?, Exception, ValueTask>? OnHttpErrorAsync { get; init; }

    /// <summary>
    ///     Gets a callback invoked when an HTTP redirect is encountered.
    ///     Receives the original URI and the redirect target URI.
    /// </summary>
    public Func<Uri, Uri, ValueTask>? OnRedirectAsync { get; init; }

    /// <summary>
    ///     Creates a merged <see cref="ParallelOptionsRivulet" /> that combines the specified parallel options
    ///     with HTTP-specific transient error handling.
    /// </summary>
    /// <returns>A configured ParallelOptionsRivulet instance with HTTP-aware retry logic.</returns>
    internal ParallelOptionsRivulet GetMergedParallelOptions()
    {
        var baseOptions = ParallelOptions ?? new ParallelOptionsRivulet();

        return new()
        {
            MaxDegreeOfParallelism = baseOptions.MaxDegreeOfParallelism,
            PerItemTimeout = RequestTimeout ?? baseOptions.PerItemTimeout,
            ErrorMode = baseOptions.ErrorMode,
            OnErrorAsync = baseOptions.OnErrorAsync,
            OnStartItemAsync = baseOptions.OnStartItemAsync,
            OnCompleteItemAsync = baseOptions.OnCompleteItemAsync,
            OnRetryAsync = baseOptions.OnRetryAsync,
            OnThrottleAsync = baseOptions.OnThrottleAsync,
            OnDrainAsync = baseOptions.OnDrainAsync,
            IsTransient = HttpIsTransient,
            MaxRetries = baseOptions.MaxRetries > 0 ? baseOptions.MaxRetries : DefaultRetryCount,
            BaseDelay = baseOptions.BaseDelay,
            BackoffStrategy = baseOptions.BackoffStrategy,
            ChannelCapacity = baseOptions.ChannelCapacity,
            OrderedOutput = baseOptions.OrderedOutput,
            Progress = baseOptions.Progress,
            Metrics = baseOptions.Metrics,
            RateLimit = baseOptions.RateLimit,
            CircuitBreaker = baseOptions.CircuitBreaker,
            AdaptiveConcurrency = baseOptions.AdaptiveConcurrency
        };

        bool HttpIsTransient(Exception ex) =>
            baseOptions.IsTransient?.Invoke(ex) == true || ex switch
            {
                HttpRequestException { StatusCode: not null } httpEx => RetriableStatusCodes.Contains(httpEx.StatusCode
                    .Value),
                TaskCanceledException => true,
                _ => false
            };
    }
}