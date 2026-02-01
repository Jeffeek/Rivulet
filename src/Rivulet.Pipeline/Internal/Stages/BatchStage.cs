using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Rivulet.Pipeline.Internal.Stages;

/// <summary>
/// A stage that groups items into fixed-size batches.
/// </summary>
internal sealed class BatchStage<T>(int batchSize, TimeSpan? flushTimeout, string name)
    : IInternalPipelineStage, IPipelineStage<T, IReadOnlyList<T>>
{
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));
    public StageOptions Options { get; } = new();

    public async IAsyncEnumerable<IReadOnlyList<T>> ExecuteAsync(
        IAsyncEnumerable<T> input,
        PipelineContext context,
        [EnumeratorCancellation]
        CancellationToken cancellationToken
    )
    {
        // Try to get metrics - may be null if this stage is used internally
        var metrics = context.TryGetStageMetrics(Name);
        metrics?.Start();

        try
        {
            if (flushTimeout.HasValue)
            {
                await foreach (var batch in ExecuteWithTimeoutAsync(input, cancellationToken).ConfigureAwait(false))
                {
                    metrics?.IncrementItemsOut();
                    yield return batch;
                }
            }
            else
            {
                await foreach (var batch in ExecuteSimpleAsync(input, cancellationToken).ConfigureAwait(false))
                {
                    metrics?.IncrementItemsOut();
                    yield return batch;
                }
            }
        }
        finally
        {
            metrics?.Stop();
        }
    }

    private async IAsyncEnumerable<IReadOnlyList<T>> ExecuteSimpleAsync(
        IAsyncEnumerable<T> input,
        [EnumeratorCancellation]
        CancellationToken cancellationToken
    )
    {
        var batch = new List<T>(batchSize);

        await foreach (var item in input.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            batch.Add(item);

            if (batch.Count < batchSize)
                continue;

            yield return batch;

            batch = new List<T>(batchSize);
        }

        if (batch.Count > 0)
            yield return batch;
    }

    /// <summary>
    /// Batches items with a timeout - flushes when batch is full OR timeout expires.
    /// Uses a channel to coordinate between producer (batching) and consumer (yielding).
    /// </summary>
    private async IAsyncEnumerable<IReadOnlyList<T>> ExecuteWithTimeoutAsync(
        IAsyncEnumerable<T> input,
        [EnumeratorCancellation]
        CancellationToken cancellationToken
    )
    {
        var timeout = flushTimeout!.Value;

        // Channel buffers batches between producer and consumer
        var channel = Channel.CreateBounded<IReadOnlyList<T>>(new BoundedChannelOptions(16)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        });

        // Producer task: accumulate items into batches and flush on size or timeout
        var producerTask = ProduceBatchesAsync(input, channel.Writer, timeout, cancellationToken);

        // Consumer: yield batches as they become available
        await foreach (var batch in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            yield return batch;

        // Ensure producer completes without errors
        await producerTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Accumulates items into batches and writes them to the channel.
    /// Flushes when batch reaches batchSize OR when timeout expires.
    /// </summary>
    private async Task ProduceBatchesAsync(
        IAsyncEnumerable<T> input,
        ChannelWriter<IReadOnlyList<T>> writer,
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var batch = new List<T>(batchSize);
            var flushTimer = Task.Delay(timeout, cancellationToken);

            await foreach (var item in input.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                batch.Add(item);

                // Flush if batch is full OR if timeout expired and we have items
                var batchFull = batch.Count >= batchSize;
                var timeoutExpired = flushTimer.IsCompleted && batch.Count > 0;

                if (!batchFull && !timeoutExpired)
                    continue;

                await writer.WriteAsync(batch, cancellationToken).ConfigureAwait(false);
                batch = new List<T>(batchSize);
                flushTimer = Task.Delay(timeout, cancellationToken);
            }

            // Flush any remaining items
            if (batch.Count > 0)
                await writer.WriteAsync(batch, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            writer.TryComplete();
        }
    }

    public IAsyncEnumerable<object> ExecuteUntypedAsync(
        IAsyncEnumerable<object> input,
        PipelineContext context,
        CancellationToken cancellationToken
    ) => StageExecutionHelper.ExecuteUntypedAsync<T, IReadOnlyList<T>>(
        input,
        typedInput => ExecuteAsync(typedInput, context, cancellationToken),
        cancellationToken);
}
