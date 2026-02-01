namespace Rivulet.Pipeline.Internal;

/// <summary>
/// Adapts a user-provided IPipelineStage to the internal IInternalPipelineStage interface.
/// </summary>
internal sealed class CustomStageAdapter<TIn, TOut>(
    IPipelineStage<TIn, TOut> userStage,
    StageOptions options
) : IInternalPipelineStage, IPipelineStage<TIn, TOut>
{
    private readonly IPipelineStage<TIn, TOut> _userStage = userStage ?? throw new ArgumentNullException(nameof(userStage));

    public string Name => _userStage.Name;
    public StageOptions Options { get; } = options ?? throw new ArgumentNullException(nameof(options));

    public IAsyncEnumerable<TOut> ExecuteAsync(
        IAsyncEnumerable<TIn> input,
        PipelineContext context,
        CancellationToken cancellationToken
    ) => _userStage.ExecuteAsync(input, context, cancellationToken);

    public IAsyncEnumerable<object> ExecuteUntypedAsync(
        IAsyncEnumerable<object> input,
        PipelineContext context,
        CancellationToken cancellationToken
    ) => StageExecutionHelper.ExecuteUntypedAsync<TIn, TOut>(
        input,
        typedInput => ExecuteAsync(typedInput, context, cancellationToken),
        cancellationToken);
}
