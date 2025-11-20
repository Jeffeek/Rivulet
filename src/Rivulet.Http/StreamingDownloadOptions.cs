namespace Rivulet.Http;

/// <summary>
/// Configuration options for streaming downloads with resume support.
/// </summary>
public sealed class StreamingDownloadOptions
{
    /// <summary>
    /// Gets the HTTP options for controlling parallel execution and resilience features.
    /// </summary>
    public HttpOptions? HttpOptions { get; init; }

    /// <summary>
    /// Gets the buffer size for reading and writing streams during download.
    /// Defaults to 81920 bytes (80 KB).
    /// </summary>
    public int BufferSize { get; init; } = 81920;

    /// <summary>
    /// Gets a value indicating whether to enable resume support for interrupted downloads.
    /// When true, uses HTTP Range headers to resume partial downloads.
    /// Requires the server to support partial content (206 responses).
    /// Defaults to true.
    /// </summary>
    public bool EnableResume { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether to validate content length before and after download.
    /// When true, validates the Content-Length header matches the downloaded bytes.
    /// Defaults to true.
    /// </summary>
    public bool ValidateContentLength { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether to overwrite existing files.
    /// When false and a file exists, throws IOException.
    /// When true, overwrites existing files.
    /// Defaults to false.
    /// </summary>
    public bool OverwriteExisting { get; init; }

    /// <summary>
    /// Gets a callback invoked periodically during download progress.
    /// Receives the URI, bytes downloaded so far, and total bytes (if known).
    /// </summary>
    public Func<Uri, long, long?, ValueTask>? OnProgressAsync { get; init; }

    /// <summary>
    /// Gets the interval at which progress callbacks are invoked.
    /// Defaults to 1 second.
    /// </summary>
    public TimeSpan ProgressInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets a callback invoked when a download is resumed from a partial file.
    /// Receives the URI and the byte offset from which the download is resuming.
    /// </summary>
    public Func<Uri, long, ValueTask>? OnResumeAsync { get; init; }

    /// <summary>
    /// Gets a callback invoked when a download completes successfully.
    /// Receives the URI, the destination file path, and the total bytes downloaded.
    /// </summary>
    public Func<Uri, string, long, ValueTask>? OnCompleteAsync { get; init; }
}
