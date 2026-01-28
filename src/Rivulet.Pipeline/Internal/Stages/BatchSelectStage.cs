using System.Runtime.CompilerServices;
using Rivulet.Core;

namespace Rivulet.Pipeline.Internal.Stages;

/// <summary>
/// A stage that batches items and transforms each batch in parallel.
/// </summary>
internal sealed class BatchSelectStage<TIn, TOut>(int batchSize,
    Func<IReadOnlyList<TIn>, CancellationToken, ValueTask<TOut>> batchSelector,
    TimeSpan? flushTimeout,
    StageOptions options,
    string name
) : IInternalPipelineStage, IPipelineStage<TIn, TOut>
{
    private readonly Func<IReadOnlyList<TIn>, CancellationToken, ValueTask<TOut>> _batchSelector = batchSelector ?? throw new ArgumentNullException(nameof(batchSelector));

    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));
    public StageOptions Options { get; } = options ?? throw new ArgumentNullException(nameof(options));

    public async IAsyncEnumerable<TOut> ExecuteAsync(
        IAsyncEnumerable<TIn> input,
        PipelineContext context,
        [EnumeratorCancellation]
        CancellationToken cancellationToken
    )
    {
        var parallelOptions = Options.GetMergedOptions(context.DefaultStageOptions);
        var metrics = context.GetOrCreateStageMetrics(Name, 0);

        metrics.Start();

        try
        {
            // First batch the input
            var batchStage = new BatchStage<TIn>(batchSize, flushTimeout, $"{Name}_Batch");
            var batches = batchStage.ExecuteAsync(input, context, cancellationToken);

            // Then process batches in parallel
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

    public IAsyncEnumerable<object> ExecuteUntypedAsync(
        IAsyncEnumerable<object> input,
        PipelineContext context,
        CancellationToken cancellationToken
    ) => StageExecutionHelper.ExecuteUntypedAsync<TIn, TOut>(
        input,
        typedInput => ExecuteAsync(typedInput, context, cancellationToken),
        cancellationToken);
}
