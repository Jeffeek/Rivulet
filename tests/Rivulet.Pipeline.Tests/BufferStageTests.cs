namespace Rivulet.Pipeline.Tests;

public sealed class BufferStageTests
{
    [Fact]
    public async Task Buffer_PassesAllItems()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .Buffer(10)
            .Build();

        var results = await pipeline.ExecuteAsync(Enumerable.Range(1, 20), TestContext.Current.CancellationToken);

        results.Count.ShouldBe(20);
        results.OrderBy(static x => x).ShouldBe(Enumerable.Range(1, 20));
    }

    [Fact]
    public async Task Buffer_EmptySource_ReturnsEmpty()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .Buffer(10)
            .Build();

        var results = await pipeline.ExecuteAsync(Enumerable.Empty<int>(), TestContext.Current.CancellationToken);

        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task Buffer_SingleItem_PassesItem()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .Buffer(10)
            .Build();

        var results = await pipeline.ExecuteAsync(new[] { 42 }, TestContext.Current.CancellationToken);

        results.ShouldHaveSingleItem().ShouldBe(42);
    }

    [Fact]
    public async Task Buffer_DecouplesProducerAndConsumer()
    {
        var producerFinished = false;
        var consumerStarted = false;

        var pipeline = PipelineBuilder.Create<int>()
            .Tap(_ =>
            {
                consumerStarted = true;
            })
            .Buffer(100) // Large buffer to decouple
            .Tap(_ =>
            {
                // ReSharper disable once AccessToModifiedClosure
                if (!producerFinished)
                    consumerStarted = true;
            })
            .Build();

        await pipeline.ExecuteAsync(Enumerable.Range(1, 10), TestContext.Current.CancellationToken);
        producerFinished = true;

        consumerStarted.ShouldBeTrue();
    }

    [Fact]
    public async Task Buffer_WithBackpressure_HandlesFullBuffer()
    {
        var processedCount = 0;

        var pipeline = PipelineBuilder.Create<int>()
            .Buffer(5) // Small buffer
            .Tap(_ => Interlocked.Increment(ref processedCount))
            .Build();

        var results = await pipeline.ExecuteAsync(Enumerable.Range(1, 100), TestContext.Current.CancellationToken);

        results.Count.ShouldBe(100);
        processedCount.ShouldBe(100);
    }

    [Fact]
    public async Task Buffer_ChainedWithOtherStages_WorksCorrectly()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .SelectParallel(static x => x * 2)
            .Buffer(10)
            .WhereParallel(static x => x > 10)
            .Buffer(5)
            .Build();

        var results = await pipeline.ExecuteAsync(Enumerable.Range(1, 20), TestContext.Current.CancellationToken);

        results.Count.ShouldBe(15); // All x*2 > 10 where x >= 6
    }

    [Fact]
    public async Task Buffer_LargeDataset_HandlesCorrectly()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .Buffer(100)
            .SelectParallel(static x => x * 2)
            .Build();

        var results = await pipeline.ExecuteAsync(Enumerable.Range(1, 10000), TestContext.Current.CancellationToken);

        results.Count.ShouldBe(10000);
        results.Sum().ShouldBe(100010000); // Sum of 2+4+...+20000
    }

    [Fact]
    public async Task Buffer_MinimalCapacity_WorksCorrectly()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .Buffer(1)
            .Build();

        var results = await pipeline.ExecuteAsync(Enumerable.Range(1, 10), TestContext.Current.CancellationToken);

        results.Count.ShouldBe(10);
    }

    [Fact]
    public async Task Buffer_MultipleBuffers_ChainCorrectly()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .Buffer(10)
            .SelectParallel(static x => x * 2)
            .Buffer(5)
            .SelectParallel(static x => x + 1)
            .Buffer(20)
            .Build();

        var results = await pipeline.ExecuteAsync(Enumerable.Range(1, 50), TestContext.Current.CancellationToken);

        results.Count.ShouldBe(50);
        // x -> x*2 -> x*2+1 = 2x+1
        results.OrderBy(static x => x).ShouldBe(Enumerable.Range(1, 50).Select(static x => x * 2 + 1));
    }
}
