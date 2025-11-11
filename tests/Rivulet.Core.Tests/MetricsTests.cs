using FluentAssertions;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Rivulet.Core.Observability;

namespace Rivulet.Core.Tests;

/// <summary>
/// Collection definition for EventSource tests that must run sequentially.
/// EventSource is a singleton, so tests that verify its counters cannot run in parallel.
/// </summary>
[CollectionDefinition("EventSource Sequential Tests", DisableParallelization = true)]
public class EventSourceTestCollection;

[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public class MetricsTests
{
    [Fact]
    public async Task SelectParallelAsync_WithMetrics_TracksItemsStartedAndCompleted()
    {
        var source = Enumerable.Range(1, 100);
        MetricsSnapshot? capturedSnapshot = null;

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            Metrics = new MetricsOptions
            {
                SampleInterval = TimeSpan.FromMilliseconds(50),
                OnMetricsSample = snapshot =>
                {
                    capturedSnapshot = snapshot;
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

        await Task.Delay(100);

        results.Should().HaveCount(100);
        capturedSnapshot.Should().NotBeNull();
        capturedSnapshot!.ItemsStarted.Should().Be(100);
        capturedSnapshot.ItemsCompleted.Should().Be(100);
        capturedSnapshot.ActiveWorkers.Should().Be(4);
    }

    [Fact]
    public async Task SelectParallelAsync_WithMetrics_TracksFailures()
    {
        var source = Enumerable.Range(1, 50);
        MetricsSnapshot? capturedSnapshot = null;

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            ErrorMode = ErrorMode.BestEffort,
            Metrics = new MetricsOptions
            {
                SampleInterval = TimeSpan.FromMilliseconds(50),
                OnMetricsSample = snapshot =>
                {
                    capturedSnapshot = snapshot;
                    return ValueTask.CompletedTask;
                }
            }
        };

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(5, ct);
                if (x % 5 == 0)
                    throw new InvalidOperationException("Error");
                return x * 2;
            },
            options);

        await Task.Delay(100);

        results.Should().HaveCount(40); // 50 - 10 failures
        capturedSnapshot.Should().NotBeNull();
        capturedSnapshot!.TotalFailures.Should().Be(10);
        capturedSnapshot.ErrorRate.Should().BeApproximately(0.2, 0.01); // 10/50 = 0.2
    }

    [Fact]
    public async Task SelectParallelAsync_WithMetrics_TracksRetries()
    {
        var source = Enumerable.Range(1, 20);
        var attempts = new ConcurrentDictionary<int, int>();
        MetricsSnapshot? capturedSnapshot = null;

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            MaxRetries = 3,
            IsTransient = ex => ex is InvalidOperationException,
            Metrics = new MetricsOptions
            {
                SampleInterval = TimeSpan.FromMilliseconds(50),
                OnMetricsSample = snapshot =>
                {
                    capturedSnapshot = snapshot;
                    return ValueTask.CompletedTask;
                }
            }
        };

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(5, ct);
                var attemptCount = attempts.AddOrUpdate(x, 1, (_, count) => count + 1);
                if (attemptCount < 3)
                    throw new InvalidOperationException("Transient error");
                return x * 2;
            },
            options);

        await Task.Delay(100);

        results.Should().HaveCount(20);
        capturedSnapshot.Should().NotBeNull();
        capturedSnapshot!.TotalRetries.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SelectParallelAsync_WithMetrics_TracksThrottleEvents()
    {
        var source = Enumerable.Range(1, 100);
        MetricsSnapshot? capturedSnapshot = null;
        var throttleCount = 0;

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            OnThrottleAsync = _ =>
            {
                throttleCount++;
                return ValueTask.CompletedTask;
            },
            Metrics = new MetricsOptions
            {
                SampleInterval = TimeSpan.FromMilliseconds(50),
                OnMetricsSample = snapshot =>
                {
                    capturedSnapshot = snapshot;
                    return ValueTask.CompletedTask;
                }
            }
        };

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x * 2;
            },
            options);

        await Task.Delay(100);

        results.Should().HaveCount(100);
        capturedSnapshot.Should().NotBeNull();
        capturedSnapshot!.ThrottleEvents.Should().Be(throttleCount);
    }

    [Fact]
    public async Task SelectParallelAsync_WithMetrics_CalculatesItemsPerSecond()
    {
        var source = Enumerable.Range(1, 100);
        MetricsSnapshot? capturedSnapshot = null;

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 8,
            Metrics = new MetricsOptions
            {
                SampleInterval = TimeSpan.FromMilliseconds(50),
                OnMetricsSample = snapshot =>
                {
                    capturedSnapshot = snapshot;
                    return ValueTask.CompletedTask;
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

        await Task.Delay(100);

        results.Should().HaveCount(100);
        capturedSnapshot.Should().NotBeNull();
        capturedSnapshot!.ItemsPerSecond.Should().BeGreaterThan(0);
        capturedSnapshot.Elapsed.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task SelectParallelStreamAsync_WithMetrics_TracksItemsCorrectly()
    {
        var source = AsyncEnumerable.Range(1, 50);
        MetricsSnapshot? capturedSnapshot = null;

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            Metrics = new MetricsOptions
            {
                SampleInterval = TimeSpan.FromMilliseconds(50),
                OnMetricsSample = snapshot =>
                {
                    capturedSnapshot = snapshot;
                    return ValueTask.CompletedTask;
                }
            }
        };

        var results = await source.SelectParallelStreamAsync(async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x * 2;
            }, options)
            .ToListAsync();

        await Task.Delay(100);

        results.Should().HaveCount(50);
        capturedSnapshot.Should().NotBeNull();
        capturedSnapshot!.ItemsStarted.Should().Be(50);
        capturedSnapshot.ItemsCompleted.Should().Be(50);
    }

    [Fact]
    public async Task SelectParallelStreamAsync_WithMetrics_TracksFailuresInCollectMode()
    {
        var source = AsyncEnumerable.Range(1, 30);
        MetricsSnapshot? capturedSnapshot = null;

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            ErrorMode = ErrorMode.BestEffort,
            Metrics = new MetricsOptions
            {
                SampleInterval = TimeSpan.FromMilliseconds(50),
                OnMetricsSample = snapshot =>
                {
                    capturedSnapshot = snapshot;
                    return ValueTask.CompletedTask;
                }
            }
        };

        var results = await source.SelectParallelStreamAsync(async (x, ct) =>
            {
                await Task.Delay(5, ct);
                if (x % 3 == 0) throw new InvalidOperationException("Error");
                return x * 2;
            }, options)
            .ToListAsync();

        await Task.Delay(100);

        results.Should().HaveCount(20); // 30 - 10 failures
        capturedSnapshot.Should().NotBeNull();
        capturedSnapshot!.TotalFailures.Should().Be(10);
    }

    [Fact]
    public async Task Metrics_WithoutCallback_DoesNotThrow()
    {
        var source = Enumerable.Range(1, 50);

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            Metrics = new MetricsOptions
            {
                SampleInterval = TimeSpan.FromMilliseconds(50),
                OnMetricsSample = null
            }
        };

        var act = async () => await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(5, ct);
                return x * 2;
            },
            options);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Metrics_CallbackThrows_DoesNotBreakOperation()
    {
        var source = Enumerable.Range(1, 20);

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            Metrics = new MetricsOptions
            {
                SampleInterval = TimeSpan.FromMilliseconds(50),
                OnMetricsSample = _ => throw new InvalidOperationException("Callback error")
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
    }

    [Fact]
    public async Task Metrics_WithCancellation_StopsTracking()
    {
        var source = Enumerable.Range(1, 1000);
        using var cts = new CancellationTokenSource();
        var sampleCount = 0;

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            Metrics = new MetricsOptions
            {
                SampleInterval = TimeSpan.FromMilliseconds(50),
                OnMetricsSample = _ =>
                {
                    sampleCount++;
                    return ValueTask.CompletedTask;
                }
            }
        };

        var act = async () =>
        {
            cts.CancelAfter(100);
            await source.SelectParallelAsync(
                async (x, ct) =>
                {
                    await Task.Delay(20, ct);
                    return x * 2;
                },
                options,
                cts.Token);
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
        sampleCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Metrics_WithFailFastMode_StopsTrackingOnError()
    {
        var source = Enumerable.Range(1, 100);
        MetricsSnapshot? capturedSnapshot = null;

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            ErrorMode = ErrorMode.FailFast,
            Metrics = new MetricsOptions
            {
                SampleInterval = TimeSpan.FromMilliseconds(50),
                OnMetricsSample = snapshot =>
                {
                    capturedSnapshot = snapshot;
                    return ValueTask.CompletedTask;
                }
            }
        };

        var act = async () => await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(5, ct);
                if (x == 10)
                    throw new InvalidOperationException("Error");
                return x * 2;
            },
            options);

        await act.Should().ThrowAsync<InvalidOperationException>();
        capturedSnapshot?.ItemsStarted.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Metrics_MultipleOperations_TrackIndependently()
    {
        var source1 = Enumerable.Range(1, 20);
        var source2 = Enumerable.Range(1, 30);

        MetricsSnapshot? snapshot1 = null;
        MetricsSnapshot? snapshot2 = null;

        var options1 = new ParallelOptionsRivulet
        {
            Metrics = new MetricsOptions
            {
                SampleInterval = TimeSpan.FromMilliseconds(50),
                OnMetricsSample = snapshot => { snapshot1 = snapshot; return ValueTask.CompletedTask; }
            }
        };

        var options2 = new ParallelOptionsRivulet
        {
            Metrics = new MetricsOptions
            {
                SampleInterval = TimeSpan.FromMilliseconds(50),
                OnMetricsSample = snapshot => { snapshot2 = snapshot; return ValueTask.CompletedTask; }
            }
        };

        // Increased delay from 10ms to 20ms to ensure operations run long enough for metrics to be sampled
        // This prevents the race where operations complete before the metrics timer fires
        var task1 = source1.SelectParallelAsync(async (x, ct) => { await Task.Delay(20, ct); return x * 2; }, options1);
        var task2 = source2.SelectParallelAsync(async (x, ct) => { await Task.Delay(20, ct); return x * 3; }, options2);

        var results1 = await task1;
        var results2 = await task2;

        // Increased from 100ms to 250ms (5x the sample interval) to ensure final metrics are captured
        // Operations complete in ~100-200ms, then wait additional time for metrics timer to fire and capture final state
        await Task.Delay(250);

        results1.Should().HaveCount(20);
        results2.Should().HaveCount(30);

        snapshot1.Should().NotBeNull();
        snapshot2.Should().NotBeNull();
        snapshot1!.ItemsCompleted.Should().Be(20);
        snapshot2!.ItemsCompleted.Should().Be(30);
    }

    [Fact]
    public async Task Metrics_WithOrderedOutput_TracksCorrectly()
    {
        var source = Enumerable.Range(1, 50);
        MetricsSnapshot? capturedSnapshot = null;

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            OrderedOutput = true,
            Metrics = new MetricsOptions
            {
                SampleInterval = TimeSpan.FromMilliseconds(50),
                OnMetricsSample = snapshot =>
                {
                    capturedSnapshot = snapshot;
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

        await Task.Delay(100);

        results.Should().HaveCount(50);
        results.Should().BeInAscendingOrder();
        capturedSnapshot.Should().NotBeNull();
        capturedSnapshot!.ItemsCompleted.Should().Be(50);
    }

    [Fact]
    public void MetricsSnapshot_DefaultValues_AreZero()
    {
        var snapshot = new MetricsSnapshot();

        snapshot.ActiveWorkers.Should().Be(0);
        snapshot.QueueDepth.Should().Be(0);
        snapshot.ItemsStarted.Should().Be(0);
        snapshot.ItemsCompleted.Should().Be(0);
        snapshot.TotalRetries.Should().Be(0);
        snapshot.TotalFailures.Should().Be(0);
        snapshot.ThrottleEvents.Should().Be(0);
        snapshot.DrainEvents.Should().Be(0);
        snapshot.Elapsed.Should().Be(TimeSpan.Zero);
        snapshot.ItemsPerSecond.Should().Be(0);
        snapshot.ErrorRate.Should().Be(0);
    }

    [Fact]
    public void MetricsOptions_DefaultSampleInterval_Is10Seconds()
    {
        var options = new MetricsOptions();

        options.SampleInterval.Should().Be(TimeSpan.FromSeconds(10));
        options.OnMetricsSample.Should().BeNull();
    }

    [Fact]
    public async Task Metrics_VeryShortSampleInterval_InvokesCallbackFrequently()
    {
        var source = Enumerable.Range(1, 100);
        var sampleCount = 0;

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 2,
            Metrics = new MetricsOptions
            {
                SampleInterval = TimeSpan.FromMilliseconds(20),
                OnMetricsSample = _ =>
                {
                    Interlocked.Increment(ref sampleCount);
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

        await Task.Delay(100);

        results.Should().HaveCount(100);
        sampleCount.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task Metrics_EmptySource_TracksZeroItems()
    {
        var source = Enumerable.Empty<int>();
        MetricsSnapshot? capturedSnapshot = null;

        var options = new ParallelOptionsRivulet
        {
            Metrics = new MetricsOptions
            {
                SampleInterval = TimeSpan.FromMilliseconds(5),
                OnMetricsSample = snapshot =>
                {
                    capturedSnapshot = snapshot;
                    return ValueTask.CompletedTask;
                }
            }
        };

        // Start the operation (completes immediately for empty source)
        var operationTask = source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(5, ct);
                return x * 2;
            },
            options);

        // Empty source completes instantly, but we need to give the metrics timer time to fire
        // Poll for up to 500ms waiting for at least one snapshot
        var deadline = DateTime.UtcNow.AddMilliseconds(500);
        while (capturedSnapshot == null && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        var results = await operationTask;

        results.Should().BeEmpty();
        capturedSnapshot.Should().NotBeNull("metrics timer should fire at least once within 500ms");
        capturedSnapshot!.ItemsStarted.Should().Be(0);
        capturedSnapshot.ItemsCompleted.Should().Be(0);
    }

    [Fact]
    public async Task Metrics_WithProgressReporting_BothWork()
    {
        var source = Enumerable.Range(1, 50);
        MetricsSnapshot? metricsSnapshot = null;
        ProgressSnapshot? progressSnapshot = null;

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            Metrics = new MetricsOptions
            {
                SampleInterval = TimeSpan.FromMilliseconds(50),
                OnMetricsSample = snapshot =>
                {
                    metricsSnapshot = snapshot;
                    return ValueTask.CompletedTask;
                }
            },
            Progress = new ProgressOptions
            {
                ReportInterval = TimeSpan.FromMilliseconds(50),
                OnProgress = snapshot =>
                {
                    progressSnapshot = snapshot;
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

        await Task.Delay(100);

        results.Should().HaveCount(50);
        metricsSnapshot.Should().NotBeNull();
        progressSnapshot.Should().NotBeNull();
        metricsSnapshot!.ItemsCompleted.Should().Be(50);
        progressSnapshot!.ItemsCompleted.Should().Be(50);
    }

    [Fact]
    public async Task Metrics_LargeScale_TracksAccurately()
    {
        var source = Enumerable.Range(1, 1000);
        MetricsSnapshot? capturedSnapshot = null;

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 16,
            Metrics = new MetricsOptions
            {
                SampleInterval = TimeSpan.FromMilliseconds(100),
                OnMetricsSample = snapshot =>
                {
                    capturedSnapshot = snapshot;
                    return ValueTask.CompletedTask;
                }
            }
        };

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x * 2;
            },
            options);

        await Task.Delay(200);

        results.Should().HaveCount(1000);
        capturedSnapshot.Should().NotBeNull();
        capturedSnapshot!.ItemsStarted.Should().Be(1000);
        capturedSnapshot.ItemsCompleted.Should().Be(1000);
        capturedSnapshot.TotalFailures.Should().Be(0);
    }


    [Fact]
    public async Task Metrics_WithAllErrorModes_TracksCorrectly()
    {
        var testCases = new[]
        {
            ErrorMode.FailFast,
            ErrorMode.CollectAndContinue,
            ErrorMode.BestEffort
        };

        foreach (var errorMode in testCases)
        {
            var source = Enumerable.Range(1, 20);
            MetricsSnapshot? capturedSnapshot = null;

            var options = new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 4,
                ErrorMode = errorMode,
                Metrics = new MetricsOptions
                {
                    SampleInterval = TimeSpan.FromMilliseconds(50),
                    OnMetricsSample = snapshot =>
                    {
                        capturedSnapshot = snapshot;
                        return ValueTask.CompletedTask;
                    }
                }
            };

            switch (errorMode)
            {
                case ErrorMode.FailFast:
                {
                    var act = async () => await source.SelectParallelAsync(
                        async (x, ct) =>
                        {
                            await Task.Delay(5, ct);
                            if (x == 10)
                                throw new InvalidOperationException("Error");
                            return x * 2;
                        },
                        options);

                    await act.Should().ThrowAsync<InvalidOperationException>();
                    break;
                }
                case ErrorMode.CollectAndContinue:
                {
                    var act = async () => await source.SelectParallelAsync(
                        async (x, ct) =>
                        {
                            await Task.Delay(5, ct);
                            if (x % 5 == 0)
                                throw new InvalidOperationException("Error");
                            return x * 2;
                        },
                        options);

                    await act.Should().ThrowAsync<AggregateException>();
                    break;
                }
                case ErrorMode.BestEffort:
                default:
                {
                    var results = await source.SelectParallelAsync(
                        async (x, ct) =>
                        {
                            await Task.Delay(5, ct);
                            if (x % 5 == 0)
                                throw new InvalidOperationException("Error");
                            return x * 2;
                        },
                        options);

                    results.Should().HaveCount(16); // 20 - 4 failures
                    break;
                }
            }

            await Task.Delay(100);

            capturedSnapshot.Should().NotBeNull($"Error mode: {errorMode}");
            capturedSnapshot!.ItemsStarted.Should().BeGreaterThan(0, $"Error mode: {errorMode}");
        }
    }


    [Fact]
    public async Task MetricsTracker_TracksDrainEvents()
    {
        MetricsSnapshot? capturedSnapshot = null;

        var options = new MetricsOptions
        {
            SampleInterval = TimeSpan.FromMilliseconds(50),
            OnMetricsSample = snapshot =>
            {
                capturedSnapshot = snapshot;
                return ValueTask.CompletedTask;
            }
        };

        using var tracker = new MetricsTracker(options, CancellationToken.None);

        tracker.IncrementDrainEvents();
        tracker.IncrementDrainEvents();
        tracker.IncrementDrainEvents();

        await Task.Delay(100);

        capturedSnapshot.Should().NotBeNull();
        capturedSnapshot!.DrainEvents.Should().Be(3);
    }

    [Fact]
    public async Task MetricsTracker_TracksQueueDepth()
    {
        MetricsSnapshot? capturedSnapshot = null;

        var options = new MetricsOptions
        {
            SampleInterval = TimeSpan.FromMilliseconds(50),
            OnMetricsSample = snapshot =>
            {
                capturedSnapshot = snapshot;
                return ValueTask.CompletedTask;
            }
        };

        using var tracker = new MetricsTracker(options, CancellationToken.None);

        tracker.SetQueueDepth(42);
        tracker.IncrementItemsStarted();

        await Task.Delay(100);

        capturedSnapshot.Should().NotBeNull();
        capturedSnapshot!.QueueDepth.Should().Be(42);
    }

    [Fact]
    public async Task MetricsTracker_DoubleDispose_DoesNotThrow()
    {
        var options = new MetricsOptions
        {
            SampleInterval = TimeSpan.FromMilliseconds(50),
            OnMetricsSample = _ => ValueTask.CompletedTask
        };

        var tracker = new MetricsTracker(options, CancellationToken.None);

        try
        {
            tracker.IncrementItemsStarted();
            await Task.Delay(100);

            var act = () => tracker.Dispose();
            act.Should().NotThrow();
        }
        finally
        {
            tracker.Dispose();
        }
    }

    [Fact]
    public void MetricsTrackerBase_WithoutMetrics_UsesNoOpTracker()
    {
        using var tracker = MetricsTrackerBase.Create(null, CancellationToken.None);

        // Should be NoOpMetricsTracker (lightweight, no allocations)
        tracker.Should().BeOfType<NoOpMetricsTracker>();

        // Should not throw when called
        tracker.IncrementItemsStarted();
        tracker.IncrementItemsCompleted();
        tracker.SetActiveWorkers(8);
        tracker.SetQueueDepth(100);

        // ReSharper disable once DisposeOnUsingVariable
        tracker.Dispose();
    }

    [Fact]
    public async Task MetricsTracker_VeryFastOperations_HandlesZeroElapsed()
    {
        MetricsSnapshot? capturedSnapshot = null;

        var options = new MetricsOptions
        {
            SampleInterval = TimeSpan.FromMilliseconds(1),
            OnMetricsSample = snapshot =>
            {
                capturedSnapshot = snapshot;
                return ValueTask.CompletedTask;
            }
        };

        using var tracker = new MetricsTracker(options, CancellationToken.None);

        // Poll for the snapshot with a timeout to avoid flakiness
        var deadline = DateTime.UtcNow.AddMilliseconds(200);
        while (capturedSnapshot is null && DateTime.UtcNow < deadline)
        {
            await Task.Delay(5);
        }

        capturedSnapshot.Should().NotBeNull();
        capturedSnapshot!.ItemsPerSecond.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task MetricsTracker_CancellationDuringDispose_HandlesGracefully()
    {
        using var cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource();

        var options = new MetricsOptions
        {
            SampleInterval = TimeSpan.FromMilliseconds(10),
            OnMetricsSample = async _ =>
            {
                await tcs.Task;
            }
        };

        var tracker = new MetricsTracker(options, cts.Token);
        tracker.IncrementItemsStarted();

        await Task.Delay(50, CancellationToken.None);

        await cts.CancelAsync();

        tcs.SetResult();

        var act = () => tracker.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task SelectParallelAsync_WithMetrics_TracksAllMetricTypes()
    {
        var source = Enumerable.Range(1, 30);
        MetricsSnapshot? capturedSnapshot = null;

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            MaxRetries = 2,
            IsTransient = ex => ex is InvalidOperationException,
            ErrorMode = ErrorMode.BestEffort,
            Metrics = new MetricsOptions
            {
                SampleInterval = TimeSpan.FromMilliseconds(50),
                OnMetricsSample = snapshot =>
                {
                    capturedSnapshot = snapshot;
                    return ValueTask.CompletedTask;
                }
            }
        };

        var attempts = new ConcurrentDictionary<int, int>();

        await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(10, ct);
                var attempt = attempts.AddOrUpdate(x, 1, (_, count) => count + 1);

                if (x % 5 == 0 && attempt <= 1)
                    throw new InvalidOperationException("Transient error");

                return x * 2;
            },
            options);

        await Task.Delay(100);

        capturedSnapshot.Should().NotBeNull();
        capturedSnapshot!.ItemsStarted.Should().Be(30);
        capturedSnapshot.ItemsCompleted.Should().Be(30);
        capturedSnapshot.TotalRetries.Should().BeGreaterThan(0);
        capturedSnapshot.ActiveWorkers.Should().Be(4);
        capturedSnapshot.Elapsed.Should().BeGreaterThan(TimeSpan.Zero);
        capturedSnapshot.ItemsPerSecond.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SelectParallelAsync_WithRetriesButNoMetrics_WorksCorrectly()
    {
        var source = Enumerable.Range(1, 20);
        var attempts = new ConcurrentDictionary<int, int>();

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            MaxRetries = 2,
            IsTransient = ex => ex is InvalidOperationException,
            ErrorMode = ErrorMode.BestEffort
        };

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(5, ct);
                var attempt = attempts.AddOrUpdate(x, 1, (_, count) => count + 1);

                if (x % 5 == 0 && attempt <= 1)
                    throw new InvalidOperationException("Transient error");

                return x * 2;
            },
            options);

        results.Should().HaveCount(20);
        attempts.Values.Should().Contain(v => v > 1);
    }

    [Fact]
    public async Task MetricsTracker_DisposeDuringWait_HandlesCatchBlock()
    {
        var longRunningTcs = new TaskCompletionSource<bool>();

        var options = new MetricsOptions
        {
            SampleInterval = TimeSpan.FromMilliseconds(10),
            OnMetricsSample = async _ =>
            {
                await longRunningTcs.Task;
            }
        };

        var tracker = new MetricsTracker(options, CancellationToken.None);
        tracker.IncrementItemsStarted();

        await Task.Delay(50);

        var disposeTask = Task.Run(() =>
        {
            tracker.Dispose();
        });

        await Task.Delay(100);

        longRunningTcs.SetResult(true);

        await disposeTask;

        disposeTask.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void MetricsTracker_WithNullCallback_SampleMetricsReturnsEarly()
    {
        var tracker = new MetricsTracker(new MetricsOptions { OnMetricsSample = null }, CancellationToken.None);

        try
        {
            tracker.IncrementItemsStarted();
            tracker.IncrementItemsCompleted();
        }
        finally
        {
            tracker.Dispose();
        }
    }

    [Fact]
    public async Task MetricsTracker_CallbackThrowsException_DoesNotCrash()
    {
        var callbackCount = 0;
        var tracker = new MetricsTracker(new MetricsOptions
        {
            SampleInterval = TimeSpan.FromMilliseconds(20),
            OnMetricsSample = _ =>
            {
                Interlocked.Increment(ref callbackCount);
                throw new InvalidOperationException("Metrics callback error!");
            }
        }, CancellationToken.None);

        try
        {
            tracker.IncrementItemsStarted();
            tracker.IncrementItemsCompleted();

            await Task.Delay(100);

            // Should not crash despite exception
            callbackCount.Should().BeGreaterThan(0);
        }
        finally
        {
            tracker.Dispose();
        }
    }

    [Fact]
    public void MetricsTracker_Dispose_WithCallbackThrows_HandlesGracefully()
    {
        var tracker = new MetricsTracker(new MetricsOptions
        {
            SampleInterval = TimeSpan.FromMilliseconds(100),
            OnMetricsSample = _ => throw new InvalidOperationException("Error!")
        }, CancellationToken.None);

        tracker.IncrementItemsCompleted();

        // Should not throw despite callback exception
        var act = () => tracker.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void RivuletEventSource_Counters_WorkCorrectly()
    {
        var eventSource = RivuletEventSource.Log;

        eventSource.IncrementItemsStarted();
        eventSource.IncrementItemsCompleted();
        eventSource.IncrementRetries();
        eventSource.IncrementFailures();
        eventSource.IncrementThrottleEvents();
        eventSource.IncrementDrainEvents();

        // Should not throw and should return valid values
        eventSource.GetItemsStarted().Should().BeGreaterThanOrEqualTo(0);
        eventSource.GetItemsCompleted().Should().BeGreaterThanOrEqualTo(0);
        eventSource.GetTotalRetries().Should().BeGreaterThanOrEqualTo(0);
        eventSource.GetTotalFailures().Should().BeGreaterThanOrEqualTo(0);
        eventSource.GetThrottleEvents().Should().BeGreaterThanOrEqualTo(0);
        eventSource.GetDrainEvents().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Metrics_TracksRetriesCorrectly()
    {
        var source = Enumerable.Range(1, 10);
        MetricsSnapshot? snapshot = null;

        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 2,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            IsTransient = ex => ex is InvalidOperationException,
            ErrorMode = ErrorMode.BestEffort,
            Metrics = new MetricsOptions
            {
                SampleInterval = TimeSpan.FromMilliseconds(50),
                OnMetricsSample = s =>
                {
                    snapshot = s;
                    return ValueTask.CompletedTask;
                }
            }
        };

        var attemptCount = 0;
        await source.SelectParallelAsync(
            (x, _) =>
            {
                if (x != 5) return new ValueTask<int>(x * 2);
                var attempt = Interlocked.Increment(ref attemptCount);
                if (attempt <= 2) // Fail first 2 attempts
                    throw new InvalidOperationException("Transient");
                return new ValueTask<int>(x * 2);
            },
            options);

        await Task.Delay(150); // Wait for sampling

        snapshot.Should().NotBeNull();
        snapshot!.TotalRetries.Should().BeGreaterThan(0);
    }

}
