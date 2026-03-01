using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Rivulet.Pipeline.Tests;

[
    SuppressMessage("ReSharper", "ArgumentsStyleLiteral"),
    SuppressMessage("ReSharper", "ArgumentsStyleStringLiteral")
]
public sealed class ThrottleStageTests
{
    [Fact]
    public async Task Throttle_BurstCapacity_AllowsInitialBurst()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .Throttle(5, burstCapacity: 5, name: "Throttle")
            .Build();

        var sw = Stopwatch.StartNew();
        await pipeline.ExecuteAsync(Enumerable.Range(1, 5), TestContext.Current.CancellationToken);
        sw.Stop();

        // First 5 items should process almost instantly (burst capacity)
        sw.ElapsedMilliseconds.ShouldBeLessThan(500);
    }

    [Fact]
    public async Task Throttle_RateLimits_AfterBurstExhausted()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .Throttle(5, burstCapacity: 3, name: "Throttle")
            .Build();

        var sw = Stopwatch.StartNew();
        await pipeline.ExecuteAsync(Enumerable.Range(1, 6), TestContext.Current.CancellationToken);
        sw.Stop();

        // 3 items burst, then 3 more at 5/sec = ~600ms minimum
        sw.ElapsedMilliseconds.ShouldBeGreaterThan(400);
    }

    [Fact]
    public async Task Throttle_MaintainsDataIntegrity()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .Throttle(100, burstCapacity: 100, name: "Throttle")
            .SelectParallel(static x => x * 2)
            .Build();

        var results = await pipeline.ExecuteAsync(Enumerable.Range(1, 20), TestContext.Current.CancellationToken);

        results.Count.ShouldBe(20);
        results.OrderBy(static x => x).ShouldBe(Enumerable.Range(1, 20).Select(static x => x * 2));
    }

    [Fact]
    public async Task Throttle_WithHighRate_ProcessesQuickly()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .Throttle(1000, burstCapacity: 1000, name: "HighRateThrottle")
            .Build();

        var sw = Stopwatch.StartNew();
        var results = await pipeline.ExecuteAsync(Enumerable.Range(1, 100), TestContext.Current.CancellationToken);
        sw.Stop();

        results.Count.ShouldBe(100);
        sw.ElapsedMilliseconds.ShouldBeLessThan(1000);
    }

    [Fact]
    public async Task Throttle_EmptySource_ReturnsEmpty()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .Throttle(10)
            .Build();

        var results = await pipeline.ExecuteAsync(Enumerable.Empty<int>(), TestContext.Current.CancellationToken);

        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task Throttle_SingleItem_ProcessesImmediately()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .Throttle(1)
            .Build();

        var sw = Stopwatch.StartNew();
        var results = await pipeline.ExecuteAsync(new[] { 42 }, TestContext.Current.CancellationToken);
        sw.Stop();

        results.ShouldHaveSingleItem().ShouldBe(42);
        sw.ElapsedMilliseconds.ShouldBeLessThan(500);
    }

    [Fact]
    public async Task Throttle_ChainedWithOtherStages_WorksCorrectly()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .SelectParallel(static x => x * 2)
            .Throttle(100, burstCapacity: 100)
            .WhereParallel(static x => x > 10)
            .Build();

        var results = await pipeline.ExecuteAsync(Enumerable.Range(1, 20), TestContext.Current.CancellationToken);

        // After doubling: 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32, 34, 36, 38, 40
        // After filtering (> 10): 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32, 34, 36, 38, 40
        results.Count.ShouldBe(15);
    }

    [Fact]
    public async Task Throttle_LowRate_EnforcesLimit()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .Throttle(2, burstCapacity: 1) // 2 per second, but only 1 burst
            .Build();

        var sw = Stopwatch.StartNew();
        await pipeline.ExecuteAsync(Enumerable.Range(1, 3), TestContext.Current.CancellationToken);
        sw.Stop();

        // 1 burst, then 2 more at 2/sec = 1000ms minimum
        sw.ElapsedMilliseconds.ShouldBeGreaterThan(800);
    }
}
