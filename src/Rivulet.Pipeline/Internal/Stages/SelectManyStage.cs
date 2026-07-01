using System.Runtime.CompilerServices;
using Rivulet.Core;

namespace Rivulet.Pipeline.Internal.Stages;

/// <summary>
/// A stage that flattens collections returned by the selector.
/// </summary>
internal sealed class SelectManyStage<TIn, TOut>(
    Func<TIn, CancellationToken, ValueTask<IEnumerable<TOut>>> selector,
    StageOptions options,
    string name
) : PipelineStageBase<TIn, TOut>(name, options)
{
    private readonly Func<TIn, CancellationToken, ValueTask<IEnumerable<TOut>>> _selector = selector ?? throw new ArgumentNullException(nameof(selector));

    public override async IAsyncEnumerable<TOut> ExecuteAsync(
        IAsyncEnumerable<TIn> input,
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
            await foreach (var collection in input
                               .SelectParallelStreamAsync(_selector, parallelOptions, cancellationToken)
                               .ConfigureAwait(false))
            {
                foreach (var item in collection)
                {
                    metrics.IncrementItemsOut();
                    yield return item;
                }
            }
        }
        finally
        {
            metrics.Stop();
        }
    }
}
