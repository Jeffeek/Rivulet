using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Rivulet.Pipeline.Internal.Stages;

/// <summary>
/// A stage that buffers items to decouple upstream and downstream processing.
/// </summary>
internal sealed class BufferStage<T>(int capacity, string name) : IInternalPipelineStage, IPipelineStage<T, T>
{
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));
    public StageOptions Options { get; } = new();

    public async IAsyncEnumerable<T> ExecuteAsync(
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

    public IAsyncEnumerable<object> ExecuteUntypedAsync(
        IAsyncEnumerable<object> input,
        PipelineContext context,
        CancellationToken cancellationToken
    ) => StageExecutionHelper.ExecuteUntypedAsync<T, T>(
        input,
        typedInput => ExecuteAsync(typedInput, context, cancellationToken),
        cancellationToken);
}
