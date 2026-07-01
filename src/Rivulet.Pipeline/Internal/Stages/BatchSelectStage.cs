using System.Runtime.CompilerServices;
using Rivulet.Core;

namespace Rivulet.Pipeline.Internal.Stages;

internal sealed class BatchSelectStage<TIn, TOut>(
    int batchSize,
    Func<IReadOnlyList<TIn>, CancellationToken, ValueTask<TOut>> batchSelector,
    TimeSpan? flushTimeout,
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
        var batchStage = new BatchStage<TIn>(batchSize, flushTimeout, $"{Name}_Batch");
        var batches = batchStage.ExecuteAsync(input, context, cancellationToken);

        await foreach (var result in batches
                           .SelectParallelStreamAsync(batchSelector, parallelOptions, cancellationToken)
                           .ConfigureAwait(false))
            yield return result;
    }
}
