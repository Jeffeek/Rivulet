using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Rivulet.Core.Resilience;

namespace Rivulet.Core.Tests;

[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public sealed class BackoffStrategyTests
{
    [Fact]
    public async Task BackoffStrategy_Exponential_RetriesSuccessfully()
    {
        var attemptCounts = new ConcurrentDictionary<int, int>();

        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 3,
            BaseDelay = TimeSpan.FromMilliseconds(10),
            BackoffStrategy = BackoffStrategy.Exponential,
            IsTransient = static _ => true
        };


        var results = await new[] { 1, 2, 3 }.SelectParallelAsync(
            (x, _) =>
            {
                var attempts = attemptCounts.AddOrUpdate(x, 1, static (_, count) => count + 1);
                return x == 2 && attempts <= 2
                    ? throw new InvalidOperationException("Transient error")
                    : new ValueTask<int>(x * 2);
            },
            options);
        results.Count.ShouldBe(3);
        attemptCounts[2].ShouldBe(3);
    }

    [Fact]
    public async Task BackoffStrategy_ExponentialJitter_RetriesSuccessfully()
    {
        var attemptCounts = new ConcurrentDictionary<int, int>();

        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 3,
            BaseDelay = TimeSpan.FromMilliseconds(10),
            BackoffStrategy = BackoffStrategy.ExponentialJitter,
            IsTransient = static _ => true
        };

        var results = await new[] { 1, 2, 3 }.SelectParallelAsync(
            (x, _) =>
            {
                var attempts = attemptCounts.AddOrUpdate(x, 1, static (_, count) => count + 1);
                return x == 2 && attempts <= 2
                    ? throw new InvalidOperationException("Transient error")
                    : new ValueTask<int>(x * 2);
            },
            options);
        results.Count.ShouldBe(3);
        attemptCounts[2].ShouldBe(3);
    }

    [Fact]
    public async Task BackoffStrategy_DecorrelatedJitter_RetriesSuccessfully()
    {
        var attemptCounts = new ConcurrentDictionary<int, int>();

        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 3,
            BaseDelay = TimeSpan.FromMilliseconds(10),
            BackoffStrategy = BackoffStrategy.DecorrelatedJitter,
            IsTransient = static _ => true
        };

        var results = await new[] { 1, 2, 3 }.SelectParallelAsync(
            (x, _) =>
            {
                var attempts = attemptCounts.AddOrUpdate(x, 1, static (_, count) => count + 1);
                return x == 2 && attempts <= 2
                    ? throw new InvalidOperationException("Transient error")
                    : new ValueTask<int>(x * 2);
            },
            options);

        results.Count.ShouldBe(3);
        attemptCounts[2].ShouldBe(3);
    }

    [Fact]
    public async Task BackoffStrategy_Linear_RetriesSuccessfully()
    {
        var attemptCounts = new ConcurrentDictionary<int, int>();

        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 3,
            BaseDelay = TimeSpan.FromMilliseconds(10),
            BackoffStrategy = BackoffStrategy.Linear,
            IsTransient = static _ => true
        };

        var results = await new[] { 1, 2, 3 }.SelectParallelAsync(
            (x, _) =>
            {
                var attempts = attemptCounts.AddOrUpdate(x, 1, static (_, count) => count + 1);
                return x == 2 && attempts <= 2
                    ? throw new InvalidOperationException("Transient error")
                    : new ValueTask<int>(x * 2);
            },
            options);

        results.Count.ShouldBe(3);
        attemptCounts[2].ShouldBe(3);
    }

    [Fact]
    public async Task BackoffStrategy_LinearJitter_RetriesSuccessfully()
    {
        var attemptCounts = new ConcurrentDictionary<int, int>();

        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 3,
            BaseDelay = TimeSpan.FromMilliseconds(10),
            BackoffStrategy = BackoffStrategy.LinearJitter,
            IsTransient = static _ => true
        };

        var results = await new[] { 1, 2, 3 }.SelectParallelAsync(
            (x, _) =>
            {
                var attempts = attemptCounts.AddOrUpdate(x, 1, static (_, count) => count + 1);
                return x == 2 && attempts <= 2
                    ? throw new InvalidOperationException("Transient error")
                    : new ValueTask<int>(x * 2);
            },
            options);

        results.Count.ShouldBe(3);
        attemptCounts[2].ShouldBe(3);
    }

    [Fact]
    public void BackoffStrategy_DefaultIsExponential()
    {
        var options = new ParallelOptionsRivulet();
        options.BackoffStrategy.ShouldBe(BackoffStrategy.Exponential);
    }

    [Fact]
    public async Task BackoffStrategy_WorksWithSelectParallelStreamAsync()
    {
        var source = Enumerable.Range(1, 10).ToAsyncEnumerable();
        var attemptCounts = new ConcurrentDictionary<int, int>();

        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 2,
            BaseDelay = TimeSpan.FromMilliseconds(10),
            BackoffStrategy = BackoffStrategy.ExponentialJitter,
            IsTransient = static _ => true,
            ErrorMode = ErrorMode.BestEffort
        };

        var results = await source.SelectParallelStreamAsync(
                (x, _) =>
                {
                    var attempts = attemptCounts.AddOrUpdate(x, 1, static (_, count) => count + 1);
                    return x == 5 && attempts <= 2
                        ? throw new InvalidOperationException("Transient error")
                        : new ValueTask<int>(x * 2);
                },
                options)
            .ToListAsync();

        results.Count.ShouldBe(10);
        attemptCounts[5].ShouldBe(3);
    }

    [Fact]
    public async Task BackoffStrategy_CancellationDuringDelay_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        var attemptCount = 0;

        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 5,
            BaseDelay = TimeSpan.FromSeconds(10),
            BackoffStrategy = BackoffStrategy.Exponential,
            IsTransient = static _ => true
        };

        var act = () => new[] { 1 }.SelectParallelAsync<int, int>(
            (_, _) =>
            {
                attemptCount++;
                if (attemptCount == 1)
                {
                    Task.Run(async () =>
                        {
                            await Task.Delay(100, cts.Token);
                            await cts.CancelAsync();
                        },
                        cts.Token);
                }

                throw new InvalidOperationException("Transient error");
            },
            options,
            cts.Token);

        await act.ShouldThrowAsync<OperationCanceledException>();
        attemptCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task BackoffStrategy_MultipleItemsRetrying_UsesIndependentDelayCalculations()
    {
        var source = Enumerable.Range(1, 5);
        var attemptCounts = new ConcurrentDictionary<int, int>();

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 5,
            MaxRetries = 2,
            BaseDelay = TimeSpan.FromMilliseconds(10),
            BackoffStrategy = BackoffStrategy.DecorrelatedJitter,
            IsTransient = static _ => true
        };

        var results = await source.SelectParallelAsync(
            (x, _) =>
            {
                var attempts = attemptCounts.AddOrUpdate(x, 1, static (_, count) => count + 1);
                return attempts <= 2
                    ? throw new InvalidOperationException($"Transient error for {x}")
                    : new ValueTask<int>(x * 2);
            },
            options);

        results.Count.ShouldBe(5);
        attemptCounts.Values.ShouldAllBe(static count => count == 3);
    }

    [Fact]
    public async Task BackoffStrategy_WithErrorModeCollectAndContinue_StillRetriesBeforeFailing()
    {
        var source = Enumerable.Range(1, 3);
        var attemptCounts = new ConcurrentDictionary<int, int>();

        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 2,
            BaseDelay = TimeSpan.FromMilliseconds(10),
            BackoffStrategy = BackoffStrategy.Linear,
            IsTransient = static _ => true,
            ErrorMode = ErrorMode.CollectAndContinue
        };

        var act = () => source.SelectParallelAsync(
            (x, _) =>
            {
                attemptCounts.AddOrUpdate(x, 1, static (_, count) => count + 1);
                return x == 2 ? throw new InvalidOperationException("Always fails") : new ValueTask<int>(x * 2);
            },
            options);

        await act.ShouldThrowAsync<AggregateException>();
        attemptCounts[2].ShouldBe(3);
    }

    [Fact]
    public void BackoffStrategy_AllEnumValuesDefined() =>
        Enum.GetValues<BackoffStrategy>()
            .ShouldBe([
                BackoffStrategy.Exponential,
                BackoffStrategy.ExponentialJitter,
                BackoffStrategy.DecorrelatedJitter,
                BackoffStrategy.Linear,
                BackoffStrategy.LinearJitter
            ]);

    [Fact]
    public async Task BackoffStrategy_WithForEachParallelAsync_RetriesCorrectly()
    {
        var source = Enumerable.Range(1, 10).ToAsyncEnumerable();
        var processedCount = 0;
        var attemptCounts = new ConcurrentDictionary<int, int>();

        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 2,
            BaseDelay = TimeSpan.FromMilliseconds(10),
            BackoffStrategy = BackoffStrategy.ExponentialJitter,
            IsTransient = static _ => true,
            ErrorMode = ErrorMode.BestEffort
        };

        await source.ForEachParallelAsync(
            (x, _) =>
            {
                var attempts = attemptCounts.AddOrUpdate(x, 1, static (_, count) => count + 1);
                if (x == 5 && attempts <= 2) throw new InvalidOperationException("Transient error");

                Interlocked.Increment(ref processedCount);
                return ValueTask.CompletedTask;
            },
            options);

        processedCount.ShouldBe(10);
        attemptCounts[5].ShouldBe(3);
    }

    [Fact]
    public async Task BackoffStrategy_BackwardCompatibility_DefaultBehaviorUnchanged()
    {
        var attemptCounts = new ConcurrentDictionary<int, int>();

        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 2, BaseDelay = TimeSpan.FromMilliseconds(10), IsTransient = static _ => true
        };

        var results = await new[] { 1 }.SelectParallelAsync(
            (x, _) =>
            {
                var attempts = attemptCounts.AddOrUpdate(x, 1, static (_, count) => count + 1);
                return attempts <= 2 ? throw new InvalidOperationException("Transient error") : new ValueTask<int>(x);
            },
            options);

        results.Count.ShouldBe(1);
        results[0].ShouldBe(1);
        attemptCounts[1].ShouldBe(3);
    }

    [Fact]
    public async Task BackoffStrategy_DifferentStrategies_AllWorkIndependently()
    {
        var strategies = new[]
        {
            BackoffStrategy.Exponential,
            BackoffStrategy.ExponentialJitter,
            BackoffStrategy.DecorrelatedJitter,
            BackoffStrategy.Linear,
            BackoffStrategy.LinearJitter
        };

        foreach (var strategy in strategies)
        {
            var attemptCounts = new ConcurrentDictionary<int, int>();

            var options = new ParallelOptionsRivulet
            {
                MaxRetries = 2, BaseDelay = TimeSpan.FromMilliseconds(5), BackoffStrategy = strategy,
                IsTransient = static _ => true
            };

            var results = await new[] { 1 }.SelectParallelAsync(
                (x, _) =>
                {
                    var attempts = attemptCounts.AddOrUpdate(x, 1, static (_, count) => count + 1);
                    return attempts <= 2
                        ? throw new InvalidOperationException($"Test {strategy}")
                        : new ValueTask<int>(x);
                },
                options);

            results.Count.ShouldBe(1);
            attemptCounts[1].ShouldBe(3, $"strategy {strategy} should retry correctly");
        }
    }

    [Fact]
    public async Task BackoffStrategy_WithOrderedOutput_RetriesCorrectly()
    {
        var attemptCounts = new ConcurrentDictionary<int, int>();

        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 2,
            BaseDelay = TimeSpan.FromMilliseconds(10),
            BackoffStrategy = BackoffStrategy.ExponentialJitter,
            IsTransient = static _ => true,
            OrderedOutput = true
        };

        var results = await Enumerable.Range(1, 5)
            .SelectParallelAsync(
                (x, _) =>
                {
                    var attempts = attemptCounts.AddOrUpdate(x, 1, static (_, count) => count + 1);
                    return x == 3 && attempts <= 2
                        ? throw new InvalidOperationException("Transient error")
                        : new ValueTask<int>(x * 2);
                },
                options);

        results.ShouldBe([2, 4, 6, 8, 10]);
        attemptCounts[3].ShouldBe(3);
    }

    [Fact]
    public async Task BackoffStrategy_InvalidEnumValue_FallsBackToExponential()
    {
        var attemptCounts = new ConcurrentDictionary<int, int>();

        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 2,
            BaseDelay = TimeSpan.FromMilliseconds(10),
            BackoffStrategy = (BackoffStrategy)999,
            IsTransient = static _ => true
        };

        var results = await new[] { 1 }.SelectParallelAsync(
            (x, _) =>
            {
                var attempts = attemptCounts.AddOrUpdate(x, 1, static (_, count) => count + 1);
                return attempts <= 2 ? throw new InvalidOperationException("Transient error") : new ValueTask<int>(x);
            },
            options);

        results.Count.ShouldBe(1);
        results[0].ShouldBe(1);
        attemptCounts[1].ShouldBe(3);
    }

    [Fact]
    public async Task BackoffStrategy_AllStrategiesInSwitchCovered()
    {
        var strategies = new[]
        {
            BackoffStrategy.Exponential,
            BackoffStrategy.ExponentialJitter,
            BackoffStrategy.DecorrelatedJitter,
            BackoffStrategy.Linear,
            BackoffStrategy.LinearJitter,
            (BackoffStrategy)999
        };

        foreach (var strategy in strategies)
        {
            var attemptCounts = new ConcurrentDictionary<int, int>();

            var options = new ParallelOptionsRivulet
            {
                MaxRetries = 1, BaseDelay = TimeSpan.FromMilliseconds(5), BackoffStrategy = strategy,
                IsTransient = static _ => true
            };

            var results = await new[] { 1 }.SelectParallelAsync(
                (x, _) =>
                {
                    var attempts = attemptCounts.AddOrUpdate(x, 1, static (_, count) => count + 1);
                    return attempts <= 1
                        ? throw new InvalidOperationException($"Test {strategy}")
                        : new ValueTask<int>(x);
                },
                options);

            results.Count.ShouldBe(1, $"strategy {strategy} should complete successfully");
        }
    }
}
