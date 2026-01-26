namespace Rivulet.Pipeline;

/// <summary>
/// Represents an executable multi-stage pipeline.
/// </summary>
/// <typeparam name="TIn">The pipeline input type.</typeparam>
/// <typeparam name="TOut">The pipeline output type.</typeparam>
public interface IPipeline<in TIn, TOut>
{
    /// <summary>
    /// Gets the pipeline name for diagnostics.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the number of stages in this pipeline.
    /// </summary>
    int StageCount { get; }

    /// <summary>
    /// Executes the pipeline with streaming output.
    /// Results are yielded as they complete through all stages.
    /// </summary>
    /// <param name="source">The async enumerable source to process.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>An async enumerable of processed results.</returns>
    IAsyncEnumerable<TOut> ExecuteStreamAsync(
        IAsyncEnumerable<TIn> source,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Executes the pipeline and materializes all results into a list.
    /// </summary>
    /// <param name="source">The enumerable source to process.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task containing the list of all processed results.</returns>
    Task<List<TOut>> ExecuteAsync(
        IEnumerable<TIn> source,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Executes the pipeline and materializes all results into a list.
    /// </summary>
    /// <param name="source">The async enumerable source to process.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task containing the list of all processed results.</returns>
    Task<List<TOut>> ExecuteAsync(
        IAsyncEnumerable<TIn> source,
        CancellationToken cancellationToken = default
    );
}
