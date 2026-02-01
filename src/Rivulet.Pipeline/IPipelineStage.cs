namespace Rivulet.Pipeline;

/// <summary>
/// Represents a processing stage in a pipeline that transforms input to output.
/// </summary>
/// <typeparam name="TIn">The input type for this stage.</typeparam>
/// <typeparam name="TOut">The output type for this stage.</typeparam>
// ReSharper disable once MemberCanBeInternal
public interface IPipelineStage<in TIn, out TOut>
{
    /// <summary>
    /// Gets the stage name for diagnostics and tracing.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes this stage, processing input items and yielding output items.
    /// </summary>
    /// <param name="input">The input async enumerable to process.</param>
    /// <param name="context">The pipeline execution context.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>An async enumerable of processed results.</returns>
    // ReSharper disable once UnusedMemberInSuper.Global
    IAsyncEnumerable<TOut> ExecuteAsync(
        IAsyncEnumerable<TIn> input,
        PipelineContext context,
        CancellationToken cancellationToken
    );
}
