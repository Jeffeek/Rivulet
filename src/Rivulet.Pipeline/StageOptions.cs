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
    /// Initializes a new instance of the <see cref="StageOptions"/> class by copying values from another instance.
    /// </summary>
    /// <param name="original">The original instance to copy from. If null, default values are used.</param>
    public StageOptions(StageOptions? original)
    {
        if (original is null)
            return;

        ParallelOptions = original.ParallelOptions is not null
            ? new ParallelOptionsRivulet(original.ParallelOptions)
            : null;
    }

    /// <summary>
    /// Merges this stage's options with pipeline defaults.
    /// Stage-specific options take precedence over pipeline defaults.
    /// </summary>
    /// <param name="pipelineDefaults">The pipeline's default parallel options.</param>
    /// <returns>The merged parallel options to use for this stage.</returns>
    internal ParallelOptionsRivulet GetMergedOptions(ParallelOptionsRivulet pipelineDefaults) =>
        ParallelOptions ?? pipelineDefaults;
}
