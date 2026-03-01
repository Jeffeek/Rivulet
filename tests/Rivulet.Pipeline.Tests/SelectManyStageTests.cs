namespace Rivulet.Pipeline.Tests;

public sealed class SelectManyStageTests
{
    [Fact]
    public async Task SelectMany_FlattensCollections()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .SelectManyParallel(static x => Enumerable.Range(1, x))
            .Build();

        var results = await pipeline.ExecuteAsync(new[] { 1, 2, 3 }, TestContext.Current.CancellationToken);

        // 1 -> [1], 2 -> [1, 2], 3 -> [1, 2, 3]
        results.Count.ShouldBe(6);
        results.OrderBy(static x => x).ShouldBe(new[] { 1, 1, 1, 2, 2, 3 });
    }

    [Fact]
    public async Task SelectMany_EmptySource_ReturnsEmpty()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .SelectManyParallel(static x => Enumerable.Range(1, x))
            .Build();

        var results = await pipeline.ExecuteAsync(Enumerable.Empty<int>(), TestContext.Current.CancellationToken);

        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task SelectMany_EmptyCollections_SkipsThem()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .SelectManyParallel(static x => x > 0 ? Enumerable.Range(1, x) : Enumerable.Empty<int>())
            .Build();

        var results = await pipeline.ExecuteAsync(new[] { 0, 1, 0, 2, 0 }, TestContext.Current.CancellationToken);

        // 0 -> [], 1 -> [1], 0 -> [], 2 -> [1, 2], 0 -> []
        results.Count.ShouldBe(3);
        results.OrderBy(static x => x).ShouldBe(new[] { 1, 1, 2 });
    }

    [Fact]
    public async Task SelectMany_SingleItemToMany_ExpandsCorrectly()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .SelectManyParallel(static x => Enumerable.Repeat(x, x))
            .Build();

        var results = await pipeline.ExecuteAsync(new[] { 3 }, TestContext.Current.CancellationToken);

        // 3 -> [3, 3, 3]
        results.Count.ShouldBe(3);
        results.ShouldAllBe(static x => x == 3);
    }

    [Fact]
    public async Task SelectMany_WithAsyncSelector_HandlesAsync()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .SelectManyParallel(static async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return Enumerable.Range(1, x);
            })
            .Build();

        var results = await pipeline.ExecuteAsync(new[] { 2, 3 }, TestContext.Current.CancellationToken);

        results.Count.ShouldBe(5); // 2 + 3
    }

    [Fact]
    public async Task SelectMany_ChainedWithOtherStages_WorksCorrectly()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .SelectManyParallel(static x => Enumerable.Range(1, x))
            .SelectParallel(static x => x * 10)
            .WhereParallel(static x => x > 15)
            .Build();

        var results = await pipeline.ExecuteAsync(new[] { 1, 2, 3 }, TestContext.Current.CancellationToken);

        // After flatten: [1, 1, 2, 1, 2, 3]
        // After *10: [10, 10, 20, 10, 20, 30]
        // After filter (>15): [20, 20, 30]
        results.Count.ShouldBe(3);
        results.OrderBy(static x => x).ShouldBe(new[] { 20, 20, 30 });
    }

    [Fact]
    public async Task SelectMany_LargeExpansion_HandlesCorrectly()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .SelectManyParallel(static _ => Enumerable.Range(1, 100))
            .Build();

        var results = await pipeline.ExecuteAsync(Enumerable.Range(1, 10), TestContext.Current.CancellationToken);

        results.Count.ShouldBe(1000); // 10 items * 100 each
    }

    [Fact]
    public async Task SelectMany_ProcessesInParallel()
    {
        var maxConcurrent = 0;
        var concurrentCount = 0;
        var lockObj = new object();

        var pipeline = PipelineBuilder.Create<int>()
            .SelectManyParallel(async (x, ct) =>
            {
                lock (lockObj)
                {
                    concurrentCount++;
                    maxConcurrent = Math.Max(maxConcurrent, concurrentCount);
                }

                await Task.Delay(50, ct);

                lock (lockObj)
                    concurrentCount--;

                return Enumerable.Range(1, x);
            })
            .Build();

        await pipeline.ExecuteAsync(Enumerable.Range(1, 10), TestContext.Current.CancellationToken);

        maxConcurrent.ShouldBeGreaterThan(1);
    }

    [Fact]
    public async Task SelectMany_StringSplit_WorksCorrectly()
    {
        var pipeline = PipelineBuilder.Create<string>()
            .SelectManyParallel(static s => s.Split(' '))
            .Build();

        var results = await pipeline.ExecuteAsync(new[] { "hello world", "foo bar baz" }, TestContext.Current.CancellationToken);

        results.Count.ShouldBe(5);
        results.OrderBy(static x => x).ShouldBe(new[] { "bar", "baz", "foo", "hello", "world" });
    }
}
