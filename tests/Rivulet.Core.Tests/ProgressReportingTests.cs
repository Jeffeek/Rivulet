using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Rivulet.Base.Tests;
using Rivulet.Core.Observability;

namespace Rivulet.Core.Tests;

[
    Collection(TestCollections.EventSourceSequential),
    SuppressMessage("ReSharper", "AccessToDisposedClosure")
]
public sealed class ProgressReportingTests
{
    [Fact]
    public async Task ProgressReporting_SelectParallelAsync_ReportsProgress()
    {
        var snapshots = new ConcurrentBag<ProgressSnapshot>();
        var source = Enumerable.Range(1, 100);

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 10,
            Progress = new()
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
            static async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x * 2;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Count.ShouldBe(100);
        snapshots.ShouldNotBeEmpty();

        var maxCompleted = snapshots.Max(static s => s.ItemsCompleted);
        maxCompleted.ShouldBeGreaterThan(50);

        snapshots.All(static s => s.TotalItems == 100).ShouldBeTrue();
        snapshots.Where(static s => s.ItemsCompleted > 0).All(static s => s.ItemsPerSecond > 0).ShouldBeTrue();
        snapshots.Where(static s => s.ItemsCompleted > 0).All(static s => s.PercentComplete is >= 0 and <= 100)
            .ShouldBeTrue();
    }

    [Fact]
    public async Task ProgressReporting_SelectParallelStreamAsync_ReportsProgress()
    {
        var snapshots = new ConcurrentBag<ProgressSnapshot>();
        var source = Enumerable.Range(1, 50).ToAsyncEnumerable();

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 5,
            Progress = new()
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
                static async (x, ct) =>
                {
                    await Task.Delay(10, ct);
                    return x * 2;
                },
                options)
            .ToListAsync();

        results.Count.ShouldBe(50);
        snapshots.ShouldNotBeEmpty();

        var maxCompleted = snapshots.Max(static s => s.ItemsCompleted);
        maxCompleted.ShouldBeGreaterThan(10);

