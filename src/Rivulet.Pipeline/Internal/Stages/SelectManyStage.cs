using System.Runtime.CompilerServices;
using Rivulet.Core;

namespace Rivulet.Pipeline.Internal.Stages;

internal sealed class SelectManyStage<TIn, TOut>(
    Func<TIn, CancellationToken, ValueTask<IEnumerable<TOut>>> selector,
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
        await foreach (var collection in input
                           .SelectParallelStreamAsync(selector, parallelOptions, cancellationToken)
                           .ConfigureAwait(false))
        {
            foreach (var item in collection)
                yield return item;
        }
    }
}
