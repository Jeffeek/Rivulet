using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Rivulet.Core;

namespace Rivulet.Hosting.Sample;

/// <summary>
///     Example background service that processes items from a channel using Rivulet
/// </summary>
public sealed class QueueProcessingWorker : ParallelBackgroundService<string>
{
    private readonly Channel<string> _queue;

    public QueueProcessingWorker(
        IOptions<ParallelOptionsRivulet> options,
        ILogger<QueueProcessingWorker> logger
    ) : base(logger, options.Value)
    {
        _queue = Channel.CreateUnbounded<string>();

        // Start a producer task to simulate incoming work
        _ = Task.Run(ProduceWorkItemsAsync);
    }

    private async Task ProduceWorkItemsAsync()
    {
        var itemCount = 0;
        while (true)
        {
            try
            {
                await Task.Delay(2000);

                // Produce a batch of items
                var batchSize = Random.Shared.Next(5, 15);
                for (var i = 0; i < batchSize; i++)
                {
                    var item = $"QueueItem-{Interlocked.Increment(ref itemCount)}";
                    await _queue.Writer.WriteAsync(item);
                }
            }
            catch (Exception)
            {
                // Producer task ending
                break;
            }
        }
    }

    protected override IAsyncEnumerable<string> GetItemsAsync(CancellationToken cancellationToken) =>
        _queue.Reader.ReadAllAsync(cancellationToken);

    protected override async ValueTask ProcessItemAsync(string item, CancellationToken cancellationToken)
    {
        // Simulate processing work
        await Task.Delay(Random.Shared.Next(200, 800), cancellationToken);

        // Simulate occasional failures
        if (Random.Shared.Next(0, 100) < 5) // 5% error rate
            throw new InvalidOperationException($"Failed to process {item}");
    }
}
