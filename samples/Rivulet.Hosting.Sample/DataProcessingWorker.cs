using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;
using Rivulet.Core;

namespace Rivulet.Hosting.Sample;

/// <summary>
///     Example background worker that processes data using Rivulet
/// </summary>
public sealed class DataProcessingWorker(
    IOptions<ParallelOptionsRivulet> options,
    ILogger<DataProcessingWorker> logger
) : ParallelWorkerService<string, string>(logger, options.Value)
{
    private int _iterationCount;

    protected override async IAsyncEnumerable<string> GetSourceItems(
        [EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var iteration = Interlocked.Increment(ref _iterationCount);

            // Simulate fetching work items every 5 seconds
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

            // Yield work items for this iteration
            for (var i = 1; i <= 20; i++) yield return $"Item-{iteration}-{i}";
        }
    }

    protected override async Task<string> ProcessAsync(string item, CancellationToken cancellationToken)
    {
        // Simulate processing
        await Task.Delay(Random.Shared.Next(100, 500), cancellationToken);

        // Simulate occasional errors
        return Random.Shared.Next(0, 100) < 10 // 10% error rate
            ? throw new InvalidOperationException($"Simulated error processing {item}")
            : $"Processed-{item}";
    }

    protected override Task OnResultAsync(string result, CancellationToken cancellationToken) =>
        // Log successful processing
        Task.CompletedTask;
}