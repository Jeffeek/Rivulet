using System.Text;
using Rivulet.Core.Internal;
using Rivulet.Diagnostics.Internal;

namespace Rivulet.Diagnostics;

/// <summary>
///     EventListener that writes Rivulet metrics to a file.
///     Supports automatic file rotation based on size or time.
/// </summary>
/// <example>
///     <code>
/// using var listener = new RivuletFileListener("metrics.log", maxFileSizeBytes: 10 * 1024 * 1024);
/// await Enumerable.Range(1, 100)
///     .ToAsyncEnumerable()
///     .SelectParallelAsync(async x =&gt; await ProcessAsync(x), new ParallelOptionsRivulet
///     {
///         MaxDegreeOfParallelism = 10
///     });
/// </code>
/// </example>
public sealed class RivuletFileListener : RivuletEventListenerBase, IAsyncDisposable
{
    private readonly string _filePath;
    private readonly long _maxFileSizeBytes;
    private readonly object _lock = LockFactory.CreateLock();
    private StreamWriter? _writer;
    private long _currentFileSize;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RivuletFileListener" /> class.
    /// </summary>
    /// <param name="filePath">The path to the log file.</param>
    /// <param name="maxFileSizeBytes">Maximum file size before rotation. Default is 10MB.</param>
    public RivuletFileListener(string filePath, long maxFileSizeBytes = 10 * 1024 * 1024)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _maxFileSizeBytes = maxFileSizeBytes;
        InitializeWriter();
    }

    /// <summary>
    ///     Called when a counter value is received.
    /// </summary>
    protected override void OnCounterReceived(
        string name,
        string displayName,
        double value,
        string displayUnits) =>
        LockHelper.Execute(_lock,
            () =>
            {
                CheckRotation();

                var timestamp = DateTime.UtcNow.ToString(RivuletDiagnosticsConstants.DateTimeFormats.File);
                var formattedValue = string.IsNullOrEmpty(displayUnits)
                    ? $"{value:F2}"
                    : $"{value:F2} {displayUnits}";

                var line = $"[{timestamp}] {displayName}: {formattedValue}";
                _writer?.WriteLine(line);
                _writer?.Flush();

                _currentFileSize += Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;
            });

    private void InitializeWriter()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

        _writer = new(_filePath, true, Encoding.UTF8) { AutoFlush = false };

        if (File.Exists(_filePath)) _currentFileSize = new FileInfo(_filePath).Length;
    }

    private void CheckRotation()
    {
        if (_currentFileSize < _maxFileSizeBytes) return;

        _writer?.Flush();
        _writer?.Dispose();
        _writer = null;

        var timestamp = DateTime.UtcNow.ToString(RivuletDiagnosticsConstants.DateTimeFormats.FileRotation);
        var directory = Path.GetDirectoryName(_filePath) ?? string.Empty;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(_filePath);
        var extension = Path.GetExtension(_filePath);
        var rotatedFileName = Path.Join(directory, $"{fileNameWithoutExtension}-{timestamp}{extension}");

        // Retry file move with brief delays if needed (OS may still have handle open)
        FileOperationRetryHelper.ExecuteWithRetry(() => File.Move(_filePath, rotatedFileName));

        InitializeWriter();
    }

    /// <summary>
    ///     Disposes the file listener and closes the file.
    /// </summary>
    public override void Dispose()
    {
        StreamWriterDisposalHelper.ExtractAndDispose(_lock,
            () =>
            {
                var w = _writer;
                _writer = null;
                return w;
            });

        base.Dispose();
    }

    /// <summary>
    ///     Disposes the file listener asynchronously and closes the file.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        // Extract writer under lock to ensure thread-safety and dispose outside lock to avoid holding lock during async I/O
        await StreamWriterDisposalHelper.ExtractAndDisposeAsync(_lock,
            () =>
            {
                var w = _writer;
                _writer = null;
                return w;
            }).ConfigureAwait(false);

        base.Dispose();
    }
}