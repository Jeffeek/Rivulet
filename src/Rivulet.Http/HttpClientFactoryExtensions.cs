// HttpClient instances from IHttpClientFactory should NOT be disposed - the factory manages their lifecycle

using System.Diagnostics.CodeAnalysis;
using Rivulet.Http.Internal;

#pragma warning disable CA2000 // Do not dispose objects before losing scope

namespace Rivulet.Http;

/// <summary>
///     Provides integration between Rivulet parallel HTTP operations and IHttpClientFactory.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
public static class HttpClientFactoryExtensions
{
    /// <summary>
    ///     Validates arguments, creates an HttpClient from the factory, and invokes the delegate.
    /// </summary>
    private static Task<T> CreateClientAndExecute<T>(
        IHttpClientFactory httpClientFactory,
        string? clientName,
        Func<HttpClient, Task<T>> execute
    )
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        var httpClient = HttpHelper.CreateClient(httpClientFactory, clientName);
        return execute(httpClient);
    }

    /// <param name="uris">The collection of URIs to fetch.</param>
    extension(IEnumerable<Uri> uris)
    {
        /// <summary>
        ///     Executes parallel HTTP GET requests using a named HttpClient from IHttpClientFactory.
        /// </summary>
        /// <param name="httpClientFactory">The IHttpClientFactory instance to create HttpClient instances.</param>
        /// <param name="clientName">The name of the HttpClient to use. If null, uses the default client.</param>
        /// <param name="options">
        ///     Configuration options for parallel execution and HTTP-specific resilience features. If null,
        ///     defaults are used.
        /// </param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation, containing a list of HTTP response messages.</returns>
        /// <exception cref="ArgumentNullException">Thrown when uris or httpClientFactory is null.</exception>
        public Task<List<HttpResponseMessage>> GetParallelAsync(
            IHttpClientFactory httpClientFactory,
            string? clientName = null,
            HttpOptions? options = null,
            CancellationToken cancellationToken = default
        ) => CreateClientAndExecute(
            httpClientFactory,
            clientName,
            client => uris.GetParallelAsync(client, options, cancellationToken)
        );

        /// <summary>
        ///     Executes parallel HTTP GET requests using a named HttpClient and returns response content as strings.
        /// </summary>
        /// <param name="httpClientFactory">The IHttpClientFactory instance to create HttpClient instances.</param>
        /// <param name="clientName">The name of the HttpClient to use. If null, uses the default client.</param>
        /// <param name="options">
        ///     Configuration options for parallel execution and HTTP-specific resilience features. If null,
        ///     defaults are used.
        /// </param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation, containing a list of response content strings.</returns>
        /// <exception cref="ArgumentNullException">Thrown when uris or httpClientFactory is null.</exception>
        public Task<List<string>> GetStringParallelAsync(
            IHttpClientFactory httpClientFactory,
            string? clientName = null,
            HttpOptions? options = null,
            CancellationToken cancellationToken = default
        ) => CreateClientAndExecute(
            httpClientFactory,
            clientName,
            client => uris.GetStringParallelAsync(client, options, cancellationToken)
        );

        /// <summary>
        ///     Executes parallel HTTP GET requests using a named HttpClient and returns response content as byte arrays.
        /// </summary>
        /// <param name="httpClientFactory">The IHttpClientFactory instance to create HttpClient instances.</param>
        /// <param name="clientName">The name of the HttpClient to use. If null, uses the default client.</param>
        /// <param name="options">
        ///     Configuration options for parallel execution and HTTP-specific resilience features. If null,
        ///     defaults are used.
        /// </param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation, containing a list of byte arrays.</returns>
        /// <exception cref="ArgumentNullException">Thrown when uris or httpClientFactory is null.</exception>
        public Task<List<byte[]>> GetByteArrayParallelAsync(
            IHttpClientFactory httpClientFactory,
            string? clientName = null,
            HttpOptions? options = null,
            CancellationToken cancellationToken = default
        ) => CreateClientAndExecute(
            httpClientFactory,
            clientName,
            client => uris.GetByteArrayParallelAsync(client, options, cancellationToken)
        );

        /// <summary>
        ///     Executes parallel HTTP DELETE requests using a named HttpClient.
        /// </summary>
        /// <param name="httpClientFactory">The IHttpClientFactory instance to create HttpClient instances.</param>
        /// <param name="clientName">The name of the HttpClient to use. If null, uses the default client.</param>
        /// <param name="options">
        ///     Configuration options for parallel execution and HTTP-specific resilience features. If null,
        ///     defaults are used.
        /// </param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation, containing a list of HTTP response messages.</returns>
        /// <exception cref="ArgumentNullException">Thrown when uris or httpClientFactory is null.</exception>
        public Task<List<HttpResponseMessage>> DeleteParallelAsync(
            IHttpClientFactory httpClientFactory,
            string? clientName = null,
            HttpOptions? options = null,
            CancellationToken cancellationToken = default
        ) => CreateClientAndExecute(
            httpClientFactory,
            clientName,
            client => uris.DeleteParallelAsync(client, options, cancellationToken)
        );
    }

    /// <param name="requests">The collection of tuples containing URIs and their corresponding content.</param>
    /// <typeparam name="TContent">The type of content to send in POST requests.</typeparam>
    extension<TContent>(IEnumerable<(Uri uri, TContent content)> requests)
        where TContent : HttpContent
    {
        /// <summary>
        ///     Executes parallel HTTP POST requests using a named HttpClient.
        /// </summary>
        /// <param name="httpClientFactory">The IHttpClientFactory instance to create HttpClient instances.</param>
        /// <param name="clientName">The name of the HttpClient to use. If null, uses the default client.</param>
        /// <param name="options">
        ///     Configuration options for parallel execution and HTTP-specific resilience features. If null,
        ///     defaults are used.
        /// </param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation, containing a list of HTTP response messages.</returns>
        /// <exception cref="ArgumentNullException">Thrown when requests or httpClientFactory is null.</exception>
        public Task<List<HttpResponseMessage>> PostParallelAsync(
            IHttpClientFactory httpClientFactory,
            string? clientName = null,
            HttpOptions? options = null,
            CancellationToken cancellationToken = default
        ) => CreateClientAndExecute(
            httpClientFactory,
            clientName,
            client => requests.PostParallelAsync(client, options, cancellationToken)
        );

        /// <summary>
        ///     Executes parallel HTTP PUT requests using a named HttpClient.
        /// </summary>
        /// <param name="httpClientFactory">The IHttpClientFactory instance to create HttpClient instances.</param>
        /// <param name="clientName">The name of the HttpClient to use. If null, uses the default client.</param>
        /// <param name="options">
        ///     Configuration options for parallel execution and HTTP-specific resilience features. If null,
        ///     defaults are used.
        /// </param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation, containing a list of HTTP response messages.</returns>
        /// <exception cref="ArgumentNullException">Thrown when requests or httpClientFactory is null.</exception>
        public Task<List<HttpResponseMessage>> PutParallelAsync(
            IHttpClientFactory httpClientFactory,
            string? clientName = null,
            HttpOptions? options = null,
            CancellationToken cancellationToken = default
        ) =>
            CreateClientAndExecute(
                httpClientFactory,
                clientName,
                client => requests.PutParallelAsync(client, options, cancellationToken)
            );
    }

    /// <summary>
    ///     Downloads files from multiple URIs in parallel using a named HttpClient.
    /// </summary>
    /// <param name="downloads">The collection of download requests containing source URI and destination file path.</param>
    /// <param name="httpClientFactory">The IHttpClientFactory instance to create HttpClient instances.</param>
    /// <param name="clientName">The name of the HttpClient to use. If null, uses the default client.</param>
    /// <param name="options">Configuration options for streaming downloads. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing a list of download results.</returns>
    /// <exception cref="ArgumentNullException">Thrown when downloads or httpClientFactory is null.</exception>
    public static Task<List<(Uri uri, string filePath, long bytesDownloaded)>> DownloadParallelAsync(
        this IEnumerable<(Uri uri, string destinationPath)> downloads,
        IHttpClientFactory httpClientFactory,
        string? clientName = null,
        StreamingDownloadOptions? options = null,
        CancellationToken cancellationToken = default
    ) => CreateClientAndExecute(
        httpClientFactory,
        clientName,
        client => downloads.DownloadParallelAsync(client, options, cancellationToken)
    );
}
