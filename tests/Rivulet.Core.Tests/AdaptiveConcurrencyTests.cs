using FluentAssertions;

namespace Rivulet.Core.Tests;

public class AdaptiveConcurrencyTests
{
    [Fact]
    public void AdaptiveConcurrencyOptions_Validation_ThrowsOnInvalidMinConcurrency()
    {
        var act = () => new AdaptiveConcurrencyOptions { MinConcurrency = 0 }.Validate();
        act.Should().Throw<ArgumentException>()
            .WithMessage("*MinConcurrency*");
    }

    [Fact]
    public void AdaptiveConcurrencyOptions_Validation_ThrowsWhenMaxLessThanMin()
    {
        var act = () => new AdaptiveConcurrencyOptions { MinConcurrency = 10, MaxConcurrency = 5 }.Validate();
        act.Should().Throw<ArgumentException>()
            .WithMessage("*MaxConcurrency*");
    }

    [Fact]
    public void AdaptiveConcurrencyOptions_Validation_ThrowsOnInvalidInitialConcurrency()
    {
        var act = () => new AdaptiveConcurrencyOptions
        {
            MinConcurrency = 5,
            MaxConcurrency = 10,
            InitialConcurrency = 15
        }.Validate();

        act.Should().Throw<ArgumentException>()
            .WithMessage("*InitialConcurrency*");
    }

    [Fact]
    public void AdaptiveConcurrencyOptions_Validation_ThrowsOnInvalidSampleInterval()
    {
        var act = () => new AdaptiveConcurrencyOptions { SampleInterval = TimeSpan.Zero }.Validate();
        act.Should().Throw<ArgumentException>()
            .WithMessage("*SampleInterval*");
    }

    [Fact]
    public void AdaptiveConcurrencyOptions_Validation_ThrowsOnInvalidTargetLatency()
    {
        var act = () => new AdaptiveConcurrencyOptions { TargetLatency = TimeSpan.Zero }.Validate();
        act.Should().Throw<ArgumentException>()
            .WithMessage("*TargetLatency*");
    }

    [Fact]
    public void AdaptiveConcurrencyOptions_Validation_ThrowsOnInvalidMinSuccessRate()
    {
        var act = () => new AdaptiveConcurrencyOptions { MinSuccessRate = 1.5 }.Validate();
        act.Should().Throw<ArgumentException>()
            .WithMessage("*MinSuccessRate*");
    }

    [Fact]
    public void AdaptiveConcurrencyController_Constructor_ThrowsOnNullOptions()
    {
        var act = () => new AdaptiveConcurrencyController(null!);
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("*options*");
    }

    [Fact]
    public void AdaptiveConcurrencyController_InitialConcurrency_UsesConfiguredValue()
    {
        var controller = new AdaptiveConcurrencyController(new AdaptiveConcurrencyOptions
        {
            MinConcurrency = 5,
            MaxConcurrency = 20,
            InitialConcurrency = 10
        });

        controller.CurrentConcurrency.Should().Be(10);
        controller.Dispose();
    }

    [Fact]
    public void AdaptiveConcurrencyController_InitialConcurrency_DefaultsToMin()
    {
        var controller = new AdaptiveConcurrencyController(new AdaptiveConcurrencyOptions
        {
            MinConcurrency = 5,
            MaxConcurrency = 20
        });

        controller.CurrentConcurrency.Should().Be(5);
        controller.Dispose();
    }

