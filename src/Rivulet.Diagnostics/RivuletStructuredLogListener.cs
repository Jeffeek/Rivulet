using System.Text;
using System.Text.Json;
using Rivulet.Core;
using Rivulet.Core.Internal;
using Rivulet.Diagnostics.Internal;

namespace Rivulet.Diagnostics;

/// <summary>
///     EventListener that writes Rivulet metrics in structured JSON format.
///     Ideal for log aggregation systems like ELK, Splunk, or Azure Monitor.
/// </summary>
/// <example>
///     <code>
/// using var listener = new RivuletStructuredLogListener("metrics.json");
/// 
/// await Enumerable.Range(1, 100)
///     .ToAsyncEnumerable()
///     .SelectParallelAsync(async x => await ProcessAsync(x), new ParallelOptionsRivulet
///     {
///         MaxDegreeOfParallelism = 10
///     });
/// </code>
/// </example>
public sealed class RivuletStructuredLogListener : RivuletEventListenerBase, IAsyncDisposable
{
    private readonly string? _filePath;
    private readonly Action<string>? _logAction;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly object _lock = LockFactory.CreateLock();
    private StreamWriter? _writer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RivuletStructuredLogListener" /> class that writes to a file.
    /// </summary>
    /// <param name="filePath">The path to the JSON lines log file.</param>
    /// <param name="writeIndented">Whether to format JSON with indentation. Default is false for compact output.</param>
    public RivuletStructuredLogListener(string filePath, bool writeIndented = false)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _jsonOptions = new() { WriteIndented = writeIndented };
        InitializeWriter();
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="RivuletStructuredLogListener" /> class with a custom log action.
    /// </summary>
    /// <param name="logAction">Action to invoke with each JSON log line.</param>
    /// <param name="writeIndented">Whether to format JSON with indentation. Default is false for compact output.</param>
    // ReSharper disable once MemberCanBeInternal
    public RivuletStructuredLogListener(Action<string> logAction, bool writeIndented = false)
    {
        _logAction = logAction ?? throw new ArgumentNullException(nameof(logAction));
        _jsonOptions = new() { WriteIndented = writeIndented };
    }

    /// <summary>
    ///     Called when a counter value is received.
    /// </summary>
    protected override void OnCounterReceived(string name,
        string displayName,
        double value,
        string displayUnits)
    {
        var logEntry = new
        {
            timestamp = DateTime.UtcNow,
            source = RivuletSharedConstants.RivuletCore,
            metric = new { name, displayName, value, displayUnits }
        };

        var json = JsonSerializer.Serialize(logEntry, _jsonOptions);

        LockHelper.Execute(_lock,
            () =>
            {
                if (_writer != null)
                {
                    _writer.WriteLine(json);
                    _writer.Flush();
                }
                else
                    _logAction?.Invoke(json);
            });
    }

    private void InitializeWriter()
    {
        if (_filePath == null) return;

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

        _writer = new(_filePath, true, Encoding.UTF8) { AutoFlush = false };
    }

    /// <summary>
    ///     Disposes the structured log listener and closes the file if applicable.
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
    ///     Disposes the structured log listener asynchronously and closes the file if applicable.
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