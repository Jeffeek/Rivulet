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

    /// <summary>
    ///     Initializes a new instance of the <see cref="ProgressOptions"/> class with default values.
    /// </summary>
    public ProgressOptions() { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ProgressOptions"/> class by copying values from another instance.
    /// </summary>
    /// <param name="original">The original instance to copy from. If null, default values are used.</param>
    // ReSharper disable once MemberCanBeInternal
    public ProgressOptions(ProgressOptions? original)
    {
        if (original is null)
            return;

        ReportInterval = original.ReportInterval;
        OnProgress = original.OnProgress;
    }
}
