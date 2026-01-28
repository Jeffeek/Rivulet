using System.Diagnostics.CodeAnalysis;

namespace Rivulet.Pipeline.Tests;

[SuppressMessage("ReSharper", "ArgumentsStyleLiteral")]
public sealed class CancellationTests
{
    [Fact]
    public async Task ExecuteAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();

        var pipeline = PipelineBuilder.Create<int>()
            .SelectParallel(static async (x, ct) =>
            {
                await Task.Delay(100, ct);
                return x * 2;
            })
            .Build();

        cts.CancelAfter(50);

        await Should.ThrowAsync<OperationCanceledException>(() => pipeline.ExecuteAsync(Enumerable.Range(1, 100), cts.Token));
    }

    [Fact]
    public async Task ExecuteStreamAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();

        var pipeline = PipelineBuilder.Create<int>()
            .SelectParallel(static async (x, ct) =>
            {
                await Task.Delay(100, ct);
                return x * 2;
            })
            .Build();

        cts.CancelAfter(50);

        await Should.ThrowAsync<OperationCanceledException>(Act);
        return;

        async Task Act()
        {
            await foreach (var _ in pipeline.ExecuteStreamAsync(Enumerable.Range(1, 100).ToAsyncEnumerable(),
                               // ReSharper disable once AccessToDisposedClosure
                               cts.Token))
            {
                // Consuming items
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_CancellationBeforeStart_ThrowsImmediately()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var pipeline = PipelineBuilder.Create<int>()
            .SelectParallel(static x => x * 2)
            .Build();

        await Should.ThrowAsync<OperationCanceledException>(() => pipeline.ExecuteAsync(Enumerable.Range(1, 10), cts.Token));
    }

    [Fact]
    public async Task ExecuteAsync_CancellationInMiddle_StopsProcessing()
    {
        using var cts = new CancellationTokenSource();
        var processedCount = 0;

        var pipeline = PipelineBuilder.Create<int>()
            .SelectParallel(async (x, ct) =>
            {
                Interlocked.Increment(ref processedCount);
                await Task.Delay(50, ct);
                return x * 2;
            })
            .Build();

        cts.CancelAfter(150);

        await Should.ThrowAsync<OperationCanceledException>(() => pipeline.ExecuteAsync(Enumerable.Range(1, 100), cts.Token));

        // Should have processed some but not all items
        processedCount.ShouldBeLessThan(100);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationInFilter_StopsProcessing()
    {
        using var cts = new CancellationTokenSource();

        var pipeline = PipelineBuilder.Create<int>()
            .WhereParallel(static async (x, ct) =>
            {
                await Task.Delay(50, ct);
                return x % 2 == 0;
            })
            .Build();

        cts.CancelAfter(100);

        await Should.ThrowAsync<OperationCanceledException>(() => pipeline.ExecuteAsync(Enumerable.Range(1, 100), cts.Token));
    }

    [Fact]
    public async Task ExecuteAsync_CancellationInBatch_StopsProcessing()
    {
        using var cts = new CancellationTokenSource();

        var pipeline = PipelineBuilder.Create<int>()
            .BatchSelectParallel(10,
                static async (batch, ct) =>
                {
                    await Task.Delay(200, ct); // Longer delay to ensure cancellation hits
                    return batch.Sum();
                })
            .Build();

        cts.CancelAfter(100);

        await Should.ThrowAsync<OperationCanceledException>(() => pipeline.ExecuteAsync(Enumerable.Range(1, 1000), cts.Token));
    }

    [Fact]
    public async Task ExecuteAsync_CancellationInThrottle_StopsProcessing()
    {
        using var cts = new CancellationTokenSource();

        var pipeline = PipelineBuilder.Create<int>()
            .Throttle(2, burstCapacity: 1) // Very slow rate
            .Build();

        cts.CancelAfter(100);

        await Should.ThrowAsync<OperationCanceledException>(() => pipeline.ExecuteAsync(Enumerable.Range(1, 100), cts.Token));
    }

    [Fact]
    public async Task ExecuteAsync_CancellationInTap_StopsProcessing()
    {
        using var cts = new CancellationTokenSource();

        var pipeline = PipelineBuilder.Create<int>()
            .Tap(static async (_, ct) =>
            {
                await Task.Delay(50, ct);
            })
            .Build();

        cts.CancelAfter(100);

        await Should.ThrowAsync<OperationCanceledException>(() => pipeline.ExecuteAsync(Enumerable.Range(1, 100), cts.Token));
    }

    [Fact]
    public async Task ExecuteAsync_CancellationInBuffer_StopsProcessing()
    {
        using var cts = new CancellationTokenSource();

        var pipeline = PipelineBuilder.Create<int>()
            .SelectParallel(static async (x, ct) =>
            {
                await Task.Delay(50, ct);
                return x * 2;
            })
            .Buffer(5)
            .Build();

        cts.CancelAfter(100);

        await Should.ThrowAsync<OperationCanceledException>(() => pipeline.ExecuteAsync(Enumerable.Range(1, 100), cts.Token));
    }

    [Fact]
    public async Task ExecuteStreamAsync_EarlyBreak_DoesNotThrow()
    {
        var processedCount = 0;

        var pipeline = PipelineBuilder.Create<int>()
            .Tap(_ => Interlocked.Increment(ref processedCount))
            .Build();

        var results = new List<int>();
        await foreach (var result in pipeline.ExecuteStreamAsync(Enumerable.Range(1, 100).ToAsyncEnumerable()))
        {
            results.Add(result);
            if (results.Count >= 5)
                break;
        }

        results.Count.ShouldBe(5);
    }
}
