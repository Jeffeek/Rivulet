using System.Runtime.CompilerServices;
using Rivulet.Core;

namespace Rivulet.Pipeline.Internal.Stages;

/// <summary>
/// A stage that batches items and transforms each batch in parallel.
/// </summary>
internal sealed class BatchSelectStage<TIn, TOut>(
    int batchSize,
    Func<IReadOnlyList<TIn>, CancellationToken, ValueTask<TOut>> batchSelector,
    TimeSpan? flushTimeout,
    StageOptions options,
    string name
) : PipelineStageBase<TIn, TOut>(name, options)
{
    private readonly Func<IReadOnlyList<TIn>, CancellationToken, ValueTask<TOut>> _batchSelector = batchSelector ?? throw new ArgumentNullException(nameof(batchSelector));

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
            var batchStage = new BatchStage<TIn>(batchSize, flushTimeout, $"{Name}_Batch");
            var batches = batchStage.ExecuteAsync(input, context, cancellationToken);

            await foreach (var result in batches
                               .SelectParallelStreamAsync(_batchSelector, parallelOptions, cancellationToken)
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
