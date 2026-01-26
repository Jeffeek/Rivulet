namespace Rivulet.Pipeline.Results;

/// <summary>
/// Represents the result summary of a pipeline execution.
/// </summary>
public sealed class PipelineResult
{
    /// <summary>
    /// Gets the total number of items that entered the pipeline.
    /// </summary>
    public long ItemsProcessed { get; init; }

    /// <summary>
    /// Gets the total number of items that completed successfully through all stages.
    /// </summary>
    public long ItemsCompleted { get; init; }

    /// <summary>
    /// Gets the total number of items that failed during processing.
    /// </summary>
    public long ItemsFailed { get; init; }

    /// <summary>
    /// Gets the total elapsed time for the pipeline execution.
    /// </summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>
    /// Gets the average throughput in items per second.
    /// </summary>
    public double ItemsPerSecond => Elapsed.TotalSeconds > 0
        ? ItemsCompleted / Elapsed.TotalSeconds
        : 0;

    /// <summary>
    /// Gets a value indicating whether the pipeline completed successfully without errors.
    /// </summary>
    public bool IsSuccess => ItemsFailed == 0;

    /// <summary>
    /// Gets the results for each individual stage.
    /// </summary>
    public IReadOnlyList<StageResult> StageResults { get; init; } = [];
}
