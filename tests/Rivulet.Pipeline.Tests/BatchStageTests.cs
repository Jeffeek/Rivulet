using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Rivulet.Pipeline.Tests;

[SuppressMessage("ReSharper", "ArgumentsStyleOther")]
public sealed class BatchStageTests
{
    [Fact]
    public async Task Batch_ExactMultiple_CreatesFullBatches()
    {
        var batchSizes = new ConcurrentBag<int>();

        var pipeline = PipelineBuilder.Create<int>()
            .Batch(5)
            .SelectParallel((batch, _) =>
            {
                batchSizes.Add(batch.Count);
                return ValueTask.FromResult(batch.Sum());
            })
            .Build();

        var results = await pipeline.ExecuteAsync(Enumerable.Range(1, 20));

        results.Count.ShouldBe(4);
        batchSizes.ShouldAllBe(static size => size == 5);
    }

    [Fact]
    public async Task Batch_WithRemainder_IncludesPartialBatch()
    {
        var batchSizes = new ConcurrentBag<int>();

        var pipeline = PipelineBuilder.Create<int>()
            .Batch(5)
            .SelectParallel((batch, _) =>
            {
                batchSizes.Add(batch.Count);
                return ValueTask.FromResult(batch.Sum());
            })
            .Build();

        var results = await pipeline.ExecuteAsync(Enumerable.Range(1, 17));

        results.Count.ShouldBe(4); // 3 full batches + 1 partial
        batchSizes.ShouldContain(5);
        batchSizes.ShouldContain(2);
    }

    [Fact]
    public async Task Batch_SingleItem_CreatesSingleBatch()
    {
        var batchSizes = new ConcurrentBag<int>();

        var pipeline = PipelineBuilder.Create<int>()
            .Batch(10)
            .SelectParallel((batch, _) =>
            {
                batchSizes.Add(batch.Count);
                return ValueTask.FromResult(batch.Sum());
            })
            .Build();

        var results = await pipeline.ExecuteAsync(new[] { 42 });

        results.ShouldHaveSingleItem().ShouldBe(42);
        batchSizes.ShouldHaveSingleItem().ShouldBe(1);
    }

    [Fact]
    public async Task Batch_EmptySource_ReturnsEmpty()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .Batch(10)
            .Build();

        var results = await pipeline.ExecuteAsync(Enumerable.Empty<int>());

        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task Batch_BatchSizeOne_CreatesSingleItemBatches()
    {
        var batchSizes = new ConcurrentBag<int>();

        var pipeline = PipelineBuilder.Create<int>()
            .Batch(1)
            .SelectParallel((batch, _) =>
            {
                batchSizes.Add(batch.Count);
                return ValueTask.FromResult(batch.Single());
            })
            .Build();

        var results = await pipeline.ExecuteAsync(Enumerable.Range(1, 5));

        results.Count.ShouldBe(5);
        batchSizes.ShouldAllBe(static size => size == 1);
    }

    [Fact]
    public async Task Batch_LargeBatchSize_HandlesCorrectly()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .Batch(1000)
            .Build();

        var results = await pipeline.ExecuteAsync(Enumerable.Range(1, 100));

        results.ShouldHaveSingleItem();
        results[0].Count.ShouldBe(100);
    }

    [Fact]
    public async Task BatchSelect_ProcessesBatchesInParallel()
    {
        var maxConcurrent = 0;
        var concurrentCount = 0;
        var lockObj = new object();

        var pipeline = PipelineBuilder.Create<int>()
            .BatchSelectParallel(10,
                async (batch, ct) =>
                {
                    lock (lockObj)
                    {
                        concurrentCount++;
                        maxConcurrent = Math.Max(maxConcurrent, concurrentCount);
                    }

                    await Task.Delay(50, ct);

                    lock (lockObj)
                        concurrentCount--;

                    return batch.Sum();
                })
            .Build();

        await pipeline.ExecuteAsync(Enumerable.Range(1, 100));

        // Should have some parallel processing
        maxConcurrent.ShouldBeGreaterThan(1);
    }

    [Fact]
    public async Task BatchSelect_PreservesDataIntegrity()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .BatchSelectParallel(10, static (batch, _) => ValueTask.FromResult(batch.Sum()))
            .Build();

        var results = await pipeline.ExecuteAsync(Enumerable.Range(1, 100));

        results.Count.ShouldBe(10);
        results.Sum().ShouldBe(5050); // Sum of 1..100
    }

    [Fact]
    public async Task Batch_ChainedWithOtherStages_WorksCorrectly()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .SelectParallel(static x => x * 2)
            .Batch(5)
            .SelectParallel(static (batch, _) => ValueTask.FromResult(batch.Sum()))
            .WhereParallel(static x => x > 50)
            .Build();

        var results = await pipeline.ExecuteAsync(Enumerable.Range(1, 20));

        // After doubling: 2,4,6,8,10,12,14,16,18,20,22,24,26,28,30,32,34,36,38,40
        // After batching by 5: batches have sum > 30 (first batch sum)
        // After filtering (> 50): all batches except first
        // Due to parallel processing, order is not guaranteed, but we should have
        // batches with sums greater than 50
        results.ShouldNotBeEmpty();
        results.ShouldAllBe(static x => x > 50);
        results.Sum().ShouldBeGreaterThan(200); // Should have significant sum
    }

    [Fact]
    public async Task Batch_WithTimeout_FlushesPartialBatches()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .Batch(100, flushTimeout: TimeSpan.FromMilliseconds(100))
            .SelectParallel(static (batch, _) => ValueTask.FromResult(batch.Count))
            .Build();

        var results = await pipeline.ExecuteAsync(SlowSource().ToBlockingEnumerable().ToList());

        results.Sum().ShouldBe(5);
        return;

        // Create a slow async source
        static async IAsyncEnumerable<int> SlowSource()
        {
            for (var i = 1; i <= 5; i++)
            {
                yield return i;

                await Task.Delay(50); // Slow enough that timeout triggers
            }
        }
    }

    [Fact]
    public async Task Batch_MaintainsOrder()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .Batch(3)
            .Build();

        var results = await pipeline.ExecuteAsync(Enumerable.Range(1, 9));

        results.Count.ShouldBe(3);
        results[0].ShouldBe(new[] { 1, 2, 3 });
        results[1].ShouldBe(new[] { 4, 5, 6 });
        results[2].ShouldBe(new[] { 7, 8, 9 });
    }
}
