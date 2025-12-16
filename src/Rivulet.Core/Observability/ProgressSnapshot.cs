namespace Rivulet.Core.Observability;

/// <summary>
///     Represents a snapshot of progress information for a parallel operation at a specific point in time.
/// </summary>
public sealed class ProgressSnapshot
{
    /// <summary>
    ///     Gets the total number of items that have started processing.
    ///     This includes items currently in progress, completed items, and failed items.
    /// </summary>
    public int ItemsStarted { get; init; }

    /// <summary>
    ///     Gets the total number of items that have completed processing successfully.
    /// </summary>
    public int ItemsCompleted { get; init; }

    /// <summary>
    ///     Gets the total number of items to be processed, if known.
    ///     Returns null when the total count is unknown (e.g., unbounded async streams).
    /// </summary>
    public int? TotalItems { get; init; }

    /// <summary>
    ///     Gets the total number of items that encountered errors during processing.
    /// </summary>
    public int ErrorCount { get; init; }

    /// <summary>
    ///     Gets the elapsed time since the operation started.
    /// </summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>
    ///     Gets the average processing rate in items per second.
    ///     Calculated as ItemsCompleted / Elapsed.TotalSeconds.
    ///     Returns 0 if no time has elapsed.
    /// </summary>
    public double ItemsPerSecond { get; init; }

    /// <summary>
    ///     Gets the estimated time remaining to complete all items, if the total count is known.
    ///     Returns null when the total count is unknown or when no items have been completed yet.
    ///     Calculated based on the current processing rate and remaining items.
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; init; }

    /// <summary>
    ///     Gets the percentage of completion (0-100), if the total count is known.
    ///     Returns null when the total count is unknown.
    /// </summary>
    public double? PercentComplete { get; init; }
}