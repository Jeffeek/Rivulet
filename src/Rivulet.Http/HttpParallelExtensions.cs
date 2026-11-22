using System.Diagnostics.CodeAnalysis;
using System.Net;
using Rivulet.Core;
using Rivulet.Http.Internal;

namespace Rivulet.Http;

/// <summary>
///     Provides parallel HTTP operations with bounded concurrency, automatic retries, and resilience features.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
public static class HttpParallelExtensions
{
    /// <summary>
    ///     Executes parallel HTTP GET requests and returns the response content using the provided extractor.
    /// </summary>
    private static Task<List<TR>> ExecuteGetContentParallelAsync<TSrc, TR>(
        IEnumerable<TSrc> source,
        HttpClient httpClient,
        HttpOptions? options,
        CancellationToken cancellationToken,
        Func<HttpResponseMessage, CancellationToken, Task<TR>> contentExtractor
    )
        where TSrc : Uri
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(httpClient);

        (options, var parallelOptions) = HttpHelper.InitializeOptions(options);

        return source.SelectParallelAsync(
            async (item, ct) =>
            {
                using var response = await ExecuteHttpRequestAsync(
                        item,
                        () => httpClient.GetAsync(item, HttpCompletionOption.ResponseContentRead, ct),
                        options,
                        ct)
                    .ConfigureAwait(false);

                return await contentExtractor(response, ct).ConfigureAwait(false);
            },
            parallelOptions,
            cancellationToken);
    }

    /// <summary>
    ///     Executes parallel HTTP requests with bounded concurrency and automatic retries.
    /// </summary>
    private static Task<List<HttpResponseMessage>> ExecuteMethodParallelAsync<TSrc>(
        IEnumerable<TSrc> source,
        HttpOptions? options,
        CancellationToken cancellationToken,
        Func<TSrc, Uri> uriExtractor,
        Func<TSrc, CancellationToken, Task<HttpResponseMessage>> httpMethod
    )
    {
        ArgumentNullException.ThrowIfNull(source);

        (options, var parallelOptions) = HttpHelper.InitializeOptions(options);

        return source.SelectParallelAsync(
            (item, ct) => ExecuteHttpRequestAsync(uriExtractor(item), () => httpMethod(item, ct), options, ct),
            parallelOptions,
            cancellationToken);
    }

    /// <param name="uris">The collection of URIs to fetch.</param>
    extension(IEnumerable<Uri> uris)
    {
        /// <summary>
        ///     Executes parallel HTTP GET requests for a collection of URIs with bounded concurrency and automatic retries.
        /// </summary>
        /// <param name="httpClient">The HttpClient instance to use for requests.</param>
        /// <param name="options">
        ///     Configuration options for parallel execution and HTTP-specific resilience features. If null,
        ///     defaults are used.
        /// </param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation, containing a list of HTTP response messages.</returns>
        /// <exception cref="ArgumentNullException">Thrown when uris or httpClient is null.</exception>
        /// <exception cref="AggregateException">Thrown when errors occur and ErrorMode is set to CollectAndContinue.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
        public Task<List<HttpResponseMessage>> GetParallelAsync(
            HttpClient httpClient,
            HttpOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            ArgumentNullException.ThrowIfNull(httpClient);

            return ExecuteMethodParallelAsync(
                uris,
                options,
                cancellationToken,
                static x => x,
                (uri, ct) => httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct));
        }

        /// <summary>
        ///     Executes parallel HTTP GET requests and returns the response content as strings.
        /// </summary>
        /// <param name="httpClient">The HttpClient instance to use for requests.</param>
        /// <param name="options">
        ///     Configuration options for parallel execution and HTTP-specific resilience features. If null,
        ///     defaults are used.
        /// </param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation, containing a list of response content strings.</returns>
        /// <exception cref="ArgumentNullException">Thrown when uris or httpClient is null.</exception>
        public Task<List<string>> GetStringParallelAsync(
            HttpClient httpClient,
            HttpOptions? options = null,
            CancellationToken cancellationToken = default
        ) => ExecuteGetContentParallelAsync(
            uris,
            httpClient,
            options,
            cancellationToken,
            static (response, ct) => response.Content.ReadAsStringAsync(ct));

        /// <summary>
        ///     Executes parallel HTTP GET requests and returns the response content as byte arrays.
        /// </summary>
        /// <param name="httpClient">The HttpClient instance to use for requests.</param>
        /// <param name="options">
        ///     Configuration options for parallel execution and HTTP-specific resilience features. If null,
        ///     defaults are used.
        /// </param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation, containing a list of byte arrays.</returns>
        /// <exception cref="ArgumentNullException">Thrown when uris or httpClient is null.</exception>
        public Task<List<byte[]>> GetByteArrayParallelAsync(
            HttpClient httpClient,
            HttpOptions? options = null,
            CancellationToken cancellationToken = default
        ) => ExecuteGetContentParallelAsync(
            uris,
            httpClient,
            options,
            cancellationToken,
            static (response, ct) => response.Content.ReadAsByteArrayAsync(ct));

        /// <summary>
        ///     Executes parallel HTTP DELETE requests.
        /// </summary>
        /// <param name="httpClient">The HttpClient instance to use for requests.</param>
        /// <param name="options">
        ///     Configuration options for parallel execution and HTTP-specific resilience features. If null,
        ///     defaults are used.
        /// </param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation, containing a list of HTTP response messages.</returns>
        /// <exception cref="ArgumentNullException">Thrown when uris or httpClient is null.</exception>
        public Task<List<HttpResponseMessage>> DeleteParallelAsync(
            HttpClient httpClient,
            HttpOptions? options = null,
            CancellationToken cancellationToken = default
        ) => ExecuteMethodParallelAsync(
            uris,
            options,
            cancellationToken,
            static x => x,
            httpClient.DeleteAsync);
    }

    /// <param name="requests">The collection of tuples containing URIs and their corresponding content.</param>
    /// <typeparam name="TContent">The type of content to send in POST requests.</typeparam>
    extension<TContent>(IEnumerable<(Uri uri, TContent content)> requests)
        where TContent : HttpContent
    {
        /// <summary>
        ///     Executes parallel HTTP POST requests with specified content.
        /// </summary>
        /// <param name="httpClient">The HttpClient instance to use for requests.</param>
        /// <param name="options">
        ///     Configuration options for parallel execution and HTTP-specific resilience features. If null,
        ///     defaults are used.
        /// </param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation, containing a list of HTTP response messages.</returns>
        /// <exception cref="ArgumentNullException">Thrown when requests or httpClient is null.</exception>
        public Task<List<HttpResponseMessage>> PostParallelAsync(
            HttpClient httpClient,
            HttpOptions? options = null,
            CancellationToken cancellationToken = default
        ) =>
            ExecuteMethodParallelAsync(
                requests,
                options,
                cancellationToken,
                static r => r.uri,
                (request, ct) => httpClient.PostAsync(request.uri, request.content, ct));

        /// <summary>
        ///     Executes parallel HTTP PUT requests with specified content.
        /// </summary>
        /// <param name="httpClient">The HttpClient instance to use for requests.</param>
        /// <param name="options">
        ///     Configuration options for parallel execution and HTTP-specific resilience features. If null,
        ///     defaults are used.
        /// </param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation, containing a list of HTTP response messages.</returns>
        /// <exception cref="ArgumentNullException">Thrown when requests or httpClient is null.</exception>
        public Task<List<HttpResponseMessage>> PutParallelAsync(
            HttpClient httpClient,
            HttpOptions? options = null,
            CancellationToken cancellationToken = default
        ) =>
            ExecuteMethodParallelAsync(
                requests,
                options,
                cancellationToken,
                static r => r.uri,
                (request, ct) => httpClient.PutAsync(request.uri, request.content, ct));
    }

    /// <summary>
    ///     Executes an HTTP request with automatic Retry-After header handling and error callbacks.
    /// </summary>
    private static async ValueTask<HttpResponseMessage> ExecuteHttpRequestAsync(
        Uri uri,
        Func<Task<HttpResponseMessage>> requestFunc,
        HttpOptions options,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var response = await requestFunc().ConfigureAwait(false);

            if (response.IsSuccessStatusCode) return response;

            // Handle Retry-After header for 429/503
            if (options.RespectRetryAfterHeader &&
                response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable &&
                response.Headers.RetryAfter?.Delta.HasValue == true)
            {
                var retryDelay = response.Headers.RetryAfter.Delta.Value;
                await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
            }

            // Invoke error callback if provided
            if (options.OnHttpErrorAsync is not null)
            {
                await options.OnHttpErrorAsync(uri,
                        response.StatusCode,
                        new HttpRequestException(
                            $"HTTP request failed with status code {(int)response.StatusCode} ({response.StatusCode})",
                            null,
                            response.StatusCode))
                    .ConfigureAwait(false);
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
                await options.OnHttpErrorAsync(uri, ex.StatusCode, ex).ConfigureAwait(false);

            throw;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            var timeoutEx = new HttpRequestException("HTTP request timed out", ex);

            if (options.OnHttpErrorAsync is not null)
                await options.OnHttpErrorAsync(uri, null, timeoutEx).ConfigureAwait(false);

            throw timeoutEx;
        }
    }
}
