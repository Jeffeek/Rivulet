using System.Runtime.CompilerServices;
using Rivulet.Core;

namespace Rivulet.Pipeline.Internal.Stages;

internal sealed class FilterStage<T>(
    Func<T, CancellationToken, ValueTask<bool>> predicate,
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
        await foreach (var (item, keep) in input
                           .SelectParallelStreamAsync(
                               async (item, ct) => (item, keep: await predicate(item, ct).ConfigureAwait(false)),
                               parallelOptions,
                               cancellationToken)
                           .ConfigureAwait(false))
        {
            var metrics = context.GetStageMetrics(Name);
            metrics.IncrementItemsIn();

            if (!keep)
                continue;

            metrics.IncrementItemsOut();
            yield return item;
        }
    }
}
