using Rivulet.Core;
using Rivulet.Pipeline.Results;

namespace Rivulet.Pipeline;

/// <summary>
/// Configuration options for the entire pipeline.
/// </summary>
public sealed class PipelineOptions
{
    /// <summary>
    /// Gets the pipeline name for diagnostics and tracing.
    /// Defaults to "Pipeline".
    /// </summary>
    public string Name { get; init; } = "Pipeline";

    /// <summary>
    /// Gets the default parallel options applied to all stages unless overridden.
    /// This includes concurrency, retries, circuit breaker, rate limiting, etc.
    /// </summary>
    // ReSharper disable once MemberCanBeInternal
    public ParallelOptionsRivulet DefaultStageOptions { get; init; } = new();

    /// <summary>
    /// Gets a callback invoked when the pipeline starts execution.
    /// Receives the pipeline context.
    /// </summary>
    public Func<PipelineContext, ValueTask>? OnPipelineStartAsync { get; init; }

    /// <summary>
    /// Gets a callback invoked when the pipeline completes execution.
    /// Receives the pipeline context and result summary.
    /// </summary>
    public Func<PipelineContext, PipelineResult, ValueTask>? OnPipelineCompleteAsync { get; init; }

    /// <summary>
    /// Gets a callback invoked when a stage starts execution.
    /// Receives the stage name and stage index.
    /// </summary>
    public Func<string, int, ValueTask>? OnStageStartAsync { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineOptions"/> class with default values.
    /// </summary>
    public PipelineOptions() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineOptions"/> class by copying values from another instance.
    /// </summary>
    /// <param name="original">The original instance to copy from. If null, default values are used.</param>
    internal PipelineOptions(PipelineOptions? original)
    {
        if (original is null)
            return;

        Name = original.Name;
        DefaultStageOptions = new ParallelOptionsRivulet(original.DefaultStageOptions);
        OnPipelineStartAsync = original.OnPipelineStartAsync;
        OnPipelineCompleteAsync = original.OnPipelineCompleteAsync;
        OnStageStartAsync = original.OnStageStartAsync;
    }
}
