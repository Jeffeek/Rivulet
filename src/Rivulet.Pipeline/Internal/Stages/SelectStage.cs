using System.Runtime.CompilerServices;
using Rivulet.Core;

namespace Rivulet.Pipeline.Internal.Stages;

/// <summary>
/// A stage that transforms each item in parallel using the provided selector.
/// </summary>
internal sealed class SelectStage<TIn, TOut>(
    Func<TIn, CancellationToken, ValueTask<TOut>> selector,
    StageOptions options,
    string name
) : PipelineStageBase<TIn, TOut>(name, options)
{
    private readonly Func<TIn, CancellationToken, ValueTask<TOut>> _selector = selector ?? throw new ArgumentNullException(nameof(selector));

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
            await foreach (var result in input
                               .SelectParallelStreamAsync(_selector, parallelOptions, cancellationToken)
                               .ConfigureAwait(false))
            {
                metrics.IncrementItemsOut();
                yield return result;
            }
        }
        finally
        {
            metrics.Stop();
        }
    }
}
