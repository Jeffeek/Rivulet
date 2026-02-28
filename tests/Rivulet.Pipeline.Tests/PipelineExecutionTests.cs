using System.Collections.Concurrent;
using Rivulet.Core;
using Rivulet.Pipeline.Results;

namespace Rivulet.Pipeline.Tests;

public sealed class PipelineExecutionTests
{
    [Fact]
    public async Task ExecuteAsync_EmptySource_ReturnsEmptyList()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .SelectParallel(static x => x * 2)
            .Build();

        var results = await pipeline.ExecuteAsync(Enumerable.Empty<int>(), TestContext.Current.CancellationToken);

        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_SingleItem_ReturnsTransformedItem()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .SelectParallel(static x => x * 2)
            .Build();

        var results = await pipeline.ExecuteAsync(new[] { 5 }, TestContext.Current.CancellationToken);

        results.ShouldHaveSingleItem().ShouldBe(10);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleItems_ReturnsAllTransformed()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .SelectParallel(static x => x * 2)
            .Build();

        var results = await pipeline.ExecuteAsync(Enumerable.Range(1, 10), TestContext.Current.CancellationToken);

        results.Count.ShouldBe(10);
        results.OrderBy(static x => x).ShouldBe(new[] { 2, 4, 6, 8, 10, 12, 14, 16, 18, 20 });
    }

    [Fact]
    public async Task ExecuteAsync_WithFilter_FiltersCorrectly()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .SelectParallel(static x => x * 2)
            .WhereParallel(static x => x > 10)
            .Build();

        var results = await pipeline.ExecuteAsync(Enumerable.Range(1, 10), TestContext.Current.CancellationToken);

        // After doubling: 2, 4, 6, 8, 10, 12, 14, 16, 18, 20
        // After filtering (> 10): 12, 14, 16, 18, 20
        results.Count.ShouldBe(5);
        results.OrderBy(static x => x).ShouldBe(new[] { 12, 14, 16, 18, 20 });
    }

    [Fact]
    public async Task ExecuteAsync_MultipleStages_TransformsCorrectly()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .SelectParallel(static x => x * 2)
            .SelectParallel(static x => x + 1)
            .SelectParallel(static x => x.ToString())
            .Build();

        var results = await pipeline.ExecuteAsync(new[] { 1, 2, 3 }, TestContext.Current.CancellationToken);

        results.Count.ShouldBe(3);
        results.OrderBy(static x => x).ShouldBe(new[] { "3", "5", "7" });
    }

    [Fact]
    public async Task ExecuteAsync_WithAsyncSelector_HandlesDelays()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .SelectParallel(static async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x * 2;
            })
            .Build();

        var results = await pipeline.ExecuteAsync(Enumerable.Range(1, 5), TestContext.Current.CancellationToken);

        results.Count.ShouldBe(5);
        results.OrderBy(static x => x).ShouldBe(new[] { 2, 4, 6, 8, 10 });
    }

    [Fact]
    public async Task ExecuteAsync_WithBatch_BatchesCorrectly()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .Batch(5)
            .Build();

        var results = await pipeline.ExecuteAsync(Enumerable.Range(1, 12), TestContext.Current.CancellationToken);

        results.Count.ShouldBe(3);
        results[0].Count.ShouldBe(5);
        results[1].Count.ShouldBe(5);
        results[2].Count.ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAsync_WithBatchSelect_ProcessesBatches()
    {
        var batchSizes = new ConcurrentBag<int>();

        var pipeline = PipelineBuilder.Create<int>()
            .BatchSelectParallel(5,
                (batch, _) =>
                {
                    batchSizes.Add(batch.Count);
                    return ValueTask.FromResult(batch.Sum());
                })
            .Build();

        var results = await pipeline.ExecuteAsync(Enumerable.Range(1, 12), TestContext.Current.CancellationToken);

        results.Count.ShouldBe(3);
        results.Sum().ShouldBe(78); // Sum of 1..12
        batchSizes.ShouldContain(5);
        batchSizes.ShouldContain(2);
    }

    [Fact]
    public async Task ExecuteAsync_WithSelectMany_FlattensCorrectly()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .SelectManyParallel(static x => Enumerable.Range(1, x))
            .Build();

        var results = await pipeline.ExecuteAsync(new[] { 1, 2, 3 }, TestContext.Current.CancellationToken);

        // 1 -> [1], 2 -> [1, 2], 3 -> [1, 2, 3]
        // Total: 1 + 2 + 3 = 6 items
        results.Count.ShouldBe(6);
        results.OrderBy(static x => x).ShouldBe(new[] { 1, 1, 1, 2, 2, 3 });
    }

    [Fact]
    public async Task ExecuteAsync_WithTap_ExecutesSideEffect()
    {
        var tappedItems = new ConcurrentBag<int>();

        var pipeline = PipelineBuilder.Create<int>()
            .SelectParallel(static x => x * 2)
            .Tap(x => tappedItems.Add(x))
            .Build();

        var results = await pipeline.ExecuteAsync(Enumerable.Range(1, 5), TestContext.Current.CancellationToken);

        results.Count.ShouldBe(5);
        tappedItems.Count.ShouldBe(5);
        tappedItems.OrderBy(static x => x).ShouldBe(new[] { 2, 4, 6, 8, 10 });
    }

    [Fact]
    public async Task ExecuteAsync_WithBuffer_BuffersItems()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .SelectParallel(static x => x * 2)
            .Buffer(10)
            .Build();

        var results = await pipeline.ExecuteAsync(Enumerable.Range(1, 20), TestContext.Current.CancellationToken);

        results.Count.ShouldBe(20);
        results.Sum().ShouldBe(420); // Sum of 2+4+...+40
    }

    [Fact]
    public async Task ExecuteStreamAsync_StreamsResults()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .SelectParallel(static async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x * 2;
            })
            .Build();

        var results = new List<int>();
        await foreach (var result in pipeline.ExecuteStreamAsync(Enumerable.Range(1, 5).ToAsyncEnumerable(), TestContext.Current.CancellationToken))
            results.Add(result);

        results.Count.ShouldBe(5);
        results.OrderBy(static x => x).ShouldBe(new[] { 2, 4, 6, 8, 10 });
    }

    [Fact]
    public async Task ExecuteAsync_WithCallbacks_InvokesCallbacks()
    {
        var startCalled = false;
        var completeCalled = false;
        var stageStartCount = 0;

        var pipeline = PipelineBuilder.Create<int>(new PipelineOptions
            {
                Name = "CallbackTest",
                OnPipelineStartAsync = _ =>
                {
                    startCalled = true;
                    return ValueTask.CompletedTask;
                },
                OnPipelineCompleteAsync = (_, _) =>
                {
                    completeCalled = true;
                    return ValueTask.CompletedTask;
                },
                OnStageStartAsync = (_, _) =>
                {
                    Interlocked.Increment(ref stageStartCount);
                    return ValueTask.CompletedTask;
                }
            })
            .SelectParallel(static x => x * 2, name: "Double")
            .SelectParallel(static x => x + 1, name: "Increment")
            .Build();

        await pipeline.ExecuteAsync(Enumerable.Range(1, 5), TestContext.Current.CancellationToken);

        startCalled.ShouldBeTrue();
        completeCalled.ShouldBeTrue();
        stageStartCount.ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAsync_WithPipelineResult_ReportsMetrics()
    {
        PipelineResult? capturedResult = null;

        var pipeline = PipelineBuilder.Create<int>(new PipelineOptions
            {
                Name = "MetricsTest",
                OnPipelineCompleteAsync = (_, result) =>
                {
                    capturedResult = result;
                    return ValueTask.CompletedTask;
                }
            })
            .SelectParallel(static x => x * 2)
            .Build();

        await pipeline.ExecuteAsync(Enumerable.Range(1, 10), TestContext.Current.CancellationToken);

        capturedResult.ShouldNotBeNull();
        capturedResult!.ItemsCompleted.ShouldBe(10);
        capturedResult.Elapsed.ShouldBeGreaterThan(TimeSpan.Zero);
        capturedResult.ItemsPerSecond.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteAsync_WithStageOptions_RespectsPerStageConfig()
    {
        var maxConcurrent = 0;
        var concurrentCount = 0;
        var lockObj = new object();

        var pipeline = PipelineBuilder.Create<int>()
            .SelectParallel(
                async (x, ct) =>
                {
                    lock (lockObj)
                    {
                        concurrentCount++;
                        maxConcurrent = Math.Max(maxConcurrent, concurrentCount);
                    }

                    await Task.Delay(50, ct);

                    lock (lockObj)
                        concurrentCount--;

                    return x * 2;
                },
                new StageOptions
                {
                    ParallelOptions = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 2 }
                })
            .Build();

        await pipeline.ExecuteAsync(Enumerable.Range(1, 10), TestContext.Current.CancellationToken);

        maxConcurrent.ShouldBeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task ExecuteAsync_LargeDataset_ProcessesAll()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .SelectParallel(static x => x * 2)
            .WhereParallel(static x => x % 4 == 0)
            .Build();

        var results = await pipeline.ExecuteAsync(Enumerable.Range(1, 1000), TestContext.Current.CancellationToken);

        results.Count.ShouldBe(500);
        results.Sum().ShouldBe(501000);
    }

    [Fact]
    public async Task ExecuteAsync_ComplexPipeline_WorksCorrectly()
    {
        var pipeline = PipelineBuilder.Create<int>("ComplexPipeline")
            .SelectParallel(static x => x * 2, name: "Double")
            .WhereParallel(static x => x > 10, name: "FilterGreaterThan10")
            .Batch(3, name: "BatchBy3")
            .SelectParallel(static (batch, _) => ValueTask.FromResult(batch.Sum()), name: "SumBatch")
            .Build();

        var results = await pipeline.ExecuteAsync(Enumerable.Range(1, 10), TestContext.Current.CancellationToken);

        // After doubling: 2, 4, 6, 8, 10, 12, 14, 16, 18, 20
        // After filtering (> 10): 12, 14, 16, 18, 20 (5 items)
        // After batching by 3: [12,14,16], [18,20]
        // After sum: 42, 38
        results.Count.ShouldBe(2);
        results.Sum().ShouldBe(80);
    }
}
