using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Rivulet.Core;

namespace Rivulet.Pipeline.Internal.Stages;

internal sealed class BufferStage<T>(int capacity, string name)
    : PipelineStageBase<T, T>(name, new StageOptions())
{
    public override async IAsyncEnumerable<T> ExecuteAsync(
        IAsyncEnumerable<T> input,
        PipelineContext context,
        [EnumeratorCancellation]
        CancellationToken cancellationToken
    )
    {
        var metrics = context.GetStageMetrics(Name);
        metrics.Start();

        var channel = Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        });

        var producerTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var item in input.WithCancellation(cancellationToken).ConfigureAwait(false))
                    {
                        metrics.IncrementItemsIn();
                        await channel.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
                    }
                }
                finally
                {
                    channel.Writer.TryComplete();
                }
            },
            cancellationToken);

        try
        {
            await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                metrics.IncrementItemsOut();
                yield return item;
            }

            await producerTask.ConfigureAwait(false);
        }
        finally
        {
            metrics.Stop();
        }
    }

    protected override IAsyncEnumerable<T> ExecuteCoreAsync(
        IAsyncEnumerable<T> _,
        ParallelOptionsRivulet __,
        PipelineContext ___,
        CancellationToken ____
    ) => throw new NotSupportedException();
}
