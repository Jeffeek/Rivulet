namespace Rivulet.Http;

/// <summary>
/// Provides integration between Rivulet parallel HTTP operations and IHttpClientFactory.
/// </summary>
public static class HttpClientFactoryExtensions
{
    /// <summary>
    /// Executes parallel HTTP GET requests using a named HttpClient from IHttpClientFactory.
    /// </summary>
    /// <param name="uris">The collection of URIs to fetch.</param>
    /// <param name="httpClientFactory">The IHttpClientFactory instance to create HttpClient instances.</param>
    /// <param name="clientName">The name of the HttpClient to use. If null, uses the default client.</param>
    /// <param name="options">Configuration options for parallel execution and HTTP-specific resilience features. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing a list of HTTP response messages.</returns>
    /// <exception cref="ArgumentNullException">Thrown when uris or httpClientFactory is null.</exception>
    public static async Task<List<HttpResponseMessage>> GetParallelAsync(
        this IEnumerable<Uri> uris,
        IHttpClientFactory httpClientFactory,
        string? clientName = null,
        HttpOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uris);
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        var httpClient = string.IsNullOrEmpty(clientName)
            ? httpClientFactory.CreateClient()
            : httpClientFactory.CreateClient(clientName);

        return await uris.GetParallelAsync(httpClient, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes parallel HTTP GET requests using a named HttpClient and returns response content as strings.
    /// </summary>
    /// <param name="uris">The collection of URIs to fetch.</param>
    /// <param name="httpClientFactory">The IHttpClientFactory instance to create HttpClient instances.</param>
    /// <param name="clientName">The name of the HttpClient to use. If null, uses the default client.</param>
    /// <param name="options">Configuration options for parallel execution and HTTP-specific resilience features. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing a list of response content strings.</returns>
    /// <exception cref="ArgumentNullException">Thrown when uris or httpClientFactory is null.</exception>
    public static async Task<List<string>> GetStringParallelAsync(
        this IEnumerable<Uri> uris,
        IHttpClientFactory httpClientFactory,
        string? clientName = null,
        HttpOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uris);
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        var httpClient = string.IsNullOrEmpty(clientName)
            ? httpClientFactory.CreateClient()
            : httpClientFactory.CreateClient(clientName);

        return await uris.GetStringParallelAsync(httpClient, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes parallel HTTP GET requests using a named HttpClient and returns response content as byte arrays.
    /// </summary>
    /// <param name="uris">The collection of URIs to fetch.</param>
    /// <param name="httpClientFactory">The IHttpClientFactory instance to create HttpClient instances.</param>
    /// <param name="clientName">The name of the HttpClient to use. If null, uses the default client.</param>
    /// <param name="options">Configuration options for parallel execution and HTTP-specific resilience features. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing a list of byte arrays.</returns>
    /// <exception cref="ArgumentNullException">Thrown when uris or httpClientFactory is null.</exception>
    public static async Task<List<byte[]>> GetByteArrayParallelAsync(
        this IEnumerable<Uri> uris,
        IHttpClientFactory httpClientFactory,
        string? clientName = null,
        HttpOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uris);
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        var httpClient = string.IsNullOrEmpty(clientName)
            ? httpClientFactory.CreateClient()
            : httpClientFactory.CreateClient(clientName);

        return await uris.GetByteArrayParallelAsync(httpClient, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes parallel HTTP POST requests using a named HttpClient.
    /// </summary>
    /// <typeparam name="TContent">The type of content to send in POST requests.</typeparam>
    /// <param name="requests">The collection of tuples containing URIs and their corresponding content.</param>
    /// <param name="httpClientFactory">The IHttpClientFactory instance to create HttpClient instances.</param>
    /// <param name="clientName">The name of the HttpClient to use. If null, uses the default client.</param>
    /// <param name="options">Configuration options for parallel execution and HTTP-specific resilience features. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing a list of HTTP response messages.</returns>
    /// <exception cref="ArgumentNullException">Thrown when requests or httpClientFactory is null.</exception>
    public static async Task<List<HttpResponseMessage>> PostParallelAsync<TContent>(
        this IEnumerable<(Uri uri, TContent content)> requests,
        IHttpClientFactory httpClientFactory,
        string? clientName = null,
        HttpOptions? options = null,
        CancellationToken cancellationToken = default) where TContent : HttpContent
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        var httpClient = string.IsNullOrEmpty(clientName)
            ? httpClientFactory.CreateClient()
            : httpClientFactory.CreateClient(clientName);

        return await requests.PostParallelAsync(httpClient, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes parallel HTTP PUT requests using a named HttpClient.
    /// </summary>
    /// <typeparam name="TContent">The type of content to send in PUT requests.</typeparam>
    /// <param name="requests">The collection of tuples containing URIs and their corresponding content.</param>
    /// <param name="httpClientFactory">The IHttpClientFactory instance to create HttpClient instances.</param>
    /// <param name="clientName">The name of the HttpClient to use. If null, uses the default client.</param>
    /// <param name="options">Configuration options for parallel execution and HTTP-specific resilience features. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing a list of HTTP response messages.</returns>
    /// <exception cref="ArgumentNullException">Thrown when requests or httpClientFactory is null.</exception>
    public static async Task<List<HttpResponseMessage>> PutParallelAsync<TContent>(
        this IEnumerable<(Uri uri, TContent content)> requests,
        IHttpClientFactory httpClientFactory,
        string? clientName = null,
        HttpOptions? options = null,
        CancellationToken cancellationToken = default) where TContent : HttpContent
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        var httpClient = string.IsNullOrEmpty(clientName)
            ? httpClientFactory.CreateClient()
            : httpClientFactory.CreateClient(clientName);

        return await requests.PutParallelAsync(httpClient, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes parallel HTTP DELETE requests using a named HttpClient.
    /// </summary>
    /// <param name="uris">The collection of URIs to delete.</param>
    /// <param name="httpClientFactory">The IHttpClientFactory instance to create HttpClient instances.</param>
    /// <param name="clientName">The name of the HttpClient to use. If null, uses the default client.</param>
    /// <param name="options">Configuration options for parallel execution and HTTP-specific resilience features. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing a list of HTTP response messages.</returns>
    /// <exception cref="ArgumentNullException">Thrown when uris or httpClientFactory is null.</exception>
    public static async Task<List<HttpResponseMessage>> DeleteParallelAsync(
        this IEnumerable<Uri> uris,
        IHttpClientFactory httpClientFactory,
        string? clientName = null,
        HttpOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uris);
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        var httpClient = string.IsNullOrEmpty(clientName)
            ? httpClientFactory.CreateClient()
            : httpClientFactory.CreateClient(clientName);

        return await uris.DeleteParallelAsync(httpClient, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Downloads files from multiple URIs in parallel using a named HttpClient.
    /// </summary>
    /// <param name="downloads">The collection of download requests containing source URI and destination file path.</param>
    /// <param name="httpClientFactory">The IHttpClientFactory instance to create HttpClient instances.</param>
    /// <param name="clientName">The name of the HttpClient to use. If null, uses the default client.</param>
    /// <param name="options">Configuration options for streaming downloads. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing a list of download results.</returns>
    /// <exception cref="ArgumentNullException">Thrown when downloads or httpClientFactory is null.</exception>
    public static async Task<List<(Uri uri, string filePath, long bytesDownloaded)>> DownloadParallelAsync(
        this IEnumerable<(Uri uri, string destinationPath)> downloads,
        IHttpClientFactory httpClientFactory,
        string? clientName = null,
        StreamingDownloadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(downloads);
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        var httpClient = string.IsNullOrEmpty(clientName)
            ? httpClientFactory.CreateClient()
            : httpClientFactory.CreateClient(clientName);

        return await downloads.DownloadParallelAsync(httpClient, options, cancellationToken).ConfigureAwait(false);
    }
}
