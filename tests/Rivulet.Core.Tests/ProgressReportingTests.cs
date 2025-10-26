using FluentAssertions;
using System.Collections.Concurrent;

namespace Rivulet.Core.Tests;

public class ProgressReportingTests
{
    [Fact]
    public async Task ProgressReporting_SelectParallelAsync_ReportsProgress()
    {
        var snapshots = new ConcurrentBag<ProgressSnapshot>();
        var source = Enumerable.Range(1, 100);

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 10,
            Progress = new ProgressOptions
            {
                ReportInterval = TimeSpan.FromMilliseconds(50),
                OnProgress = snapshot =>
                {
                    snapshots.Add(snapshot);
                    return ValueTask.CompletedTask;
                }
            }
        };

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x * 2;
            },
            options);

        results.Should().HaveCount(100);
        snapshots.Should().NotBeEmpty();

        var maxCompleted = snapshots.Max(s => s.ItemsCompleted);
        maxCompleted.Should().BeGreaterThan(50);

        snapshots.All(s => s.TotalItems == 100).Should().BeTrue();
        snapshots.Where(s => s.ItemsCompleted > 0).All(s => s.ItemsPerSecond > 0).Should().BeTrue();
        snapshots.Where(s => s.ItemsCompleted > 0).All(s => s.PercentComplete is >= 0 and <= 100).Should().BeTrue();
    }

    [Fact]
    public async Task ProgressReporting_SelectParallelStreamAsync_ReportsProgress()
    {
        var snapshots = new ConcurrentBag<ProgressSnapshot>();
        var source = Enumerable.Range(1, 50).ToAsyncEnumerable();

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 5,
            Progress = new ProgressOptions
            {
                ReportInterval = TimeSpan.FromMilliseconds(50),
                OnProgress = snapshot =>
                {
                    snapshots.Add(snapshot);
                    return ValueTask.CompletedTask;
                }
            }
        };

        var results = await source.SelectParallelStreamAsync(
            async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x * 2;
            },
            options).ToListAsync();

        results.Should().HaveCount(50);
        snapshots.Should().NotBeEmpty();

        var maxCompleted = snapshots.Max(s => s.ItemsCompleted);
        maxCompleted.Should().BeGreaterThan(10);

        snapshots.All(s => s.TotalItems == null).Should().BeTrue();
        snapshots.All(s => s.PercentComplete == null).Should().BeTrue();
        snapshots.Where(s => s.ItemsCompleted > 0).All(s => s.ItemsPerSecond > 0).Should().BeTrue();
    }

    [Fact]
    public async Task ProgressReporting_TracksErrorCount()
    {
        var snapshots = new ConcurrentBag<ProgressSnapshot>();
        var source = Enumerable.Range(1, 20);

        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 0,
            ErrorMode = ErrorMode.BestEffort,
            Progress = new ProgressOptions
            {
                ReportInterval = TimeSpan.FromMilliseconds(50),
                OnProgress = snapshot =>
                {
                    snapshots.Add(snapshot);
                    return ValueTask.CompletedTask;
                }
            }
        };

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(5, ct);
                if (x % 5 == 0)
                    throw new InvalidOperationException($"Error on {x}");
                return x * 2;
            },
            options);

        results.Should().HaveCount(16); // 20 - 4 errors
        snapshots.Should().NotBeEmpty();

        // Use Max to avoid race condition - the final snapshot might not be last in the bag
        var maxErrors = snapshots.Max(s => s.ErrorCount);
        var maxCompleted = snapshots.Max(s => s.ItemsCompleted);

        maxErrors.Should().Be(4); // Should eventually report all 4 errors
        maxCompleted.Should().Be(16); // Should eventually report all 16 completed
    }

    [Fact]
    public async Task ProgressReporting_CalculatesItemsPerSecond()
    {
        var snapshots = new ConcurrentBag<ProgressSnapshot>();
        var source = Enumerable.Range(1, 50);

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 10,
            Progress = new ProgressOptions
            {
                ReportInterval = TimeSpan.FromMilliseconds(100),
                OnProgress = snapshot =>
                {
                    snapshots.Add(snapshot);
                    return ValueTask.CompletedTask;
                }
            }
        };

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(20, ct);
                return x;
            },
            options);

        results.Should().HaveCount(50);
        snapshots.Should().NotBeEmpty();

        var progressWithItems = snapshots.Where(s => s.ItemsCompleted > 0).ToList();
        progressWithItems.Should().NotBeEmpty();
        progressWithItems.All(s => s.ItemsPerSecond > 0).Should().BeTrue();
    }

    [Fact]
    public async Task ProgressReporting_CalculatesEstimatedTimeRemaining()
    {
        var snapshotsWithEta = new ConcurrentBag<ProgressSnapshot>();
        var source = Enumerable.Range(1, 100);

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 10,
            Progress = new ProgressOptions
            {
                ReportInterval = TimeSpan.FromMilliseconds(50),
                OnProgress = snapshot =>
                {
                    if (snapshot.EstimatedTimeRemaining.HasValue)
                        snapshotsWithEta.Add(snapshot);
                    return ValueTask.CompletedTask;
                }
            }
        };

        await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x;
            },
            options);

        snapshotsWithEta.Should().NotBeEmpty();
        snapshotsWithEta.All(s => s.EstimatedTimeRemaining.HasValue).Should().BeTrue();
    }

    [Fact]
    public async Task ProgressReporting_CalculatesPercentComplete()
    {
        var snapshots = new ConcurrentBag<ProgressSnapshot>();
        var source = Enumerable.Range(1, 50);

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 5,
            Progress = new ProgressOptions
            {
                ReportInterval = TimeSpan.FromMilliseconds(50),
                OnProgress = snapshot =>
                {
                    snapshots.Add(snapshot);
                    return ValueTask.CompletedTask;
                }
            }
        };

        await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x;
            },
            options);

        snapshots.Should().NotBeEmpty();
        var progressSnapshots = snapshots.Where(s => s.PercentComplete.HasValue).ToList();
        progressSnapshots.Should().NotBeEmpty();
        progressSnapshots.All(s => s.PercentComplete is >= 0 and <= 100).Should().BeTrue();
    }

    [Fact]
    public async Task ProgressReporting_NoProgress_WhenProgressIsNull()
    {
        var source = Enumerable.Range(1, 10);

        var options = new ParallelOptionsRivulet
        {
            Progress = null
        };

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(5, ct);
                return x * 2;
            },
            options);

        results.Should().HaveCount(10);
    }

    [Fact]
    public async Task ProgressReporting_TracksStartedItems()
    {
        var snapshots = new ConcurrentBag<ProgressSnapshot>();
        var source = Enumerable.Range(1, 30);

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 5,
            Progress = new ProgressOptions
            {
                ReportInterval = TimeSpan.FromMilliseconds(50),
                OnProgress = snapshot =>
                {
                    snapshots.Add(snapshot);
                    return ValueTask.CompletedTask;
                }
            }
        };

        await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(20, ct);
                return x;
            },
            options);

        snapshots.Should().NotBeEmpty();
        snapshots.All(s => s.ItemsStarted >= s.ItemsCompleted).Should().BeTrue();
    }

    [Fact]
    public async Task ProgressReporting_WorksWithOrderedOutput()
    {
        var snapshots = new ConcurrentBag<ProgressSnapshot>();
        var source = Enumerable.Range(1, 40);

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 8,
            OrderedOutput = true,
            Progress = new ProgressOptions
            {
                ReportInterval = TimeSpan.FromMilliseconds(50),
                OnProgress = snapshot =>
                {
                    snapshots.Add(snapshot);
                    return ValueTask.CompletedTask;
                }
            }
        };

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x * 2;
            },
            options);

        results.Should().Equal(Enumerable.Range(1, 40).Select(x => x * 2));
        snapshots.Should().NotBeEmpty();
        snapshots.Max(s => s.ItemsCompleted).Should().BeGreaterThan(20);
    }

    [Fact]
    public async Task ProgressReporting_WorksWithForEachParallelAsync()
    {
        var snapshots = new ConcurrentBag<ProgressSnapshot>();
        var source = Enumerable.Range(1, 30).ToAsyncEnumerable();
        var processedCount = 0;

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 5,
            Progress = new ProgressOptions
            {
                ReportInterval = TimeSpan.FromMilliseconds(50),
                OnProgress = snapshot =>
                {
                    snapshots.Add(snapshot);
                    return ValueTask.CompletedTask;
                }
            }
        };

        await source.ForEachParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(10, ct);
                Interlocked.Increment(ref processedCount);
            },
            options);

        processedCount.Should().Be(30);
        snapshots.Should().NotBeEmpty();
        snapshots.Max(s => s.ItemsCompleted).Should().BeGreaterThan(10);
    }

    [Fact]
    public async Task ProgressReporting_CallbackExceptionDoesNotBreakOperation()
    {
        var source = Enumerable.Range(1, 20);
        var callbackCount = 0;

        var options = new ParallelOptionsRivulet
        {
            Progress = new ProgressOptions
            {
                ReportInterval = TimeSpan.FromMilliseconds(50),
                OnProgress = _ =>
                {
                    Interlocked.Increment(ref callbackCount);
                    throw new InvalidOperationException("Callback error");
                }
            }
        };

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(5, ct);
                return x * 2;
            },
            options);

        results.Should().HaveCount(20);
        callbackCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ProgressReporting_ElapsedTimeIncreases()
    {
        var snapshots = new ConcurrentBag<ProgressSnapshot>();
        var source = Enumerable.Range(1, 50);

        var options = new ParallelOptionsRivulet
        {
            Progress = new ProgressOptions
            {
                ReportInterval = TimeSpan.FromMilliseconds(50),
                OnProgress = snapshot =>
                {
                    snapshots.Add(snapshot);
                    return ValueTask.CompletedTask;
                }
            }
        };

        await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x;
            },
            options);

        snapshots.Should().NotBeEmpty();
        var orderedSnapshots = snapshots.OrderBy(s => s.Elapsed).ToList();
        for (var i = 1; i < orderedSnapshots.Count; i++)
        {
            orderedSnapshots[i].Elapsed.Should().BeGreaterThanOrEqualTo(orderedSnapshots[i - 1].Elapsed);
        }
    }

    [Fact]
    public async Task ProgressReporting_StreamingWithOrderedOutput()
    {
        var snapshots = new ConcurrentBag<ProgressSnapshot>();
        var source = Enumerable.Range(1, 30).ToAsyncEnumerable();

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 5,
            OrderedOutput = true,
            Progress = new ProgressOptions
            {
                ReportInterval = TimeSpan.FromMilliseconds(50),
                OnProgress = snapshot =>
                {
                    snapshots.Add(snapshot);
                    return ValueTask.CompletedTask;
                }
            }
        };

        var results = await source.SelectParallelStreamAsync(
            async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x * 2;
            },
            options).ToListAsync();

        results.Should().Equal(Enumerable.Range(1, 30).Select(x => x * 2));
        snapshots.Should().NotBeEmpty();
        snapshots.Max(s => s.ItemsCompleted).Should().BeGreaterThan(10);
    }

    [Fact]
    public async Task ProgressReporting_TracksErrorsInBestEffortMode()
    {
        var snapshots = new ConcurrentBag<ProgressSnapshot>();
        var source = Enumerable.Range(1, 15);

        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 0,
            ErrorMode = ErrorMode.BestEffort,
            Progress = new ProgressOptions
            {
                ReportInterval = TimeSpan.FromMilliseconds(50),
                OnProgress = snapshot =>
                {
                    snapshots.Add(snapshot);
                    return ValueTask.CompletedTask;
                }
            }
        };

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(5, ct);
                if (x % 5 == 0)
                    throw new InvalidOperationException("Permanent error");
                return x;
            },
            options);

        results.Should().HaveCount(12);
        snapshots.Should().NotBeEmpty();
        var maxErrors = snapshots.Max(s => s.ErrorCount);
        maxErrors.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public Task ProgressOptions_DefaultValues_AreCorrect()
    {
        var options = new ProgressOptions();

        options.ReportInterval.Should().Be(TimeSpan.FromSeconds(5));
        options.OnProgress.Should().BeNull();
        return Task.CompletedTask;
    }

    [Fact]
    public Task ProgressSnapshot_AllProperties_AreReadable()
    {
        var snapshot = new ProgressSnapshot
        {
            ItemsStarted = 10,
            ItemsCompleted = 8,
            TotalItems = 100,
            ErrorCount = 2,
            Elapsed = TimeSpan.FromSeconds(5),
            ItemsPerSecond = 1.6,
            EstimatedTimeRemaining = TimeSpan.FromSeconds(57.5),
            PercentComplete = 8.0
        };

        snapshot.ItemsStarted.Should().Be(10);
        snapshot.ItemsCompleted.Should().Be(8);
        snapshot.TotalItems.Should().Be(100);
        snapshot.ErrorCount.Should().Be(2);
        snapshot.Elapsed.Should().Be(TimeSpan.FromSeconds(5));
        snapshot.ItemsPerSecond.Should().Be(1.6);
        snapshot.EstimatedTimeRemaining.Should().Be(TimeSpan.FromSeconds(57.5));
        snapshot.PercentComplete.Should().Be(8.0);

        return Task.CompletedTask;
    }

    [Fact]
    public async Task ProgressReporting_CancellationStopsReporting()
    {
        var snapshots = new ConcurrentBag<ProgressSnapshot>();
        var source = Enumerable.Range(1, 1000);
        var cts = new CancellationTokenSource();

        var options = new ParallelOptionsRivulet
        {
            Progress = new ProgressOptions
            {
                ReportInterval = TimeSpan.FromMilliseconds(50),
                OnProgress = snapshot =>
                {
                    snapshots.Add(snapshot);
                    if (snapshot.ItemsCompleted >= 20)
                        cts.Cancel();
                    return ValueTask.CompletedTask;
                }
            }
        };

        var act = async () => await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x;
            },
            options,
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        snapshots.Should().NotBeEmpty();
    }
}
