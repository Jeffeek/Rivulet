using System.Net;
using Rivulet.Core;

namespace Rivulet.Http;

/// <summary>
/// Provides parallel HTTP operations with bounded concurrency, automatic retries, and resilience features.
/// </summary>
public static class HttpParallelExtensions
{
    /// <summary>
    /// Executes parallel HTTP GET requests for a collection of URIs with bounded concurrency and automatic retries.
    /// </summary>
    /// <param name="uris">The collection of URIs to fetch.</param>
    /// <param name="httpClient">The HttpClient instance to use for requests.</param>
    /// <param name="options">Configuration options for parallel execution and HTTP-specific resilience features. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing a list of HTTP response messages.</returns>
    /// <exception cref="ArgumentNullException">Thrown when uris or httpClient is null.</exception>
    /// <exception cref="AggregateException">Thrown when errors occur and ErrorMode is set to CollectAndContinue.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    public static async Task<List<HttpResponseMessage>> GetParallelAsync(
        this IEnumerable<Uri> uris,
        HttpClient httpClient,
        HttpOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uris);
        ArgumentNullException.ThrowIfNull(httpClient);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        return await uris.SelectParallelAsync(
            async (uri, ct) => await ExecuteHttpRequestAsync(
                uri,
                () => httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct),
                options,
                ct).ConfigureAwait(false),
            parallelOptions,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes parallel HTTP GET requests and returns the response content as strings.
    /// </summary>
    /// <param name="uris">The collection of URIs to fetch.</param>
    /// <param name="httpClient">The HttpClient instance to use for requests.</param>
    /// <param name="options">Configuration options for parallel execution and HTTP-specific resilience features. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing a list of response content strings.</returns>
    /// <exception cref="ArgumentNullException">Thrown when uris or httpClient is null.</exception>
    public static async Task<List<string>> GetStringParallelAsync(
        this IEnumerable<Uri> uris,
        HttpClient httpClient,
        HttpOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uris);
        ArgumentNullException.ThrowIfNull(httpClient);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        return await uris.SelectParallelAsync(
            async (uri, ct) =>
            {
                using var response = await ExecuteHttpRequestAsync(
                    uri,
                    () => httpClient.GetAsync(uri, HttpCompletionOption.ResponseContentRead, ct),
                    options,
                    ct).ConfigureAwait(false);

                return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            },
            parallelOptions,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes parallel HTTP GET requests and returns the response content as byte arrays.
    /// </summary>
    /// <param name="uris">The collection of URIs to fetch.</param>
    /// <param name="httpClient">The HttpClient instance to use for requests.</param>
    /// <param name="options">Configuration options for parallel execution and HTTP-specific resilience features. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing a list of byte arrays.</returns>
    /// <exception cref="ArgumentNullException">Thrown when uris or httpClient is null.</exception>
    public static async Task<List<byte[]>> GetByteArrayParallelAsync(
        this IEnumerable<Uri> uris,
        HttpClient httpClient,
        HttpOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uris);
        ArgumentNullException.ThrowIfNull(httpClient);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        return await uris.SelectParallelAsync(
            async (uri, ct) =>
            {
                using var response = await ExecuteHttpRequestAsync(
                    uri,
                    () => httpClient.GetAsync(uri, HttpCompletionOption.ResponseContentRead, ct),
                    options,
                    ct).ConfigureAwait(false);

                return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            },
            parallelOptions,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes parallel HTTP POST requests with specified content.
    /// </summary>
    /// <typeparam name="TContent">The type of content to send in POST requests.</typeparam>
    /// <param name="requests">The collection of tuples containing URIs and their corresponding content.</param>
    /// <param name="httpClient">The HttpClient instance to use for requests.</param>
    /// <param name="options">Configuration options for parallel execution and HTTP-specific resilience features. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing a list of HTTP response messages.</returns>
    /// <exception cref="ArgumentNullException">Thrown when requests or httpClient is null.</exception>
    public static async Task<List<HttpResponseMessage>> PostParallelAsync<TContent>(
        this IEnumerable<(Uri uri, TContent content)> requests,
        HttpClient httpClient,
        HttpOptions? options = null,
        CancellationToken cancellationToken = default) where TContent : HttpContent
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(httpClient);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        return await requests.SelectParallelAsync(
            async (request, ct) => await ExecuteHttpRequestAsync(
                request.uri,
                () => httpClient.PostAsync(request.uri, request.content, ct),
                options,
                ct).ConfigureAwait(false),
            parallelOptions,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes parallel HTTP PUT requests with specified content.
    /// </summary>
    /// <typeparam name="TContent">The type of content to send in PUT requests.</typeparam>
    /// <param name="requests">The collection of tuples containing URIs and their corresponding content.</param>
    /// <param name="httpClient">The HttpClient instance to use for requests.</param>
    /// <param name="options">Configuration options for parallel execution and HTTP-specific resilience features. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing a list of HTTP response messages.</returns>
    /// <exception cref="ArgumentNullException">Thrown when requests or httpClient is null.</exception>
    public static async Task<List<HttpResponseMessage>> PutParallelAsync<TContent>(
        this IEnumerable<(Uri uri, TContent content)> requests,
        HttpClient httpClient,
        HttpOptions? options = null,
        CancellationToken cancellationToken = default) where TContent : HttpContent
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(httpClient);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        return await requests.SelectParallelAsync(
            async (request, ct) => await ExecuteHttpRequestAsync(
                request.uri,
                () => httpClient.PutAsync(request.uri, request.content, ct),
                options,
                ct).ConfigureAwait(false),
            parallelOptions,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes parallel HTTP DELETE requests.
    /// </summary>
    /// <param name="uris">The collection of URIs to delete.</param>
    /// <param name="httpClient">The HttpClient instance to use for requests.</param>
    /// <param name="options">Configuration options for parallel execution and HTTP-specific resilience features. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing a list of HTTP response messages.</returns>
    /// <exception cref="ArgumentNullException">Thrown when uris or httpClient is null.</exception>
    public static async Task<List<HttpResponseMessage>> DeleteParallelAsync(
        this IEnumerable<Uri> uris,
        HttpClient httpClient,
        HttpOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uris);
        ArgumentNullException.ThrowIfNull(httpClient);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        return await uris.SelectParallelAsync(
            async (uri, ct) => await ExecuteHttpRequestAsync(
                uri,
                () => httpClient.DeleteAsync(uri, ct),
                options,
                ct).ConfigureAwait(false),
            parallelOptions,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes an HTTP request with automatic Retry-After header handling and error callbacks.
    /// </summary>
    private static async ValueTask<HttpResponseMessage> ExecuteHttpRequestAsync(
        Uri uri,
        Func<Task<HttpResponseMessage>> requestFunc,
        HttpOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await requestFunc().ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
                return response;

            // Handle Retry-After header for 429/503
            if (options.RespectRetryAfterHeader && response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
            {
                if (response.Headers.RetryAfter?.Delta.HasValue == true)
                {
                    var retryDelay = response.Headers.RetryAfter.Delta.Value;
                    await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
                }
            }

            // Invoke error callback if provided
            if (options.OnHttpErrorAsync is not null)
            {
                await options.OnHttpErrorAsync(uri, response.StatusCode, new HttpRequestException(
                    $"HTTP request failed with status code {(int)response.StatusCode} ({response.StatusCode})",
                    null,
                    response.StatusCode)).ConfigureAwait(false);
            }

            // Throw to trigger retry logic if status code is retriable
            if (options.RetriableStatusCodes.Contains(response.StatusCode))
            {
                response.Dispose();
                throw new HttpRequestException(
                    $"HTTP request failed with status code {(int)response.StatusCode} ({response.StatusCode})",
                    null,
                    response.StatusCode);
            }

            // For non-retriable errors, ensure success status code (throws)
            response.EnsureSuccessStatusCode();

            return response;
        }
        catch (HttpRequestException ex)
        {
            if (options.OnHttpErrorAsync is not null)
            {
                await options.OnHttpErrorAsync(uri, ex.StatusCode, ex).ConfigureAwait(false);
            }

            throw;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            var timeoutEx = new HttpRequestException("HTTP request timed out", ex);

            if (options.OnHttpErrorAsync is not null)
            {
                await options.OnHttpErrorAsync(uri, null, timeoutEx).ConfigureAwait(false);
            }

            throw timeoutEx;
        }
    }
}
