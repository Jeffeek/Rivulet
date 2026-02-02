using Rivulet.Core;

namespace Rivulet.Pipeline;

/// <summary>
/// Configuration options for a single pipeline stage.
/// </summary>
public sealed class StageOptions
{
    /// <summary>
    /// Gets the parallel processing options for this stage.
    /// If null, inherits from pipeline defaults.
    /// </summary>
    public ParallelOptionsRivulet? ParallelOptions { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="StageOptions"/> class with default values.
    /// </summary>
    public StageOptions() { }

    /// <summary>
    /// Merges this stage's options with pipeline defaults.
    /// Stage-specific options take precedence over pipeline defaults.
    /// </summary>
    /// <param name="pipelineDefaults">The pipeline's default parallel options.</param>
    /// <returns>The merged parallel options to use for this stage.</returns>
    internal ParallelOptionsRivulet GetMergedOptions(ParallelOptionsRivulet pipelineDefaults) =>
        ParallelOptions ?? pipelineDefaults;
}
