using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using Rivulet.Core;

namespace Rivulet.Http;

/// <summary>
///     Provides streaming download operations with resume support and parallel processing.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
public static class HttpStreamingExtensions
{
    /// <summary>
    ///     Downloads files from multiple URIs in parallel with resume support and progress tracking.
    /// </summary>
    /// <param name="downloads">The collection of download requests containing source URI and destination file path.</param>
    /// <param name="httpClient">The HttpClient instance to use for downloads.</param>
    /// <param name="options">Configuration options for streaming downloads. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>
    ///     A task representing the asynchronous operation, containing a list of download results with URIs and bytes
    ///     downloaded.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when downloads or httpClient is null.</exception>
    /// <exception cref="IOException">Thrown when file operations fail or EnableResume is false and file exists.</exception>
    /// <exception cref="HttpRequestException">Thrown when HTTP requests fail and retries are exhausted.</exception>
    public static async Task<List<(Uri uri, string filePath, long bytesDownloaded)>> DownloadParallelAsync(
        this IEnumerable<(Uri uri, string destinationPath)> downloads,
        HttpClient httpClient,
        StreamingDownloadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(downloads);
        ArgumentNullException.ThrowIfNull(httpClient);

        options ??= new();
        var httpOptions = options.HttpOptions ?? new HttpOptions();
        var parallelOptions = httpOptions.GetMergedParallelOptions();

        return await downloads.SelectParallelAsync(
                async (download, ct) =>
                {
                    var (uri, destinationPath) = download;
                    var bytesDownloaded = await DownloadFileAsync(
                            uri,
                            destinationPath,
                            httpClient,
                            options,
                            ct)
                        .ConfigureAwait(false);

                    return (uri, destinationPath, bytesDownloaded);
                },
                parallelOptions,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Streams download content to a provided stream for a single URI.
    /// </summary>
    /// <param name="uri">The URI to download from.</param>
    /// <param name="destination">The destination stream to write the downloaded content.</param>
    /// <param name="httpClient">The HttpClient instance to use for the download.</param>
    /// <param name="options">Configuration options for the download. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the total bytes downloaded.</returns>
    /// <exception cref="ArgumentNullException">Thrown when uri, destination, or httpClient is null.</exception>
    public static async ValueTask<long> DownloadToStreamAsync(
        Uri uri,
        Stream destination,
        HttpClient httpClient,
        StreamingDownloadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(httpClient);

        options ??= new();

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);

        using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;

#pragma warning disable CA2007 // ConfigureAwait not applicable to await using declarations
        await using var contentStream =
            await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#pragma warning restore CA2007

        var bytesDownloaded = 0L;
        var buffer = new byte[options.BufferSize];
        var lastProgressReport = Stopwatch.GetTimestamp();
        var progressIntervalTicks = options.ProgressInterval.Ticks;

        int bytesRead;
        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            bytesDownloaded += bytesRead;

            // Report progress if callback is provided and interval has elapsed
            if (options.OnProgressAsync is null) continue;

            var elapsed = Stopwatch.GetTimestamp() - lastProgressReport;
            if (elapsed < progressIntervalTicks * (Stopwatch.Frequency / TimeSpan.TicksPerSecond)) continue;

            await options.OnProgressAsync(uri, bytesDownloaded, totalBytes).ConfigureAwait(false);
            lastProgressReport = Stopwatch.GetTimestamp();
        }

        // Final progress report
        if (options.OnProgressAsync is not null)
            await options.OnProgressAsync(uri, bytesDownloaded, totalBytes).ConfigureAwait(false);

        // Validate content length if requested
        return options.ValidateContentLength && totalBytes.HasValue && bytesDownloaded != totalBytes.Value
            ? throw new HttpRequestException($"Content length mismatch: expected {totalBytes.Value} bytes, but downloaded {bytesDownloaded} bytes")
            : bytesDownloaded;
    }

