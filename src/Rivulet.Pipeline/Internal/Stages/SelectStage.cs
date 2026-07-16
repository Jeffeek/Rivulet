using System.Runtime.CompilerServices;
using Rivulet.Core;

namespace Rivulet.Pipeline.Internal.Stages;

internal sealed class SelectStage<TIn, TOut>(
    Func<TIn, CancellationToken, ValueTask<TOut>> selector,
    StageOptions options,
    string name
) : PipelineStageBase<TIn, TOut>(name, options)
{
    protected override async IAsyncEnumerable<TOut> ExecuteCoreAsync(
        IAsyncEnumerable<TIn> input,
        ParallelOptionsRivulet parallelOptions,
        PipelineContext context,
        [EnumeratorCancellation]
        CancellationToken cancellationToken
    )
    {
        await foreach (var result in input
                           .SelectParallelStreamAsync(selector, parallelOptions, cancellationToken)
                           .ConfigureAwait(false))
            yield return result;
    }
}
