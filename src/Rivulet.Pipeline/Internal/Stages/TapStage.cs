using System.Runtime.CompilerServices;
using Rivulet.Core;

namespace Rivulet.Pipeline.Internal.Stages;

/// <summary>
/// A stage that executes a side effect on each item without transforming it.
/// </summary>
internal sealed class TapStage<T>(
    Func<T, CancellationToken, ValueTask> action,
    StageOptions options,
    string name
) : PipelineStageBase<T, T>(name, options)
{
    private readonly Func<T, CancellationToken, ValueTask> _action = action ?? throw new ArgumentNullException(nameof(action));

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
            await foreach (var item in input
                               .SelectParallelStreamAsync(
                                   async (item, ct) =>
                                   {
                                       await _action(item, ct).ConfigureAwait(false);
                                       return item;
                                   },
                                   parallelOptions,
                                   cancellationToken)
                               .ConfigureAwait(false))
            {
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
