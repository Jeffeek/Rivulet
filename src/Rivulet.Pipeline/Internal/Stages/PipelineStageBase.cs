namespace Rivulet.Pipeline.Internal.Stages;

/// <summary>
/// Base class for pipeline stages that share common property and untyped execution patterns.
/// </summary>
/// <typeparam name="TIn">The input type for this stage.</typeparam>
/// <typeparam name="TOut">The output type for this stage.</typeparam>
internal abstract class PipelineStageBase<TIn, TOut>(string name, StageOptions options) : IInternalPipelineStage, IPipelineStage<TIn, TOut>
{
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));
    public StageOptions Options { get; } = options ?? throw new ArgumentNullException(nameof(options));

    public abstract IAsyncEnumerable<TOut> ExecuteAsync(
        IAsyncEnumerable<TIn> input,
        PipelineContext context,
        CancellationToken cancellationToken
    );

    public IAsyncEnumerable<object> ExecuteUntypedAsync(
        IAsyncEnumerable<object> input,
        PipelineContext context,
        CancellationToken cancellationToken
    ) => StageExecutionHelper.ExecuteUntypedAsync<TIn, TOut>(
        input,
        typedInput => ExecuteAsync(typedInput, context, cancellationToken),
        cancellationToken);
}