    [Fact]
    public async Task AdaptiveConcurrency_IncreasesOnSuccess()
    {
        var source = Enumerable.Range(1, 100);
        var concurrencyLevels = new List<int>();

        var options = new ParallelOptionsRivulet
        {
            AdaptiveConcurrency = new AdaptiveConcurrencyOptions
            {
                MinConcurrency = 1,
                MaxConcurrency = 10,
                InitialConcurrency = 1,
                SampleInterval = TimeSpan.FromMilliseconds(50),
                MinSuccessRate = 0.5,
                OnConcurrencyChange = (_, @new) =>
                {
                    concurrencyLevels.Add(@new);
                    return ValueTask.CompletedTask;
                }
            }
        };

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(5, ct); // Fast operations
                return x * 2;
            },
            options);

        results.Should().HaveCount(100);

        // Wait for callbacks to complete
        await Task.Delay(200);

        // Should have increased concurrency at least once
        concurrencyLevels.Should().NotBeEmpty();
        concurrencyLevels.Max().Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task AdaptiveConcurrency_DecreasesOnHighLatency()
    {
        var source = Enumerable.Range(1, 50);
        var concurrencyLevels = new List<int>();

        var options = new ParallelOptionsRivulet
        {
            AdaptiveConcurrency = new AdaptiveConcurrencyOptions
            {
                MinConcurrency = 1,
                MaxConcurrency = 10,
                InitialConcurrency = 8,
                SampleInterval = TimeSpan.FromMilliseconds(100),
                TargetLatency = TimeSpan.FromMilliseconds(10),
                MinSuccessRate = 0.5,
                OnConcurrencyChange = (_, @new) =>
                {
                    concurrencyLevels.Add(@new);
                    return ValueTask.CompletedTask;
                }
            }
        };

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(50, ct); // Slow operations exceeding target latency
                return x * 2;
            },
            options);

        results.Should().HaveCount(50);

        // Wait for callbacks
        await Task.Delay(300);

        // Should have decreased concurrency
        concurrencyLevels.Should().NotBeEmpty();
        concurrencyLevels.Min().Should().BeLessThan(8);
    }

    [Fact]
    public async Task AdaptiveConcurrency_DecreasesOnLowSuccessRate()
    {
        var source = Enumerable.Range(1, 40);
        var concurrencyLevels = new List<int>();

        var options = new ParallelOptionsRivulet
        {
            AdaptiveConcurrency = new AdaptiveConcurrencyOptions
            {
                MinConcurrency = 1,
                MaxConcurrency = 10,
                InitialConcurrency = 6,
                SampleInterval = TimeSpan.FromMilliseconds(100),
                MinSuccessRate = 0.8,
                OnConcurrencyChange = (_, @new) =>
                {
                    concurrencyLevels.Add(@new);
                    return ValueTask.CompletedTask;
                }
            },
            ErrorMode = ErrorMode.BestEffort
        };

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(5, ct);
                // 50% failure rate
                if (x % 2 == 0)
                    throw new InvalidOperationException("Test failure");
                return x * 2;
            },
            options);

        // Should get only successful results (50% of 40 = 20)
        results.Count.Should().BeLessThan(40);

        // Wait for callbacks
        await Task.Delay(300);

        // Should have decreased concurrency due to low success rate
        concurrencyLevels.Should().NotBeEmpty();
        concurrencyLevels.Min().Should().BeLessThan(6);
    }

    [Fact]
    public async Task AdaptiveConcurrency_RespectsMinMaxBounds()
    {
        var source = Enumerable.Range(1, 100);
        var concurrencyLevels = new List<int>();

        var options = new ParallelOptionsRivulet
        {
            AdaptiveConcurrency = new AdaptiveConcurrencyOptions
            {
                MinConcurrency = 3,
                MaxConcurrency = 5,
                InitialConcurrency = 4,
                SampleInterval = TimeSpan.FromMilliseconds(50),
                MinSuccessRate = 0.5,
                OnConcurrencyChange = (_, @new) =>
                {
                    concurrencyLevels.Add(@new);
                    return ValueTask.CompletedTask;
                }
            }
        };

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(2, ct);
                return x * 2;
            },
            options);

        results.Should().HaveCount(100);

        // Wait for callbacks
        await Task.Delay(200);

        concurrencyLevels.Should().OnlyContain(c => c >= 3 && c <= 5);
    }

    [Fact]
    public async Task AdaptiveConcurrency_WithStreamingOperations()
    {
        var source = AsyncEnumerable.Range(1, 60);
        var concurrencyLevels = new List<int>();

        var options = new ParallelOptionsRivulet
        {
            AdaptiveConcurrency = new AdaptiveConcurrencyOptions
            {
                MinConcurrency = 1,
                MaxConcurrency = 8,
                InitialConcurrency = 2,
                SampleInterval = TimeSpan.FromMilliseconds(50),
                MinSuccessRate = 0.5,
                OnConcurrencyChange = (_, @new) =>
                {
                    concurrencyLevels.Add(@new);
                    return ValueTask.CompletedTask;
                }
            }
        };

        var count = await source.SelectParallelStreamAsync(
            async (x, ct) =>
            {
                await Task.Delay(3, ct);
                return x * 2;
            },
            options).CountAsync();

        count.Should().Be(60);

        // Wait for callbacks
        await Task.Delay(200);

        concurrencyLevels.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AdaptiveConcurrency_WithOrderedOutput_MaintainsOrder()
    {
        var source = Enumerable.Range(1, 30);

        var options = new ParallelOptionsRivulet
        {
            OrderedOutput = true,
            AdaptiveConcurrency = new AdaptiveConcurrencyOptions
            {
                MinConcurrency = 1,
                MaxConcurrency = 6,
                InitialConcurrency = 3,
                SampleInterval = TimeSpan.FromMilliseconds(100),
                MinSuccessRate = 0.5
            }
        };

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(Random.Shared.Next(1, 10), ct);
                return x * 2;
            },
            options);

        results.Should().HaveCount(30);
        results.Should().BeInAscendingOrder();
        results.Should().Equal(Enumerable.Range(1, 30).Select(x => x * 2));
    }

    [Fact]
    public async Task AdaptiveConcurrency_WithCancellation_CancelsCorrectly()
    {
        var source = Enumerable.Range(1, 100);
        var cts = new CancellationTokenSource();

        var options = new ParallelOptionsRivulet
        {
            AdaptiveConcurrency = new AdaptiveConcurrencyOptions
            {
                MinConcurrency = 1,
                MaxConcurrency = 8,
                SampleInterval = TimeSpan.FromMilliseconds(100)
            }
        };

        var processedCount = 0;

        var task = source.SelectParallelAsync(
            async (x, ct) =>
            {
                Interlocked.Increment(ref processedCount);
                if (processedCount >= 10)
                    await cts.CancelAsync();

                await Task.Delay(5, ct);
                return x * 2;
            },
            options,
            cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);

        processedCount.Should().BeLessThan(100);
    }

    [Fact]
    public void AdaptiveConcurrencyController_Dispose_StopsAdjustments()
    {
        var controller = new AdaptiveConcurrencyController(new AdaptiveConcurrencyOptions
        {
            MinConcurrency = 1,
            MaxConcurrency = 10,
            SampleInterval = TimeSpan.FromMilliseconds(10)
        });

        controller.Dispose();

        var act = () => controller.Dispose();
        act.Should().NotThrow();
    }
}