    /// <summary>
    ///     Downloads a single file with resume support.
    /// </summary>
    private static async ValueTask<long> DownloadFileAsync(
        Uri uri,
        string destinationPath,
        HttpClient httpClient,
        StreamingDownloadOptions options,
        CancellationToken cancellationToken)
    {
        // Ensure directory exists
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var existingFileSize = 0L;
        var fileMode = FileMode.Create;

        // Check for resume support
        if (options.EnableResume && File.Exists(destinationPath))
        {
            var fileInfo = new FileInfo(destinationPath);
            existingFileSize = fileInfo.Length;

            if (existingFileSize > 0)
            {
                // Try to resume download
                var supportsResume = await CheckRangeSupport(uri, httpClient, cancellationToken).ConfigureAwait(false);

                if (supportsResume)
                {
                    fileMode = FileMode.Append;
                    if (options.OnResumeAsync is not null)
                        await options.OnResumeAsync(uri, existingFileSize).ConfigureAwait(false);
                }
                else
                {
                    // Server doesn't support resume, start over
                    existingFileSize = 0;
                }
            }
        }
        else if (File.Exists(destinationPath) && !options.OverwriteExisting)
            throw new IOException($"File already exists at '{destinationPath}' and OverwriteExisting is false");

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);

        // Add Range header for resume
        if (existingFileSize > 0) request.Headers.Range = new(existingFileSize, null);

        using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);

        // Handle response based on status code
        if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            // File is already complete
            if (options.OnCompleteAsync is not null)
                await options.OnCompleteAsync(uri, destinationPath, existingFileSize).ConfigureAwait(false);

            return existingFileSize;
        }

        response.EnsureSuccessStatusCode();

        var totalBytes = existingFileSize + (response.Content.Headers.ContentLength ?? 0);

#pragma warning disable CA2007 // ConfigureAwait not applicable to await using declarations
        await using var fileStream = new FileStream(
            destinationPath,
            fileMode,
            FileAccess.Write,
            FileShare.None,
            options.BufferSize,
            true);

        await using var contentStream =
            await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#pragma warning restore CA2007

        var bytesDownloaded = existingFileSize;
        var buffer = new byte[options.BufferSize];
        var lastProgressReport = Stopwatch.GetTimestamp();
        var progressIntervalTicks = options.ProgressInterval.Ticks;

        int bytesRead;
        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            bytesDownloaded += bytesRead;

            if (options.OnProgressAsync is null) continue;

            // Report progress if callback is provided and interval has elapsed
            var elapsed = Stopwatch.GetTimestamp() - lastProgressReport;
            if (elapsed < progressIntervalTicks * (Stopwatch.Frequency / TimeSpan.TicksPerSecond)) continue;

            await options.OnProgressAsync(uri,
                    bytesDownloaded,
                    response.StatusCode == HttpStatusCode.PartialContent
                        ? totalBytes
                        : response.Content.Headers.ContentLength)
                .ConfigureAwait(false);
            lastProgressReport = Stopwatch.GetTimestamp();
        }

        // Ensure all data is written to disk
        await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);

        // Final progress report
        if (options.OnProgressAsync is not null)
        {
            await options.OnProgressAsync(uri,
                    bytesDownloaded,
                    response.StatusCode == HttpStatusCode.PartialContent
                        ? totalBytes
                        : response.Content.Headers.ContentLength)
                .ConfigureAwait(false);
        }

        // Validate content length if requested
        if (options.ValidateContentLength)
        {
            var expectedSize = response.StatusCode == HttpStatusCode.PartialContent
                ? totalBytes
                : response.Content.Headers.ContentLength;

            if (expectedSize.HasValue && bytesDownloaded != expectedSize.Value)
            {
                throw new HttpRequestException(
                    $"Content length mismatch for {uri}: expected {expectedSize.Value} bytes, but downloaded {bytesDownloaded} bytes");
            }
        }

        // Invoke completion callback
        if (options.OnCompleteAsync is not null)
            await options.OnCompleteAsync(uri, destinationPath, bytesDownloaded).ConfigureAwait(false);

        return bytesDownloaded;
    }

    /// <summary>
    ///     Checks if the server supports HTTP Range requests (resume capability).
    /// </summary>
    private static async ValueTask<bool> CheckRangeSupport(
        Uri uri,
        HttpMessageInvoker httpClient,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, uri);
            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            return response.Headers.AcceptRanges.Contains("bytes");
        }
        catch
        {
            // Generic catch is intentional: if HEAD request fails for any reason
            // (network issues, server errors, authorization, etc.), we assume no range support
            // and fall back to full download. This provides maximum compatibility.
            return false;
        }
    }
}