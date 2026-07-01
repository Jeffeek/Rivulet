using System.Runtime.CompilerServices;
using Rivulet.Core;

namespace Rivulet.Pipeline.Internal.Stages;

/// <summary>
/// A stage that filters items in parallel using the provided predicate.
/// </summary>
internal sealed class FilterStage<T>(
    Func<T, CancellationToken, ValueTask<bool>> predicate,
    StageOptions options,
    string name
) : PipelineStageBase<T, T>(name, options)
{
    private readonly Func<T, CancellationToken, ValueTask<bool>> _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));

    public override async IAsyncEnumerable<T> ExecuteAsync(
        IAsyncEnumerable<T> input,
        PipelineContext context,
        [EnumeratorCancellation]
        CancellationToken cancellationToken
    )
    {
        var parallelOptions = Options.GetMergedOptions(context.DefaultStageOptions);
        var metrics = context.GetStageMetrics(Name);

        metrics.Start();

        try
        {
            await foreach (var (item, keep) in input
                               .SelectParallelStreamAsync(
                                   async (item, ct) => (item, keep: await _predicate(item, ct).ConfigureAwait(false)),
                                   parallelOptions,
                                   cancellationToken)
                               .ConfigureAwait(false))
            {
                metrics.IncrementItemsIn();

                if (!keep)
                    continue;

                metrics.IncrementItemsOut();
                yield return item;
            }
        }
        finally
        {
            metrics.Stop();
        }
    }
}
