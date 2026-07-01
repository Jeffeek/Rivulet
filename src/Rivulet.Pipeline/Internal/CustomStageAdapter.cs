using Rivulet.Core;
using Rivulet.Pipeline.Internal.Stages;

namespace Rivulet.Pipeline.Internal;

/// <summary>
/// Adapts a user-provided IPipelineStage to the internal IInternalPipelineStage interface.
/// </summary>
internal sealed class CustomStageAdapter<TIn, TOut>(
    IPipelineStage<TIn, TOut> userStage,
    StageOptions options
) : PipelineStageBase<TIn, TOut>(userStage.Name, options)
{
    private readonly IPipelineStage<TIn, TOut> _userStage = userStage ?? throw new ArgumentNullException(nameof(userStage));

    public override IAsyncEnumerable<TOut> ExecuteAsync(
        IAsyncEnumerable<TIn> input,
        PipelineContext context,
        CancellationToken cancellationToken
    ) => _userStage.ExecuteAsync(input, context, cancellationToken);

    protected override IAsyncEnumerable<TOut> ExecuteCoreAsync(
        IAsyncEnumerable<TIn> _,
        ParallelOptionsRivulet __,
        PipelineContext ___,
        CancellationToken ____
    ) => throw new NotSupportedException();
}