        snapshots.All(static s => s.TotalItems == null).ShouldBeTrue();
        snapshots.All(static s => s.PercentComplete == null).ShouldBeTrue();
        snapshots.Where(static s => s.ItemsCompleted > 0).All(static s => s.ItemsPerSecond > 0).ShouldBeTrue();
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
            MaxDegreeOfParallelism = 4, // Limit parallelism to slow down execution
            Progress = new()
            {
                ReportInterval = TimeSpan.FromMilliseconds(30), // Faster reporting for better capture
                OnProgress = snapshot =>
                {
                    snapshots.Add(snapshot);
                    return ValueTask.CompletedTask;
                }
            }
        };

        var results = await source.SelectParallelAsync(
            static async (x, ct) =>
            {
                // Increased delay to ensure operation spans multiple timer intervals
                // This gives the timer adequate time to capture all error states
                await Task.Delay(15, ct);
                return x % 5 == 0 ? throw new InvalidOperationException($"Error on {x}") : x * 2;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        await Task.Yield();

        await DeadlineExtensions.ApplyDeadlineAsync(
            DateTime.UtcNow.AddMilliseconds(2000),
            static () => Task.Delay(50),
            () =>
            {
                if (!snapshots.Any()) return true;

                var currentMaxErrors = snapshots.Max(static s => s.ErrorCount);
                var currentMaxCompleted = snapshots.Max(static s => s.ItemsCompleted);
                return currentMaxErrors != 4 || currentMaxCompleted != 16;
            });

        results.Count.ShouldBe(16, "20 items minus 4 failures (items 5,10,15,20) equals 16 successful results");

        var maxErrors = snapshots.Max(static s => s.ErrorCount);
        var maxCompleted = snapshots.Max(static s => s.ItemsCompleted);

        maxErrors.ShouldBe(4, "items 5,10,15,20 should have failed (4 total errors)");
        maxCompleted.ShouldBe(16, "all 16 non-failing items should have completed successfully");
    }

    [Fact]
    public async Task ProgressReporting_CalculatesItemsPerSecond()
    {
        var snapshots = new ConcurrentBag<ProgressSnapshot>();
        var source = Enumerable.Range(1, 50);

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 10,
            Progress = new()
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
            static async (x, ct) =>
            {
                await Task.Delay(20, ct);
                return x;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Count.ShouldBe(50);
        snapshots.ShouldNotBeEmpty();

        var progressWithItems = snapshots.Where(static s => s.ItemsCompleted > 0).ToList();
        progressWithItems.ShouldNotBeEmpty();
        progressWithItems.All(static s => s.ItemsPerSecond > 0).ShouldBeTrue();
    }

    [Fact]
    public async Task ProgressReporting_CalculatesEstimatedTimeRemaining()
    {
        var snapshotsWithEta = new ConcurrentBag<ProgressSnapshot>();
        var source = Enumerable.Range(1, 100);

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 10,
            Progress = new()
            {
                ReportInterval = TimeSpan.FromMilliseconds(50),
                OnProgress = snapshot =>
                {
                    if (snapshot.EstimatedTimeRemaining.HasValue) snapshotsWithEta.Add(snapshot);

                    return ValueTask.CompletedTask;
                }
            }
        };

        await source.SelectParallelAsync(
            static async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        snapshotsWithEta.ShouldNotBeEmpty();
        snapshotsWithEta.All(static s => s.EstimatedTimeRemaining.HasValue).ShouldBeTrue();
    }

    [Fact]
    public async Task ProgressReporting_CalculatesPercentComplete()
    {
        var snapshots = new ConcurrentBag<ProgressSnapshot>();
        var source = Enumerable.Range(1, 50);

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 5,
            Progress = new()
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
            static async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        snapshots.ShouldNotBeEmpty();
        var progressSnapshots = snapshots.Where(static s => s.PercentComplete.HasValue).ToList();
        progressSnapshots.ShouldNotBeEmpty();
        progressSnapshots.All(static s => s.PercentComplete is >= 0 and <= 100).ShouldBeTrue();
    }

    [Fact]
    public async Task ProgressReporting_NoProgress_WhenProgressIsNull()
    {
        var source = Enumerable.Range(1, 10);

        var options = new ParallelOptionsRivulet { Progress = null };

        var results = await source.SelectParallelAsync(
            static async (x, ct) =>
            {
                await Task.Delay(5, ct);
                return x * 2;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Count.ShouldBe(10);
    }

    [Fact]
    public async Task ProgressReporting_TracksStartedItems()
    {
        var snapshots = new ConcurrentBag<ProgressSnapshot>();
        var source = Enumerable.Range(1, 30);

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 5,
            Progress = new()
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
            static async (x, ct) =>
            {
                await Task.Delay(20, ct);
                return x;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        snapshots.ShouldNotBeEmpty();
        snapshots.All(static s => s.ItemsStarted >= s.ItemsCompleted).ShouldBeTrue();
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
            Progress = new()
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
            static async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x * 2;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        results.ShouldBe(Enumerable.Range(1, 40).Select(static x => x * 2));
        snapshots.ShouldNotBeEmpty();
        snapshots.Max(static s => s.ItemsCompleted).ShouldBeGreaterThan(20);
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
            Progress = new()
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
            async (_, ct) =>
            {
                await Task.Delay(10, ct);
                Interlocked.Increment(ref processedCount);
            },
            options);

        processedCount.ShouldBe(30);
        snapshots.ShouldNotBeEmpty();
        snapshots.Max(static s => s.ItemsCompleted).ShouldBeGreaterThan(10);
    }

    [Fact]
    public async Task ProgressReporting_CallbackExceptionDoesNotBreakOperation()
    {
        var source = Enumerable.Range(1, 20);
        var callbackCount = 0;

        var options = new ParallelOptionsRivulet
        {
            Progress = new()
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
            static async (x, ct) =>
            {
                await Task.Delay(5, ct);
                return x * 2;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Count.ShouldBe(20);
        callbackCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ProgressReporting_ElapsedTimeIncreases()
    {
        var snapshots = new ConcurrentBag<ProgressSnapshot>();
        var source = Enumerable.Range(1, 50);

        var options = new ParallelOptionsRivulet
        {
            Progress = new()
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
            static async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        snapshots.ShouldNotBeEmpty();
        var orderedSnapshots = snapshots.OrderBy(static s => s.Elapsed).ToList();
        for (var i = 1; i < orderedSnapshots.Count; i++)
            orderedSnapshots[i].Elapsed.ShouldBeGreaterThanOrEqualTo(orderedSnapshots[i - 1].Elapsed);
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
            Progress = new()
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
                static async (x, ct) =>
                {
                    await Task.Delay(10, ct);
                    return x * 2;
                },
                options)
            .ToListAsync();

        results.ShouldBe(Enumerable.Range(1, 30).Select(static x => x * 2));
        snapshots.ShouldNotBeEmpty();
        snapshots.Max(static s => s.ItemsCompleted).ShouldBeGreaterThan(10);
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
            Progress = new()
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
            static async (x, ct) =>
            {
                await Task.Delay(5, ct);
                return x % 5 == 0 ? throw new InvalidOperationException("Permanent error") : x;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Count.ShouldBe(12);
        snapshots.ShouldNotBeEmpty();
        var maxErrors = snapshots.Max(static s => s.ErrorCount);
        maxErrors.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public Task ProgressOptions_DefaultValues_AreCorrect()
    {
        var options = new ProgressOptions();

        options.ReportInterval.ShouldBe(TimeSpan.FromSeconds(5));
        options.OnProgress.ShouldBeNull();
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

        snapshot.ItemsStarted.ShouldBe(10);
        snapshot.ItemsCompleted.ShouldBe(8);
        snapshot.TotalItems.ShouldBe(100);
        snapshot.ErrorCount.ShouldBe(2);
        snapshot.Elapsed.ShouldBe(TimeSpan.FromSeconds(5));
        snapshot.ItemsPerSecond.ShouldBe(1.6);
        snapshot.EstimatedTimeRemaining.ShouldBe(TimeSpan.FromSeconds(57.5));
        snapshot.PercentComplete.ShouldBe(8.0);

        return Task.CompletedTask;
    }

    [Fact]
    public async Task ProgressReporting_CancellationStopsReporting()
    {
        var snapshots = new ConcurrentBag<ProgressSnapshot>();
        var source = Enumerable.Range(1, 1000);
        using var cts = new CancellationTokenSource();

        var options = new ParallelOptionsRivulet
        {
            Progress = new()
            {
                ReportInterval = TimeSpan.FromMilliseconds(50),
                OnProgress = snapshot =>
                {
                    snapshots.Add(snapshot);
                    if (snapshot.ItemsCompleted >= 20) cts.Cancel();

                    return ValueTask.CompletedTask;
                }
            }
        };

        var act = () => source.SelectParallelAsync(
            static async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x;
            },
            options,
            cts.Token);

        await act.ShouldThrowAsync<OperationCanceledException>();
        snapshots.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ProgressReporting_WithNullOnProgressCallback_DoesNotThrow()
    {
        var source = Enumerable.Range(1, 20);

        var options = new ParallelOptionsRivulet
            { Progress = new() { ReportInterval = TimeSpan.FromMilliseconds(50), OnProgress = null } };

        var results = await source.SelectParallelAsync(
            static async (x, ct) =>
            {
                await Task.Delay(5, ct);
                return x * 2;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Count.ShouldBe(20);
    }

    [Fact]
    public async Task ProgressReporting_VeryFastCompletion_HandlesZeroElapsedTime()
    {
        var source = Enumerable.Range(1, 5);
        var firstReport = true;

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 10,
            Progress = new()
            {
                ReportInterval = TimeSpan.FromMilliseconds(1),
                OnProgress = snapshot =>
                {
                    if (!firstReport || snapshot.Elapsed.TotalSeconds > 0) return ValueTask.CompletedTask;

                    firstReport = false;
                    return ValueTask.CompletedTask;
                }
            }
        };

        await source.SelectParallelAsync(static async (x, _) =>
            {
                await Task.CompletedTask;
                return x;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ProgressReporting_DoubleDispose_DoesNotThrow()
    {
        var source = Enumerable.Range(1, 10);
        var progressCallbackCount = 0;

        var options = new ParallelOptionsRivulet
        {
            Progress = new()
            {
                ReportInterval = TimeSpan.FromMilliseconds(50),
                OnProgress = _ =>
                {
                    Interlocked.Increment(ref progressCallbackCount);
                    return ValueTask.CompletedTask;
                }
            }
        };

        var results = await source.SelectParallelAsync(
            static async (x, ct) =>
            {
                await Task.Delay(5, ct);
                return x * 2;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Count.ShouldBe(10);
    }

    [Fact]
    public async Task ProgressReporting_SlowCallback_DisposesGracefully()
    {
        var source = Enumerable.Range(1, 10);
        var callbackExecuted = false;

        var options = new ParallelOptionsRivulet
        {
            Progress = new()
            {
                ReportInterval = TimeSpan.FromMilliseconds(50),
                OnProgress = async _ =>
                {
                    callbackExecuted = true;
                    await Task.Delay(100, CancellationToken.None);
                }
            }
        };

        var results = await source.SelectParallelAsync(
            static async (x, ct) =>
            {
                await Task.Delay(5, ct);
                return x;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Count.ShouldBe(10);
        callbackExecuted.ShouldBeTrue();
    }

    [Fact]
    public async Task ProgressReporting_StreamingWithNullOnProgress_DoesNotThrow()
    {
        var source = Enumerable.Range(1, 10).ToAsyncEnumerable();

        var options = new ParallelOptionsRivulet { Progress = new() { OnProgress = null } };

        var results = await source.SelectParallelStreamAsync(
                static async (x, ct) =>
                {
                    await Task.Delay(5, ct);
                    return x * 2;
                },
                options)
            .ToListAsync();

        results.Count.ShouldBe(10);
    }

    [Fact]
    public async Task ProgressReporting_ImmediateCancellation_DisposesCleanly()
    {
        var source = Enumerable.Range(1, 1000);
        using var cts = new CancellationTokenSource();

        var options = new ParallelOptionsRivulet
        {
            Progress = new()
                { ReportInterval = TimeSpan.FromMilliseconds(10), OnProgress = static _ => ValueTask.CompletedTask }
        };

        await cts.CancelAsync();

        var act = () => source.SelectParallelAsync(
            static async (x, ct) =>
            {
                await Task.Delay(100, ct);
                return x;
            },
            options,
            cts.Token);

        await act.ShouldThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ProgressReporting_ReporterTaskException_DisposesGracefully()
    {
        var source = Enumerable.Range(1, 10);
        var reportCount = 0;

        var options = new ParallelOptionsRivulet
        {
            Progress = new()
            {
                ReportInterval = TimeSpan.FromMilliseconds(1),
                OnProgress = _ =>
                {
                    Interlocked.Increment(ref reportCount);
                    return ValueTask.CompletedTask;
                }
            }
        };

        var results = await source.SelectParallelAsync(
            static async (x, ct) =>
            {
                await Task.Delay(5, ct);
                return x;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Count.ShouldBe(10);
        reportCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ProgressReporting_ZeroElapsedTime_CalculatesZeroRate()
    {
        var source = Enumerable.Range(1, 3);

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 10,
            Progress = new()
            {
                ReportInterval = TimeSpan.FromMilliseconds(1),
                OnProgress = static snapshot =>
                {
                    if (snapshot.Elapsed.TotalMilliseconds < 10)
                        snapshot.ItemsPerSecond.ShouldBeGreaterThanOrEqualTo(0);

                    return ValueTask.CompletedTask;
                }
            }
        };

        await source.SelectParallelAsync(static (x, _) => ValueTask.FromResult(x),
            options,
            cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ProgressReporting_MultipleRapidDisposals_HandledSafely()
    {
        var source = Enumerable.Range(1, 5).ToArray();
        var disposalCount = 0;

        var options = new ParallelOptionsRivulet
        {
            Progress = new()
            {
                ReportInterval = TimeSpan.FromMilliseconds(10),
                OnProgress = _ =>
                {
                    Interlocked.Increment(ref disposalCount);
                    return ValueTask.CompletedTask;
                }
            }
        };

        for (var i = 0; i < 3; i++)
        {
            var results = await source.SelectParallelAsync(
                static async (x, ct) =>
                {
                    await Task.Delay(2, ct);
                    return x;
                },
                options,
                cancellationToken: TestContext.Current.CancellationToken);

            results.Count.ShouldBe(5);
        }

        disposalCount.ShouldBeGreaterThan(0);
    }
}
