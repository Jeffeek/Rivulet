using Rivulet.Core.Internal;

namespace Rivulet.Diagnostics;

/// <summary>
/// EventListener that writes Rivulet metrics to a file.
/// Supports automatic file rotation based on size or time.
/// </summary>
/// <example>
/// <code>
/// using var listener = new RivuletFileListener("metrics.log", maxFileSizeBytes: 10 * 1024 * 1024);
/// 
/// await Enumerable.Range(1, 100)
///     .ToAsyncEnumerable()
///     .SelectParallelAsync(async x => await ProcessAsync(x), new ParallelOptionsRivulet
///     {
///         MaxDegreeOfParallelism = 10
///     });
/// </code>
/// </example>
public sealed class RivuletFileListener : RivuletEventListenerBase
{
    private readonly string _filePath;
    private readonly long _maxFileSizeBytes;
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif
    private StreamWriter? _writer;
    private long _currentFileSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="RivuletFileListener"/> class.
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
    /// Called when a counter value is received.
    /// </summary>
    protected override void OnCounterReceived(string name, string displayName, double value, string displayUnits)
    {
        LockHelper.Execute(_lock, () =>
        {
            CheckRotation();

            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var formattedValue = string.IsNullOrEmpty(displayUnits)
                ? $"{value:F2}"
                : $"{value:F2} {displayUnits}";

            var line = $"[{timestamp}] {displayName}: {formattedValue}";
            _writer?.WriteLine(line);
            _writer?.Flush();

            _currentFileSize += System.Text.Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;
        });
    }

    private void InitializeWriter()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(_filePath, append: true, System.Text.Encoding.UTF8)
        {
            AutoFlush = false
        };

        if (File.Exists(_filePath))
        {
            _currentFileSize = new FileInfo(_filePath).Length;
        }
    }

    private void CheckRotation()
    {
        if (_currentFileSize < _maxFileSizeBytes)
            return;

        _writer?.Flush();
        _writer?.Dispose();
        _writer = null;

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var directory = Path.GetDirectoryName(_filePath) ?? string.Empty;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(_filePath);
        var extension = Path.GetExtension(_filePath);
        var rotatedFileName = Path.Join(directory, $"{fileNameWithoutExtension}-{timestamp}{extension}");

        // Retry file move with brief delays if needed (OS may still have handle open)
        var retries = 3;
        for (var i = 0; i < retries; i++)
        {
            try
            {
                File.Move(_filePath, rotatedFileName);
                break;
            }
            catch (IOException) when (i < retries - 1)
            {
                // Brief delay to allow OS to release file handle
                Thread.Sleep(10);
            }
        }

        InitializeWriter();
    }

    /// <summary>
    /// Disposes the file listener and closes the file.
    /// </summary>
    public override void Dispose()
    {
        LockHelper.Execute(_lock, () =>
        {
            _writer?.Flush();
            _writer?.Close();
            _writer?.Dispose();
            _writer = null;
        });

        base.Dispose();
    }
}
