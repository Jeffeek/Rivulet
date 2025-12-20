namespace Rivulet.Http.Internal;

/// <summary>
///     Internal helper to reduce code duplication in HTTP operations.
/// </summary>
internal static class HttpHelper
{
    /// <summary>
    ///     Initializes HTTP options and gets merged parallel options.
    /// </summary>
    public static (HttpOptions options, Core.ParallelOptionsRivulet parallelOptions) InitializeOptions(HttpOptions? options)
    {
        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();
        return (options, parallelOptions);
    }

    /// <summary>
    ///     Creates HttpClient from factory, using named client if specified.
    /// </summary>
    public static HttpClient CreateClient(IHttpClientFactory httpClientFactory, string? clientName) =>
        string.IsNullOrEmpty(clientName)
            ? httpClientFactory.CreateClient()
            : httpClientFactory.CreateClient(clientName);

    /// <summary>
    ///     Initializes progress tracking state for streaming downloads.
    /// </summary>
    public static (long bytesDownloaded, byte[] buffer, long lastProgressReport, long progressIntervalTicks)
        InitializeProgressTracking(StreamingDownloadOptions options, long existingFileSize = 0)
    {
        var bytesDownloaded = existingFileSize;
        var buffer = new byte[options.BufferSize];
        var lastProgressReport = System.Diagnostics.Stopwatch.GetTimestamp();
        var progressIntervalTicks = options.ProgressInterval.Ticks;

        return (bytesDownloaded, buffer, lastProgressReport, progressIntervalTicks);
    }

    /// <summary>
    ///     Checks if progress should be reported and invokes callback if needed.
    ///     Returns updated lastProgressReport value.
    /// </summary>
    public static async ValueTask<long> ReportProgressIfNeededAsync(
        StreamingDownloadOptions options,
        Uri uri,
        long bytesDownloaded,
        long? totalBytes,
        long lastProgressReport,
        long progressIntervalTicks
    )
    {
        if (options.OnProgressAsync is null)
            return lastProgressReport;

        var elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - lastProgressReport;
        if (elapsed < progressIntervalTicks * (System.Diagnostics.Stopwatch.Frequency / TimeSpan.TicksPerSecond))
            return lastProgressReport;

        await options.OnProgressAsync(uri, bytesDownloaded, totalBytes).ConfigureAwait(false);

        return System.Diagnostics.Stopwatch.GetTimestamp();
    }
}
