using System.Text;
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
public sealed class RivuletFileListener : FileEventListenerBase
{
    private readonly string _filePath;
    private readonly long _maxFileSizeBytes;
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
        InitializeWriterInternal();
    }

    /// <summary>
    ///     Called when a counter value is received.
    /// </summary>
    protected override void OnCounterReceived(
        string name,
        string displayName,
        double value,
        string displayUnits
    ) =>
        ExecuteUnderLock(() =>
        {
            CheckRotation();

            var timestamp = DateTime.UtcNow.ToString(RivuletDiagnosticsConstants.DateTimeFormats.File);
            var formattedValue = string.IsNullOrEmpty(displayUnits)
                ? $"{value:F2}"
                : $"{value:F2} {displayUnits}";

            var line = $"[{timestamp}] {displayName}: {formattedValue}";
            Writer?.WriteLine(line);
            Writer?.Flush();

            _currentFileSize += Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;
        });

    private void InitializeWriterInternal()
    {
        // Read size before creating the writer — the constructor creates the file if absent,
        // which would make a post-creation File.Exists check always true
        _currentFileSize = File.Exists(_filePath) ? new FileInfo(_filePath).Length : 0;
        InitializeWriter(_filePath);
    }

    private void CheckRotation()
    {
        if (_currentFileSize < _maxFileSizeBytes) return;

        Writer?.Flush();
        Writer?.Dispose();
        Writer = null;

        var timestamp = DateTime.UtcNow.ToString(RivuletDiagnosticsConstants.DateTimeFormats.FileRotation);
        var directory = Path.GetDirectoryName(_filePath) ?? string.Empty;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(_filePath);
        var extension = Path.GetExtension(_filePath);
        var rotatedFileName = Path.Join(directory, $"{fileNameWithoutExtension}-{timestamp}{extension}");

        // Retry file move with brief delays if needed (OS may still have handle open)
        FileOperationRetryHelper.ExecuteWithRetry(() => File.Move(_filePath, rotatedFileName));

        InitializeWriterInternal();
    }
}
