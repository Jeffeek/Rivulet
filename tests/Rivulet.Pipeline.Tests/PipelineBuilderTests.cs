using Rivulet.Core;
using Rivulet.Core.Resilience;

namespace Rivulet.Pipeline.Tests;

public sealed class PipelineBuilderTests
{
    [Fact]
    public void Create_WithName_SetsName()
    {
        var pipeline = PipelineBuilder.Create<int>("TestPipeline")
            .SelectParallel(static x => x * 2)
            .Build();

        pipeline.Name.ShouldBe("TestPipeline");
    }

    [Fact]
    public void Create_WithOptions_UsesOptions()
    {
        var options = new PipelineOptions
        {
            Name = "CustomPipeline",
            DefaultStageOptions = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 4 }
        };

        var pipeline = PipelineBuilder.Create<int>(options)
            .SelectParallel(static x => x * 2)
            .Build();

        pipeline.Name.ShouldBe("CustomPipeline");
    }

    [Fact]
    public void Create_WithoutStages_ThrowsOnBuild()
    {
        var builder = PipelineBuilder.Create<int>("EmptyPipeline");

        Should.Throw<InvalidOperationException>(() => builder.Build())
            .Message.ShouldContain("at least one stage");
    }

    [Fact]
    public void SelectParallel_AddsTransformStage()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .SelectParallel(static x => x * 2)
            .Build();

        pipeline.StageCount.ShouldBe(1);
    }

    [Fact]
    public void SelectParallel_WithCustomName_UsesName()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .SelectParallel(static x => x * 2, name: "DoubleStage")
            .Build();

        pipeline.StageCount.ShouldBe(1);
    }

    [Fact]
    public void WhereParallel_AddsFilterStage()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .WhereParallel(static x => x % 2 == 0)
            .Build();

        pipeline.StageCount.ShouldBe(1);
    }

    [Fact]
    public void Batch_InvalidSize_ThrowsArgumentException()
    {
        var builder = PipelineBuilder.Create<int>();

        Should.Throw<ArgumentException>(() => builder.Batch(0))
            .Message.ShouldContain("Batch size must be at least 1");
    }

    [Fact]
    public void Batch_ValidSize_AddsBatchStage()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .Batch(10)
            .Build();

        pipeline.StageCount.ShouldBe(1);
    }

    [Fact]
    public void Buffer_InvalidCapacity_ThrowsArgumentException()
    {
        var builder = PipelineBuilder.Create<int>();

        Should.Throw<ArgumentException>(() => builder.Buffer(0))
            .Message.ShouldContain("Buffer capacity must be at least 1");
    }

    [Fact]
    public void Buffer_ValidCapacity_AddsBufferStage()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .Buffer(10)
            .Build();

        pipeline.StageCount.ShouldBe(1);
    }

    [Fact]
    public void Throttle_InvalidRate_ThrowsArgumentException()
    {
        var builder = PipelineBuilder.Create<int>();

        Should.Throw<ArgumentException>(() => builder.Throttle(0))
            .Message.ShouldContain("Items per second must be positive");
    }

    [Fact]
    public void Throttle_ValidRate_AddsThrottleStage()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .Throttle(10.0)
            .Build();

        pipeline.StageCount.ShouldBe(1);
    }

    [Fact]
    public void MultipleStages_ChainsCorrectly()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .SelectParallel(static x => x * 2, name: "Double")
            .WhereParallel(static x => x > 5, name: "FilterGreaterThan5")
            .SelectParallel(static x => x.ToString(), name: "ToString")
            .Build();

        pipeline.StageCount.ShouldBe(3);
    }

    [Fact]
    public void SelectManyParallel_AddsFlattenStage()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .SelectManyParallel(static x => Enumerable.Range(1, x))
            .Build();

        pipeline.StageCount.ShouldBe(1);
    }

    [Fact]
    public void Tap_AddsTapStage()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .Tap(static _ => { })
            .Build();

        pipeline.StageCount.ShouldBe(1);
    }

    [Fact]
    public void BatchSelectParallel_AddsBatchSelectStage()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .BatchSelectParallel(5, static (batch, _) => ValueTask.FromResult(batch.Sum()))
            .Build();

        pipeline.StageCount.ShouldBe(1);
    }

    [Fact]
    public void BatchSelectParallel_InvalidBatchSize_ThrowsArgumentException()
    {
        var builder = PipelineBuilder.Create<int>();

        Should.Throw<ArgumentException>(() =>
                builder.BatchSelectParallel(0, static (batch, _) => ValueTask.FromResult(batch.Sum())))
            .Message.ShouldContain("Batch size must be at least 1");
    }

    [Fact]
    public void WithRetries_ConfiguresRetryPolicy()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .WithRetries(3, TimeSpan.FromMilliseconds(100), BackoffStrategy.ExponentialJitter)
            .SelectParallel(static x => x * 2)
            .Build();

        pipeline.StageCount.ShouldBe(1);
    }

    [Fact]
    public void WithCircuitBreaker_ConfiguresCircuitBreaker()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .WithCircuitBreaker(5, TimeSpan.FromSeconds(30), 2)
            .SelectParallel(static x => x * 2)
            .Build();

        pipeline.StageCount.ShouldBe(1);
    }

    [Fact]
    public void WithRateLimit_ConfiguresRateLimit()
    {
        var pipeline = PipelineBuilder.Create<int>()
            .WithRateLimit(100, 200)
            .SelectParallel(static x => x * 2)
            .Build();

        pipeline.StageCount.ShouldBe(1);
    }

    [Fact]
    public void Builder_IsImmutable()
    {
        var builder1 = PipelineBuilder.Create<int>();
        var builder2 = builder1.SelectParallel(static x => x * 2);
        var builder3 = builder2.SelectParallel(static x => x + 1);

        // Each builder should create pipelines with different stage counts
        Should.Throw<InvalidOperationException>(() => builder1.Build());
        builder2.Build().StageCount.ShouldBe(1);
        builder3.Build().StageCount.ShouldBe(2);
    }
}
