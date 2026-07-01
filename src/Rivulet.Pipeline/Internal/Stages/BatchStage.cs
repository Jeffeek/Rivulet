using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Rivulet.Pipeline.Internal.Stages;

/// <summary>
/// A stage that groups items into fixed-size batches.
/// </summary>
internal sealed class BatchStage<T>(int batchSize, TimeSpan? flushTimeout, string name)
    : PipelineStageBase<T, IReadOnlyList<T>>(name, new StageOptions())
{
    public override async IAsyncEnumerable<IReadOnlyList<T>> ExecuteAsync(
        IAsyncEnumerable<T> input,
        PipelineContext context,
        [EnumeratorCancellation]
        CancellationToken cancellationToken
    )
    {
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

    private async IAsyncEnumerable<IReadOnlyList<T>> ExecuteWithTimeoutAsync(
        IAsyncEnumerable<T> input,
        [EnumeratorCancellation]
        CancellationToken cancellationToken
    )
    {
        var timeout = flushTimeout!.Value;

        var channel = Channel.CreateBounded<IReadOnlyList<T>>(new BoundedChannelOptions(16)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        });

        var producerTask = ProduceBatchesAsync(input, channel.Writer, timeout, cancellationToken);

        await foreach (var batch in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            yield return batch;

        await producerTask.ConfigureAwait(false);
    }

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

                var batchFull = batch.Count >= batchSize;
                var timeoutExpired = flushTimer.IsCompleted && batch.Count > 0;

                if (!batchFull && !timeoutExpired)
                    continue;

                await writer.WriteAsync(batch, cancellationToken).ConfigureAwait(false);
                batch = new List<T>(batchSize);
                flushTimer = Task.Delay(timeout, cancellationToken);
            }

            if (batch.Count > 0)
                await writer.WriteAsync(batch, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            writer.TryComplete();
        }
    }
}
