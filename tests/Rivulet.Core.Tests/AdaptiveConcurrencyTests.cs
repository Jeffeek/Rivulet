using System.Diagnostics.CodeAnalysis;
using Rivulet.Base.Tests;
using Rivulet.Core.Resilience;

namespace Rivulet.Core.Tests;

[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public sealed class AdaptiveConcurrencyTests
{
    [Fact]
    public void AdaptiveConcurrencyOptions_Validation_ThrowsOnInvalidMinConcurrency()
    {
        var act = static () => new AdaptiveConcurrencyOptions { MinConcurrency = 0 }.Validate();
        act.ShouldThrow<ArgumentException>().Message.ShouldContain("MinConcurrency");
    }

    [Fact]
    public void AdaptiveConcurrencyOptions_Validation_ThrowsWhenMaxLessThanMin()
    {
        var act = static () => new AdaptiveConcurrencyOptions { MinConcurrency = 10, MaxConcurrency = 5 }.Validate();
        act.ShouldThrow<ArgumentException>().Message.ShouldContain("MaxConcurrency");
    }

    [Fact]
    public void AdaptiveConcurrencyOptions_Validation_ThrowsOnInvalidInitialConcurrency()
    {
        var act = static () => new AdaptiveConcurrencyOptions
            { MinConcurrency = 5, MaxConcurrency = 10, InitialConcurrency = 15 }.Validate();

        act.ShouldThrow<ArgumentException>().Message.ShouldContain("InitialConcurrency");
    }

    [Fact]
    public void AdaptiveConcurrencyOptions_Validation_ThrowsOnInvalidSampleInterval()
    {
        var act = static () => new AdaptiveConcurrencyOptions { SampleInterval = TimeSpan.Zero }.Validate();
        act.ShouldThrow<ArgumentException>().Message.ShouldContain("SampleInterval");
    }

    [Fact]
    public void AdaptiveConcurrencyOptions_Validation_ThrowsOnInvalidTargetLatency()
    {
        var act = static () => new AdaptiveConcurrencyOptions { TargetLatency = TimeSpan.Zero }.Validate();
        act.ShouldThrow<ArgumentException>().Message.ShouldContain("TargetLatency");
    }

    [Fact]
    public void AdaptiveConcurrencyOptions_Validation_ThrowsOnInvalidMinSuccessRate()
    {
        var act = static () => new AdaptiveConcurrencyOptions { MinSuccessRate = 1.5 }.Validate();
        act.ShouldThrow<ArgumentException>().Message.ShouldContain("MinSuccessRate");
    }

    [Fact]
    public void AdaptiveConcurrencyController_Constructor_ThrowsOnNullOptions()
    {
        var act = static () => new AdaptiveConcurrencyController(null!);
        act.ShouldThrow<ArgumentNullException>().Message.ShouldContain("options");
    }

    [Fact]
    public async Task AdaptiveConcurrencyController_InitialConcurrency_UsesConfiguredValue()
    {
        await using var controller = new AdaptiveConcurrencyController(new()
        {
            MinConcurrency = 5, MaxConcurrency = 20, InitialConcurrency = 10
        });

        controller.CurrentConcurrency.ShouldBe(10);
    }

    [Fact]
    public async Task AdaptiveConcurrencyController_InitialConcurrency_DefaultsToMin()
    {
        await using var controller =
            new AdaptiveConcurrencyController(new() { MinConcurrency = 5, MaxConcurrency = 20 });

        controller.CurrentConcurrency.ShouldBe(5);
    }

    [Fact]
    public async Task AdaptiveConcurrency_IncreasesOnSuccess()
    {
        var source = Enumerable.Range(1, 200);
        var concurrencyLevels = new List<int>();

        var options = new ParallelOptionsRivulet
        {
            AdaptiveConcurrency = new()
            {
                MinConcurrency = 1,
                MaxConcurrency = 10,
                InitialConcurrency = 1,
                SampleInterval = TimeSpan.FromMilliseconds(50),
                MinSuccessRate = 0.5,
                OnConcurrencyChange = (_, @new) =>
                {
                    lock (concurrencyLevels) concurrencyLevels.Add(@new);

                    return ValueTask.CompletedTask;
                }
            }
        };

        var results = await source.SelectParallelAsync(static async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x * 2;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Count.ShouldBe(200);

        // Poll for callbacks with timeout
        await DeadlineExtensions.ApplyDeadlineAsync(
            DateTime.UtcNow.AddSeconds(2),
            static () => Task.Delay(50),
            () => concurrencyLevels.Count == 0);

        // Should have increased concurrency at least once
        lock (concurrencyLevels)
        {
            concurrencyLevels.ShouldNotBeEmpty();
            concurrencyLevels.Max().ShouldBeGreaterThan(1);
        }
    }

    [Fact]
    public async Task AdaptiveConcurrency_DecreasesOnHighLatency()
    {
        var source = Enumerable.Range(1, 100);
        var concurrencyLevels = new List<int>();

        var options = new ParallelOptionsRivulet
        {
            AdaptiveConcurrency = new()
            {
                MinConcurrency = 1,
                MaxConcurrency = 10,
                InitialConcurrency = 8,
                SampleInterval = TimeSpan.FromMilliseconds(50),
                TargetLatency = TimeSpan.FromMilliseconds(10),
                MinSuccessRate = 0.5,
                OnConcurrencyChange = (_, @new) =>
                {
                    lock (concurrencyLevels) concurrencyLevels.Add(@new);

                    return ValueTask.CompletedTask;
                }
            }
        };

        var results = await source.SelectParallelAsync(static async (x, ct) =>
            {
                await Task.Delay(50, ct); // Slow operations exceeding target latency
                return x * 2;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Count.ShouldBe(100);

        // Poll for callbacks with timeout
        await DeadlineExtensions.ApplyDeadlineAsync(
            DateTime.UtcNow.AddSeconds(2),
            static () => Task.Delay(50),
            () => concurrencyLevels.Count == 0);

        // Should have decreased concurrency
        lock (concurrencyLevels)
        {
            concurrencyLevels.ShouldNotBeEmpty();
            concurrencyLevels.Min().ShouldBeLessThan(8);
        }
    }

    [Fact]
    public async Task AdaptiveConcurrency_DecreasesOnLowSuccessRate()
    {
        var source = Enumerable.Range(1, 100);
        var concurrencyLevels = new List<int>();

        var options = new ParallelOptionsRivulet
        {
            AdaptiveConcurrency = new()
            {
                MinConcurrency = 1,
                MaxConcurrency = 10,
                InitialConcurrency = 6,
                SampleInterval = TimeSpan.FromMilliseconds(50),
                MinSuccessRate = 0.8,
                OnConcurrencyChange = (_, @new) =>
                {
                    lock (concurrencyLevels) concurrencyLevels.Add(@new);

                    return ValueTask.CompletedTask;
                }
            },
            ErrorMode = ErrorMode.BestEffort
        };

        var results = await source.SelectParallelAsync(static async (x, ct) =>
            {
                await Task.Delay(10, ct); // Longer delay to ensure operations span multiple sample intervals
                // 50% failure rate
                return x % 2 == 0 ? throw new InvalidOperationException("Test failure") : x * 2;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        // Should get only successful results (50% of 100 = 50)
        results.Count.ShouldBeLessThan(100);

        // Poll for callbacks with timeout
        await DeadlineExtensions.ApplyDeadlineAsync(
            DateTime.UtcNow.AddSeconds(2),
            static () => Task.Delay(50),
            () => concurrencyLevels.Count == 0);

        // Should have decreased concurrency due to low success rate
        lock (concurrencyLevels)
        {
            concurrencyLevels.ShouldNotBeEmpty();
            concurrencyLevels.Min().ShouldBeLessThan(6);
        }
    }

    [Fact]
    public async Task AdaptiveConcurrency_RespectsMinMaxBounds()
    {
        var source = Enumerable.Range(1, 200);
        var concurrencyLevels = new List<int>();

        var options = new ParallelOptionsRivulet
        {
            AdaptiveConcurrency = new()
            {
                MinConcurrency = 3,
                MaxConcurrency = 5,
                InitialConcurrency = 4,
                SampleInterval = TimeSpan.FromMilliseconds(50),
                MinSuccessRate = 0.5,
                OnConcurrencyChange = (_, @new) =>
                {
                    lock (concurrencyLevels) concurrencyLevels.Add(@new);

                    return ValueTask.CompletedTask;
                }
            }
        };

        var results = await source.SelectParallelAsync(static async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x * 2;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Count.ShouldBe(200);

        // Poll for callbacks with timeout (may not change if already optimal)
        await Task.Delay(500, CancellationToken.None);

        lock (concurrencyLevels) concurrencyLevels.ShouldAllBe(static c => c >= 3 && c <= 5);
    }

    [Fact]
    public async Task AdaptiveConcurrency_WithStreamingOperations()
    {
        var source = AsyncEnumerable.Range(1, 150);
        var concurrencyLevels = new List<int>();

        var options = new ParallelOptionsRivulet
        {
            AdaptiveConcurrency = new()
            {
                MinConcurrency = 1,
                MaxConcurrency = 8,
                InitialConcurrency = 2,
                SampleInterval = TimeSpan.FromMilliseconds(50),
                MinSuccessRate = 0.5,
                OnConcurrencyChange = (_, @new) =>
                {
                    lock (concurrencyLevels) concurrencyLevels.Add(@new);

                    return ValueTask.CompletedTask;
                }
            }
        };

        var count = await source.SelectParallelStreamAsync(static async (x, ct) =>
                {
                    await Task.Delay(10, ct);
                    return x * 2;
                },
                options,
                cancellationToken: TestContext.Current.CancellationToken)
            .CountAsync();

        count.ShouldBe(150);

        // Poll for callbacks with timeout
        await DeadlineExtensions.ApplyDeadlineAsync(
            DateTime.UtcNow.AddSeconds(2),
            static () => Task.Delay(50),
            () => concurrencyLevels.Count == 0);

        lock (concurrencyLevels) concurrencyLevels.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task AdaptiveConcurrency_WithOrderedOutput_MaintainsOrder()
    {
        var source = Enumerable.Range(1, 30);

        var options = new ParallelOptionsRivulet
        {
            OrderedOutput = true,
            AdaptiveConcurrency = new()
            {
                MinConcurrency = 1,
                MaxConcurrency = 6,
                InitialConcurrency = 3,
                SampleInterval = TimeSpan.FromMilliseconds(100),
                MinSuccessRate = 0.5
            }
        };

        var results = await source.SelectParallelAsync(static async (x, ct) =>
            {
                await Task.Delay(Random.Shared.Next(1, 10), ct);
                return x * 2;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Count.ShouldBe(30);
        results.ShouldBeInOrder();
        results.ShouldBe(Enumerable.Range(1, 30).Select(static x => x * 2));
    }

    [Fact]
    public async Task AdaptiveConcurrency_WithCancellation_CancelsCorrectly()
    {
        var source = Enumerable.Range(1, 100);
        using var cts = new CancellationTokenSource();

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4, // Limit concurrency for more predictable cancellation
            AdaptiveConcurrency = new()
            {
                MinConcurrency = 1,
                MaxConcurrency = 4, // Match MaxDegreeOfParallelism
                SampleInterval = TimeSpan.FromMilliseconds(100)
            }
        };

        var processedCount = 0;
        var cancelRequested = 0; // 0 = false, 1 = true

        var task = source.SelectParallelAsync(
            async (x, ct) =>
            {
                var count = Interlocked.Increment(ref processedCount);

                // Cancel only once, deterministically
                if (count == 10 && cancelRequested == 0 && Interlocked.CompareExchange(ref cancelRequested, 1, 0) == 0)
                    await cts.CancelAsync();

                // Longer delay to ensure cancellation propagates
                await Task.Delay(20, ct);
                return x * 2;
            },
            options,
            cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);

        // With MaxDegreeOfParallelism=4, at most ~14-15 items should be processed
        // (10 to trigger cancel + up to 4 in flight + a few more before cancellation takes effect)
        processedCount.ShouldBeLessThan(25);
        processedCount.ShouldBeGreaterThanOrEqualTo(10);
    }

    [Fact]
    public async Task AdaptiveConcurrencyController_Dispose_StopsAdjustments()
    {
        var controller = new AdaptiveConcurrencyController(new()
        {
            MinConcurrency = 1, MaxConcurrency = 10, SampleInterval = TimeSpan.FromMilliseconds(10)
        });

        await controller.DisposeAsync();

        var act = async () => await controller.DisposeAsync();
        await act.ShouldNotThrowAsync();
    }

    [Fact]
    public async Task AdaptiveConcurrency_AggressiveStrategy_IncreasesQuickly()
    {
        var source = Enumerable.Range(1, 300);
        var concurrencyLevels = new List<int>();

        var options = new ParallelOptionsRivulet
        {
            AdaptiveConcurrency = new()
            {
                MinConcurrency = 1,
                MaxConcurrency = 20,
                InitialConcurrency = 1,
                SampleInterval = TimeSpan.FromMilliseconds(30),
                MinSuccessRate = 0.5,
                IncreaseStrategy = AdaptiveConcurrencyStrategy.Aggressive,
                DecreaseStrategy = AdaptiveConcurrencyStrategy.Aggressive,
                OnConcurrencyChange = (_, @new) =>
                {
                    lock (concurrencyLevels) concurrencyLevels.Add(@new);

                    return ValueTask.CompletedTask;
                }
            }
        };

        var results = await source.SelectParallelAsync(static async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x * 2;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Count.ShouldBe(300);

        await DeadlineExtensions.ApplyDeadlineAsync(
            DateTime.UtcNow.AddSeconds(3),
            static () => Task.Delay(50),
            () => concurrencyLevels.Count == 0);

        lock (concurrencyLevels)
        {
            concurrencyLevels.ShouldNotBeEmpty();
            // Aggressive should increase faster than AIMD
            concurrencyLevels.Max().ShouldBeGreaterThan(5);
        }
    }

    [Fact]
    public async Task AdaptiveConcurrency_GradualStrategy_DecreasesSlowly()
    {
        var source = Enumerable.Range(1, 150);
        var concurrencyLevels = new List<int>();

        var options = new ParallelOptionsRivulet
        {
            AdaptiveConcurrency = new()
            {
                MinConcurrency = 1,
                MaxConcurrency = 15,
                InitialConcurrency = 12,
                SampleInterval = TimeSpan.FromMilliseconds(40),
                TargetLatency = TimeSpan.FromMilliseconds(5),
                MinSuccessRate = 0.5,
                IncreaseStrategy = AdaptiveConcurrencyStrategy.Gradual,
                DecreaseStrategy = AdaptiveConcurrencyStrategy.Gradual,
                OnConcurrencyChange = (_, @new) =>
                {
                    lock (concurrencyLevels) concurrencyLevels.Add(@new);

                    return ValueTask.CompletedTask;
                }
            }
        };

        var results = await source.SelectParallelAsync(static async (x, ct) =>
            {
                await Task.Delay(50, ct); // High latency
                return x * 2;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Count.ShouldBe(150);

        await DeadlineExtensions.ApplyDeadlineAsync(
            DateTime.UtcNow.AddSeconds(3),
            static () => Task.Delay(50),
            () => concurrencyLevels.Count == 0);

        lock (concurrencyLevels)
        {
            concurrencyLevels.ShouldNotBeEmpty();
            // Gradual decreases to 75% each time, slower than AIMD's 50%
            concurrencyLevels.Min().ShouldBeLessThan(12);
        }
    }

    [Fact]
    public async Task AdaptiveConcurrency_CallbackThrowsException_DoesNotCrash()
    {
        var source = Enumerable.Range(1, 100);
        var callbackCount = 0;

        var options = new ParallelOptionsRivulet
        {
            AdaptiveConcurrency = new()
            {
                MinConcurrency = 1,
                MaxConcurrency = 10,
                InitialConcurrency = 1,
                SampleInterval = TimeSpan.FromMilliseconds(30),
                MinSuccessRate = 0.5,
                OnConcurrencyChange = (_, _) =>
                {
                    Interlocked.Increment(ref callbackCount);
                    throw new InvalidOperationException("Callback explosion!");
                }
            }
        };

        var results = await source.SelectParallelAsync(static async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x * 2;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Count.ShouldBe(100);
        // Operation should complete despite callback exceptions
        await Task.Delay(500, CancellationToken.None);
        callbackCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task AdaptiveConcurrency_DisposeDuringSampling_HandlesGracefully()
    {
        await using var controller = new AdaptiveConcurrencyController(new()
        {
            MinConcurrency = 1, MaxConcurrency = 10, InitialConcurrency = 5,
            SampleInterval = TimeSpan.FromMilliseconds(5)
        });

        // Trigger some activity
        await controller.AcquireAsync();
        controller.Release(TimeSpan.FromMilliseconds(10), true);

        await Task.Delay(20, CancellationToken.None); // Let sampling happen

        // Should not throw
        await Task.Delay(50, CancellationToken.None);
    }
}
