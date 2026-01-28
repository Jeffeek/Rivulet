namespace Rivulet.Pipeline.Results;

/// <summary>
/// Represents the result summary of a single stage execution.
/// </summary>
public sealed class StageResult
{
    /// <summary>
    /// Gets the stage name.
    /// </summary>
    public required string StageName { get; init; }

    /// <summary>
    /// Gets the stage index in the pipeline.
    /// </summary>
    public int StageIndex { get; init; }

    /// <summary>
    /// Gets the number of items that entered this stage.
    /// </summary>
    public long ItemsIn { get; init; }

    /// <summary>
    /// Gets the number of items that exited this stage.
    /// May differ from ItemsIn for filter or flatten stages.
    /// </summary>
    public long ItemsOut { get; init; }

    /// <summary>
    /// Gets the number of items that failed in this stage.
    /// </summary>
    public long ItemsFailed { get; init; }

    /// <summary>
    /// Gets the total number of retry attempts in this stage.
    /// </summary>
    public long TotalRetries { get; init; }

    /// <summary>
    /// Gets the elapsed time for this stage.
    /// </summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>
    /// Gets the average throughput for this stage in items per second.
    /// </summary>
    public double ItemsPerSecond => Elapsed.TotalSeconds > 0
        ? ItemsOut / Elapsed.TotalSeconds
        : 0;
}
