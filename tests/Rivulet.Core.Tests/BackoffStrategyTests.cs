using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Rivulet.Core.Resilience;

namespace Rivulet.Core.Tests;

public class BackoffStrategyTests
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
            IsTransient = _ => true
        };


        var results = await new[] { 1, 2, 3 }.SelectParallelAsync(
            (x, _) =>
            {
                var attempts = attemptCounts.AddOrUpdate(x, 1, (_, count) => count + 1);
                if (x == 2 && attempts <= 2)
                    throw new InvalidOperationException("Transient error");
                return new ValueTask<int>(x * 2);
            },
            options);
        results.Should().HaveCount(3);
        attemptCounts[2].Should().Be(3);
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
            IsTransient = _ => true
        };

        var results = await new[] { 1, 2, 3 }.SelectParallelAsync(
            (x, _) =>
            {
                var attempts = attemptCounts.AddOrUpdate(x, 1, (_, count) => count + 1);
                if (x == 2 && attempts <= 2)
                    throw new InvalidOperationException("Transient error");
                return new ValueTask<int>(x * 2);
            },
            options);
        results.Should().HaveCount(3);
        attemptCounts[2].Should().Be(3);
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
            IsTransient = _ => true
        };

        var results = await new[] { 1, 2, 3 }.SelectParallelAsync(
            (x, _) =>
            {
                var attempts = attemptCounts.AddOrUpdate(x, 1, (_, count) => count + 1);
                if (x == 2 && attempts <= 2)
                    throw new InvalidOperationException("Transient error");
                return new ValueTask<int>(x * 2);
            },
            options);

        results.Should().HaveCount(3);
        attemptCounts[2].Should().Be(3);
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
            IsTransient = _ => true
        };

        var results = await new[] { 1, 2, 3 }.SelectParallelAsync(
            (x, _) =>
            {
                var attempts = attemptCounts.AddOrUpdate(x, 1, (_, count) => count + 1);
                if (x == 2 && attempts <= 2)
                    throw new InvalidOperationException("Transient error");
                return new ValueTask<int>(x * 2);
            },
            options);

        results.Should().HaveCount(3);
        attemptCounts[2].Should().Be(3);
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
            IsTransient = _ => true
        };

        var results = await new[] { 1, 2, 3 }.SelectParallelAsync(
            (x, _) =>
            {
                var attempts = attemptCounts.AddOrUpdate(x, 1, (_, count) => count + 1);
                if (x == 2 && attempts <= 2)
                    throw new InvalidOperationException("Transient error");
                return new ValueTask<int>(x * 2);
            },
            options);

        results.Should().HaveCount(3);
        attemptCounts[2].Should().Be(3);
    }

    [Fact]
    public void BackoffStrategy_DefaultIsExponential()
    {
        var options = new ParallelOptionsRivulet();
        options.BackoffStrategy.Should().Be(BackoffStrategy.Exponential);
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
            IsTransient = _ => true,
            ErrorMode = ErrorMode.BestEffort
        };

        var results = await source.SelectParallelStreamAsync(
            (x, _) =>
            {
                var attempts = attemptCounts.AddOrUpdate(x, 1, (_, count) => count + 1);
                if (x == 5 && attempts <= 2)
                    throw new InvalidOperationException("Transient error");
                return new ValueTask<int>(x * 2);
            },
            options).ToListAsync();

        results.Should().HaveCount(10);
        attemptCounts[5].Should().Be(3);
    }

    [Fact]
    [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
    public async Task BackoffStrategy_CancellationDuringDelay_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        var attemptCount = 0;

        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 5,
            BaseDelay = TimeSpan.FromSeconds(10),
            BackoffStrategy = BackoffStrategy.Exponential,
            IsTransient = _ => true
        };

        var act = async () => await new[] { 1 }.SelectParallelAsync<int, int>(
            (_, _) =>
            {
                attemptCount++;
                if (attemptCount == 1)
                    Task.Run(async () =>
                    {
                        await Task.Delay(100, cts.Token);
                        await cts.CancelAsync();
                    }, cts.Token);
                throw new InvalidOperationException("Transient error");
            },
            options,
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        attemptCount.Should().BeGreaterThan(0);
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
            IsTransient = _ => true
        };

        var results = await source.SelectParallelAsync(
            (x, _) =>
            {
                var attempts = attemptCounts.AddOrUpdate(x, 1, (_, count) => count + 1);
                if (attempts <= 2)
                    throw new InvalidOperationException($"Transient error for {x}");
                return new ValueTask<int>(x * 2);
            },
            options);

        results.Should().HaveCount(5);
        attemptCounts.Values.Should().AllSatisfy(count => count.Should().Be(3));
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
            IsTransient = _ => true,
            ErrorMode = ErrorMode.CollectAndContinue
        };

        var act = async () => await source.SelectParallelAsync(
            (x, _) =>
            {
                attemptCounts.AddOrUpdate(x, 1, (_, count) => count + 1);
                if (x == 2)
                    throw new InvalidOperationException("Always fails");
                return new ValueTask<int>(x * 2);
            },
            options);

        await act.Should().ThrowAsync<AggregateException>();
        attemptCounts[2].Should().Be(3);
    }

    [Fact]
    public void BackoffStrategy_AllEnumValuesDefined()
    {
        Enum.GetValues<BackoffStrategy>().Should().Contain([
            BackoffStrategy.Exponential,
            BackoffStrategy.ExponentialJitter,
            BackoffStrategy.DecorrelatedJitter,
            BackoffStrategy.Linear,
            BackoffStrategy.LinearJitter
        ]);
    }

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
            IsTransient = _ => true,
            ErrorMode = ErrorMode.BestEffort
        };

        await source.ForEachParallelAsync(
            (x, _) =>
            {
                var attempts = attemptCounts.AddOrUpdate(x, 1, (_, count) => count + 1);
                if (x == 5 && attempts <= 2)
                    throw new InvalidOperationException("Transient error");
                Interlocked.Increment(ref processedCount);
                return ValueTask.CompletedTask;
            },
            options);

        processedCount.Should().Be(10);
        attemptCounts[5].Should().Be(3);
    }

    [Fact]
    public async Task BackoffStrategy_BackwardCompatibility_DefaultBehaviorUnchanged()
    {
        var attemptCounts = new ConcurrentDictionary<int, int>();

        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 2,
            BaseDelay = TimeSpan.FromMilliseconds(10),
            IsTransient = _ => true
        };

        var results = await new[] { 1 }.SelectParallelAsync(
            (x, _) =>
            {
                var attempts = attemptCounts.AddOrUpdate(x, 1, (_, count) => count + 1);
                if (attempts <= 2)
                    throw new InvalidOperationException("Transient error");
                return new ValueTask<int>(x);
            },
            options);

        results.Should().HaveCount(1);
        results[0].Should().Be(1);
        attemptCounts[1].Should().Be(3);
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
                MaxRetries = 2,
                BaseDelay = TimeSpan.FromMilliseconds(5),
                BackoffStrategy = strategy,
                IsTransient = _ => true
            };

            var results = await new[] { 1 }.SelectParallelAsync(
                (x, _) =>
                {
                    var attempts = attemptCounts.AddOrUpdate(x, 1, (_, count) => count + 1);
                    if (attempts <= 2)
                        throw new InvalidOperationException($"Test {strategy}");
                    return new ValueTask<int>(x);
                },
                options);

            results.Should().HaveCount(1);
            attemptCounts[1].Should().Be(3, $"strategy {strategy} should retry correctly");
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
            IsTransient = _ => true,
            OrderedOutput = true
        };

        var results = await Enumerable.Range(1, 5).SelectParallelAsync(
            (x, _) =>
            {
                var attempts = attemptCounts.AddOrUpdate(x, 1, (_, count) => count + 1);
                if (x == 3 && attempts <= 2)
                    throw new InvalidOperationException("Transient error");
                return new ValueTask<int>(x * 2);
            },
            options);

        results.Should().Equal(new[] { 2, 4, 6, 8, 10 });
        attemptCounts[3].Should().Be(3);
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
            IsTransient = _ => true
        };

        var results = await new[] { 1 }.SelectParallelAsync(
            (x, _) =>
            {
                var attempts = attemptCounts.AddOrUpdate(x, 1, (_, count) => count + 1);
                if (attempts <= 2)
                    throw new InvalidOperationException("Transient error");
                return new ValueTask<int>(x);
            },
            options);

        results.Should().HaveCount(1);
        results[0].Should().Be(1);
        attemptCounts[1].Should().Be(3);
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
                MaxRetries = 1,
                BaseDelay = TimeSpan.FromMilliseconds(5),
                BackoffStrategy = strategy,
                IsTransient = _ => true
            };

            var results = await new[] { 1 }.SelectParallelAsync(
                (x, _) =>
                {
                    var attempts = attemptCounts.AddOrUpdate(x, 1, (_, count) => count + 1);
                    if (attempts <= 1)
                        throw new InvalidOperationException($"Test {strategy}");
                    return new ValueTask<int>(x);
                },
                options);

            results.Should().HaveCount(1, $"strategy {strategy} should complete successfully");
        }
    }
}
