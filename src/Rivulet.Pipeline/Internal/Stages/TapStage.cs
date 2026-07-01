using System.Runtime.CompilerServices;
using Rivulet.Core;

namespace Rivulet.Pipeline.Internal.Stages;

internal sealed class TapStage<T>(
    Func<T, CancellationToken, ValueTask> action,
    StageOptions options,
    string name
) : PipelineStageBase<T, T>(name, options)
{
    protected override async IAsyncEnumerable<T> ExecuteCoreAsync(
        IAsyncEnumerable<T> input,
        ParallelOptionsRivulet parallelOptions,
        PipelineContext context,
        [EnumeratorCancellation]
        CancellationToken cancellationToken
    )
    {
        await foreach (var item in input
                           .SelectParallelStreamAsync(
                               async (item, ct) =>
                               {
                                   await action(item, ct).ConfigureAwait(false);
                                   return item;
                               },
                               parallelOptions,
                               cancellationToken)
                           .ConfigureAwait(false))
            yield return item;
    }
}
