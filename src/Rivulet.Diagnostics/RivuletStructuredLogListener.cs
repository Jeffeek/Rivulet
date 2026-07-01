using System.Text.Json;
using Rivulet.Core;
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
public sealed class RivuletStructuredLogListener : FileEventListenerBase
{
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Action<string>? _logAction;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RivuletStructuredLogListener" /> class that writes to a file.
    /// </summary>
    /// <param name="filePath">The path to the JSON lines log file.</param>
    /// <param name="writeIndented">Whether to format JSON with indentation. Default is false for compact output.</param>
    public RivuletStructuredLogListener(string filePath, bool writeIndented = false)
    {
        var filePath1 = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _jsonOptions = new() { WriteIndented = writeIndented };
        InitializeWriter(filePath1);
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
    protected override void OnCounterReceived(
        string name,
        string displayName,
        double value,
        string displayUnits
    )
    {
        var logEntry = new
        {
            timestamp = DateTime.UtcNow,
            source = RivuletSharedConstants.RivuletCore,
            metric = new { name, displayName, value, displayUnits }
        };

        var json = JsonSerializer.Serialize(logEntry, _jsonOptions);

        ExecuteUnderLock(() =>
        {
            if (Writer != null)
            {
                Writer.WriteLine(json);
                Writer.Flush();
            }
            else
                _logAction?.Invoke(json);
        });
    }
}
