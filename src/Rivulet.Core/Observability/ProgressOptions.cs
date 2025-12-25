namespace Rivulet.Core.Observability;

/// <summary>
///     Configuration options for progress reporting during parallel operations.
/// </summary>
public sealed class ProgressOptions
{
    /// <summary>
    ///     Gets the interval at which progress updates are reported.
    ///     Defaults to 5 seconds.
    /// </summary>
    public TimeSpan ReportInterval { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     Gets the callback function invoked when progress is reported.
    ///     Receives a <see cref="ProgressSnapshot" /> containing current progress metrics.
    ///     The callback is invoked periodically based on <see cref="ReportInterval" />.
    /// </summary>
    /// <remarks>
    ///     This callback is executed asynchronously and should not block for extended periods.
    ///     Common uses include logging, updating UI, or sending metrics to monitoring systems.
    ///     The callback is called from a background task and may be invoked from any thread.
    /// </remarks>
    public Func<ProgressSnapshot, ValueTask>? OnProgress { get; init; }
}
