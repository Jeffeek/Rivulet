using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Rivulet.Base.Tests;
using Rivulet.Core.Observability;

namespace Rivulet.Core.Tests;

[
    Collection(TestCollections.EventSourceSequential),
    SuppressMessage("ReSharper", "AccessToDisposedClosure")
]
public sealed class MetricsTests
{
    [Fact]
    public async Task SelectParallelAsync_WithMetrics_TracksItemsStartedAndCompleted()
    {
        var source = Enumerable.Range(1, 100);
        MetricsSnapshot? capturedSnapshot = null;

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            Metrics = new()
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
            static async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x * 2;
            },
            options);

        await Task.Delay(100, CancellationToken.None);

        results.Count.ShouldBe(100);
        capturedSnapshot.ShouldNotBeNull();
        capturedSnapshot!.ItemsStarted.ShouldBe(100);
        capturedSnapshot.ItemsCompleted.ShouldBe(100);
        capturedSnapshot.ActiveWorkers.ShouldBe(4);
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
            Metrics = new()
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
            static async (x, ct) =>
            {
                await Task.Delay(5, ct);
                if (x % 5 == 0) throw new InvalidOperationException("Error");

                return x * 2;
            },
            options);

        // Disposal completes inside SelectParallelAsync before it returns, which triggers the final sample.
        // The final sample has a 1000ms delay built-in (see MetricsTracker.cs disposal handler).
        // We add minimal extra time to ensure the callback has fully completed:
        // 1. Callback execution time
        // 2. Any remaining async state machine cleanup
        // Using Task.Yield() to force a context switch, ensuring all memory writes are globally visible
        await Task.Yield();
        await Task.Delay(500, CancellationToken.None);

        results.Count.ShouldBe(40); // 50 - 10 failures
        capturedSnapshot.ShouldNotBeNull();
        capturedSnapshot!.TotalFailures.ShouldBe(10);
        capturedSnapshot.ErrorRate.ShouldBe(0.2, 0.01); // 10/50 = 0.2
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
            IsTransient = static ex => ex is InvalidOperationException,
            Metrics = new()
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
                var attemptCount = attempts.AddOrUpdate(x, 1, static (_, count) => count + 1);
                if (attemptCount < 3) throw new InvalidOperationException("Transient error");

                return x * 2;
            },
            options);

        await Task.Delay(100, CancellationToken.None);

        results.Count.ShouldBe(20);
        capturedSnapshot.ShouldNotBeNull();
        capturedSnapshot!.TotalRetries.ShouldBeGreaterThan(0);
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
            Metrics = new()
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
            static async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x * 2;
            },
            options);

        await Task.Delay(100, CancellationToken.None);

        results.Count.ShouldBe(100);
        capturedSnapshot.ShouldNotBeNull();
        capturedSnapshot!.ThrottleEvents.ShouldBe(throttleCount);
    }

    [Fact]
    public async Task SelectParallelAsync_WithMetrics_CalculatesItemsPerSecond()
    {
        var source = Enumerable.Range(1, 100);
        MetricsSnapshot? capturedSnapshot = null;

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 8,
            Metrics = new()
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
            static async (x, ct) =>
            {
                await Task.Delay(5, ct);
                return x * 2;
            },
            options);

        await Task.Delay(100, CancellationToken.None);

        results.Count.ShouldBe(100);
        capturedSnapshot.ShouldNotBeNull();
        capturedSnapshot!.ItemsPerSecond.ShouldBeGreaterThan(0);
        capturedSnapshot.Elapsed.ShouldBeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task SelectParallelStreamAsync_WithMetrics_TracksItemsCorrectly()
    {
        var source = AsyncEnumerable.Range(1, 50);
        MetricsSnapshot? capturedSnapshot = null;

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            Metrics = new()
            {
                SampleInterval = TimeSpan.FromMilliseconds(50),
                OnMetricsSample = snapshot =>
                {
                    capturedSnapshot = snapshot;
                    return ValueTask.CompletedTask;
                }
            }
        };

        var results = await source.SelectParallelStreamAsync(static async (x, ct) =>
                {
                    await Task.Delay(10, ct);
                    return x * 2;
                },
                options)
            .ToListAsync();

        await Task.Delay(100, CancellationToken.None);

        results.Count.ShouldBe(50);
        capturedSnapshot.ShouldNotBeNull();
        capturedSnapshot!.ItemsStarted.ShouldBe(50);
        capturedSnapshot.ItemsCompleted.ShouldBe(50);
    }

    [Fact]
    public async Task SelectParallelStreamAsync_WithMetrics_TracksFailuresInCollectMode()
    {
        var source = AsyncEnumerable.Range(1, 30);
        MetricsSnapshot? capturedSnapshot = null;
        var callbackInvocationCount = 0;

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            ErrorMode = ErrorMode.BestEffort,
            Metrics = new()
            {
                SampleInterval = TimeSpan.FromMilliseconds(50),
                OnMetricsSample = snapshot =>
                {
                    Interlocked.Increment(ref callbackInvocationCount);
                    capturedSnapshot = snapshot;
                    return ValueTask.CompletedTask;
                }
            }
        };

        var results = await source.SelectParallelStreamAsync(static async (x, ct) =>
                {
                    await Task.Delay(5, ct);
                    if (x % 3 == 0) throw new InvalidOperationException("Error");

                    return x * 2;
                },
                options)
            .ToListAsync();

        results.Count.ShouldBe(20,
            "30 items minus 10 failures (every 3rd: 3,6,9,12,15,18,21,24,27,30) equals 20 successful results");

        await Task.Yield();

        await DeadlineExtensions.ApplyDeadlineAsync(
            DateTime.UtcNow.AddMilliseconds(4000),
            static () => Task.Delay(100),
            () => callbackInvocationCount == 0 || capturedSnapshot is not { TotalFailures: 10 });

        callbackInvocationCount.ShouldBeGreaterThan(0,
            "at least one metrics sample should have been captured during operation execution and disposal");
        capturedSnapshot.ShouldNotBeNull("final metrics snapshot should be available after disposal completes");
        capturedSnapshot!.TotalFailures.ShouldBe(10,
            "items 3,6,9,12,15,18,21,24,27,30 should have failed (10 total failures)");
    }

    [Fact]
    public Task Metrics_WithoutCallback_DoesNotThrow()
    {
        var source = Enumerable.Range(1, 50);

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            Metrics = new() { SampleInterval = TimeSpan.FromMilliseconds(50), OnMetricsSample = null }
        };

        var act = () => source.SelectParallelAsync(
            static async (x, ct) =>
            {
                await Task.Delay(5, ct);
                return x * 2;
            },
            options);

        return act.ShouldNotThrowAsync();
    }

    [Fact]
    public async Task Metrics_CallbackThrows_DoesNotBreakOperation()
    {
        var source = Enumerable.Range(1, 20);

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            Metrics = new()
            {
                SampleInterval = TimeSpan.FromMilliseconds(50),
                OnMetricsSample = static _ => throw new InvalidOperationException("Callback error")
            }
        };

        var results = await source.SelectParallelAsync(
            static async (x, ct) =>
            {
                await Task.Delay(5, ct);
                return x * 2;
            },
            options);

        results.Count.ShouldBe(20);
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
            Metrics = new()
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
                static async (x, ct) =>
                {
                    await Task.Delay(20, ct);
                    return x * 2;
                },
                options,
                cts.Token);
        };

        await act.ShouldThrowAsync<OperationCanceledException>();
        sampleCount.ShouldBeGreaterThan(0);
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
            Metrics = new()
            {
                SampleInterval = TimeSpan.FromMilliseconds(50),
                OnMetricsSample = snapshot =>
                {
                    capturedSnapshot = snapshot;
                    return ValueTask.CompletedTask;
                }
            }
        };

        var act = () => source.SelectParallelAsync(
            static async (x, ct) =>
            {
                await Task.Delay(5, ct);
                if (x == 10) throw new InvalidOperationException("Error");

                return x * 2;
            },
            options);

        await act.ShouldThrowAsync<InvalidOperationException>();
        capturedSnapshot?.ItemsStarted.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Metrics_MultipleOperations_TrackIndependently()
    {
        var source1 = Enumerable.Range(1, 20);
        var source2 = Enumerable.Range(1, 30);

        var snapshots1 = new ConcurrentBag<MetricsSnapshot>();
        var snapshots2 = new ConcurrentBag<MetricsSnapshot>();

        var options1 = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            Metrics = new()
            {
                SampleInterval = TimeSpan.FromMilliseconds(10),
                OnMetricsSample = snapshot =>
                {
                    snapshots1.Add(snapshot);
                    return ValueTask.CompletedTask;
                }
            }
        };

        var options2 = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            Metrics = new()
            {
                SampleInterval = TimeSpan.FromMilliseconds(10),
                OnMetricsSample = snapshot =>
                {
                    snapshots2.Add(snapshot);
                    return ValueTask.CompletedTask;
                }
            }
        };

        // Reduced sample interval to 10ms (from 50ms) to get many more timer firings
        // Operation 1: 20 items / 4 parallelism * 50ms = 250ms (25 sample intervals)
        // Operation 2: 30 items / 4 parallelism * 50ms = 375ms (37.5 sample intervals)
        // This ensures metrics timers fire frequently enough to reliably capture final state
        // even if last item completes between timer ticks
        var task1 = source1.SelectParallelAsync(static async (x, ct) =>
            {
                await Task.Delay(50, ct);
                return x * 2;
            },
            options1);
        var task2 = source2.SelectParallelAsync(static async (x, ct) =>
            {
                await Task.Delay(50, ct);
                return x * 3;
            },
            options2);

        var results1 = await task1;
        var results2 = await task2;

        results1.Count.ShouldBe(20, "first operation should complete all 20 items");
        results2.Count.ShouldBe(30, "second operation should complete all 30 items");

        await Task.Yield();

        await DeadlineExtensions.ApplyDeadlineAsync(
            DateTime.UtcNow.AddMilliseconds(4000),
            static () => Task.Delay(100),
            () =>
            {
                if (!snapshots1.Any() || !snapshots2.Any()) return true;

                var list1 = snapshots1.ToList();
                var list2 = snapshots2.ToList();

                var max1 = list1.Max(static s => s.ItemsCompleted);
                var max2 = list2.Max(static s => s.ItemsCompleted);
                return max1 != 20 || max2 != 30;
            });

        await Task.Delay(200, CancellationToken.None);

        snapshots1.ShouldNotBeEmpty("first operation should have captured at least one metrics sample");
        snapshots2.ShouldNotBeEmpty("second operation should have captured at least one metrics sample");

        var finalSnapshots1 = snapshots1.ToList();
        var finalSnapshots2 = snapshots2.ToList();

        var maxCompleted1 = finalSnapshots1.Max(static s => s.ItemsCompleted);
        var maxCompleted2 = finalSnapshots2.Max(static s => s.ItemsCompleted);

        maxCompleted1.ShouldBe(20, "first operation's final metrics snapshot should show all 20 items completed");
        maxCompleted2.ShouldBe(30, "second operation's final metrics snapshot should show all 30 items completed");
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
            Metrics = new()
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
            static async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x * 2;
            },
            options);

        await Task.Delay(100, CancellationToken.None);

        results.Count.ShouldBe(50);
        results.ShouldBeInOrder();
        capturedSnapshot.ShouldNotBeNull();
        capturedSnapshot!.ItemsCompleted.ShouldBe(50);
    }

    [Fact]
    public void MetricsSnapshot_DefaultValues_AreZero()
    {
        var snapshot = new MetricsSnapshot();

        snapshot.ActiveWorkers.ShouldBe(0);
        snapshot.QueueDepth.ShouldBe(0);
        snapshot.ItemsStarted.ShouldBe(0);
        snapshot.ItemsCompleted.ShouldBe(0);
        snapshot.TotalRetries.ShouldBe(0);
        snapshot.TotalFailures.ShouldBe(0);
        snapshot.ThrottleEvents.ShouldBe(0);
        snapshot.DrainEvents.ShouldBe(0);
        snapshot.Elapsed.ShouldBe(TimeSpan.Zero);
        snapshot.ItemsPerSecond.ShouldBe(0);
        snapshot.ErrorRate.ShouldBe(0);
    }

    [Fact]
    public void MetricsOptions_DefaultSampleInterval_Is10Seconds()
    {
        var options = new MetricsOptions();

        options.SampleInterval.ShouldBe(TimeSpan.FromSeconds(10));
        options.OnMetricsSample.ShouldBeNull();
    }

    [Fact]
    public async Task Metrics_VeryShortSampleInterval_InvokesCallbackFrequently()
    {
        var source = Enumerable.Range(1, 100);
        var sampleCount = 0;

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 2,
            Metrics = new()
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
            static async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x * 2;
            },
            options);

        await Task.Delay(100, CancellationToken.None);

        results.Count.ShouldBe(100);
        sampleCount.ShouldBeGreaterThan(1);
    }

    [Fact]
    public async Task Metrics_MinimalSource_TracksCorrectly()
    {
        // Use a minimal source (1 item) with operation delay to keep MetricsTracker alive
        // long enough for timer to fire. Testing with truly empty source causes immediate
        // disposal before timer can fire, making the test inherently flaky.
        // This test verifies metrics work correctly with very small workloads.
        var source = Enumerable.Range(1, 1);
        MetricsSnapshot? capturedSnapshot = null;

        var options = new ParallelOptionsRivulet
        {
            Metrics = new()
            {
                SampleInterval = TimeSpan.FromMilliseconds(10),
                OnMetricsSample = snapshot =>
                {
                    capturedSnapshot = snapshot;
                    return ValueTask.CompletedTask;
                }
            }
        };

        // Operation with sufficient delay (100ms) to ensure timer fires multiple times
        // This keeps the MetricsTracker alive and prevents race conditions
        var results = await source.SelectParallelAsync(
            static async (x, ct) =>
            {
                await Task.Delay(100, ct);
                return x * 2;
            },
            options);

        // Wait for final timer ticks after operation completes
        // Sample interval is 10ms, wait 50ms (5x interval) for final state + buffer
        await Task.Delay(50 + 500);

        results.Count.ShouldBe(1);
        capturedSnapshot.ShouldNotBeNull("metrics timer should fire during 100ms operation");
        capturedSnapshot!.ItemsStarted.ShouldBe(1);
        capturedSnapshot.ItemsCompleted.ShouldBe(1);
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
            Metrics = new()
            {
                SampleInterval = TimeSpan.FromMilliseconds(50),
                OnMetricsSample = snapshot =>
                {
                    metricsSnapshot = snapshot;
                    return ValueTask.CompletedTask;
                }
            },
            Progress = new()
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
            static async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x * 2;
            },
            options);

        // Wait for timers to fire multiple times after completion
        // Both sample intervals are 50ms, so wait 200ms (4x interval) to ensure
        // final state is fully captured in both metrics and progress snapshots
        await Task.Delay(200, CancellationToken.None);

        results.Count.ShouldBe(50);
        metricsSnapshot.ShouldNotBeNull();
        progressSnapshot.ShouldNotBeNull();
        metricsSnapshot!.ItemsCompleted.ShouldBe(50);
        progressSnapshot!.ItemsCompleted.ShouldBe(50);
    }

    [Fact]
    public async Task Metrics_LargeScale_TracksAccurately()
    {
        var source = Enumerable.Range(1, 1000);
        MetricsSnapshot? capturedSnapshot = null;

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 16,
            Metrics = new()
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
            static async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x * 2;
            },
            options);

        await Task.Yield();

        await DeadlineExtensions.ApplyDeadlineAsync(
            DateTime.UtcNow.AddMilliseconds(4000),
            static () => Task.Delay(100),
            () => capturedSnapshot is not { ItemsStarted: 1000 } || capturedSnapshot.ItemsCompleted != 1000);

        results.Count.ShouldBe(1000, "all 1000 items should complete successfully");
        capturedSnapshot.ShouldNotBeNull("final metrics snapshot should be captured after disposal");
        capturedSnapshot!.ItemsStarted.ShouldBe(1000, "metrics should show all 1000 items were started");
        capturedSnapshot.ItemsCompleted.ShouldBe(1000, "metrics should show all 1000 items completed successfully");
        capturedSnapshot.TotalFailures.ShouldBe(0, "no items should have failed");
    }


    [Fact]
    public async Task Metrics_WithAllErrorModes_TracksCorrectly()
    {
        var testCases = new[] { ErrorMode.FailFast, ErrorMode.CollectAndContinue, ErrorMode.BestEffort };

        foreach (var errorMode in testCases)
        {
            var source = Enumerable.Range(1, 20);
            MetricsSnapshot? capturedSnapshot = null;

            var options = new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 4,
                ErrorMode = errorMode,
                Metrics = new()
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
                    var act = () => source.SelectParallelAsync(
                        static async (x, ct) =>
                        {
                            await Task.Delay(5, ct);
                            if (x == 10) throw new InvalidOperationException("Error");

                            return x * 2;
                        },
                        options);

                    await act.ShouldThrowAsync<InvalidOperationException>();
                    break;
                }
                case ErrorMode.CollectAndContinue:
                {
                    var act = () => source.SelectParallelAsync(
                        static async (x, ct) =>
                        {
                            await Task.Delay(5, ct);
                            if (x % 5 == 0) throw new InvalidOperationException("Error");

                            return x * 2;
                        },
                        options);

                    await act.ShouldThrowAsync<AggregateException>();
                    break;
                }
                case ErrorMode.BestEffort:
                default:
                {
                    var results = await source.SelectParallelAsync(
                        static async (x, ct) =>
                        {
                            await Task.Delay(5, ct);
                            if (x % 5 == 0) throw new InvalidOperationException("Error");

                            return x * 2;
                        },
                        options);

                    results.Count.ShouldBe(16); // 20 - 4 failures
                    break;
                }
            }

            await Task.Delay(100, CancellationToken.None);

            capturedSnapshot.ShouldNotBeNull($"Error mode: {errorMode}");
            capturedSnapshot!.ItemsStarted.ShouldBeGreaterThan(0, $"Error mode: {errorMode}");
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

        await using var tracker = new MetricsTracker(options, CancellationToken.None);

        tracker.IncrementDrainEvents();
        tracker.IncrementDrainEvents();
        tracker.IncrementDrainEvents();

        // Wait for metrics sample to capture the drain events
        // SampleInterval is 50ms, so we wait for multiple intervals plus buffer
        // Race condition fixed by 50ms delay before final sample in MetricsTracker
        await Task.Delay(150, CancellationToken.None);

        capturedSnapshot.ShouldNotBeNull();
        capturedSnapshot!.DrainEvents.ShouldBe(3);
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

        await using var tracker = new MetricsTracker(options, CancellationToken.None);

        tracker.SetQueueDepth(42);
        tracker.IncrementItemsStarted();

        // Wait for metrics timer to fire - sample interval is 50ms
        // Using 200ms (4x interval) for reliability in CI/CD
        await Task.Delay(200, CancellationToken.None);

        capturedSnapshot.ShouldNotBeNull();
        capturedSnapshot!.QueueDepth.ShouldBe(42);
    }

    [Fact]
    public async Task MetricsTracker_DoubleDispose_DoesNotThrow()
    {
        var options = new MetricsOptions
            { SampleInterval = TimeSpan.FromMilliseconds(50), OnMetricsSample = static _ => ValueTask.CompletedTask };

        var tracker = new MetricsTracker(options, CancellationToken.None);

        try
        {
            tracker.IncrementItemsStarted();
            await Task.Delay(100, CancellationToken.None);

            var act = async () => await tracker.DisposeAsync();
            await act.ShouldNotThrowAsync();
        }
        finally
        {
            await tracker.DisposeAsync();
        }
    }

    [Fact]
    public async Task MetricsTrackerBase_WithoutMetrics_UsesNoOpTracker()
    {
        await using var tracker = MetricsTrackerBase.Create(null, CancellationToken.None);

        // Should be NoOpMetricsTracker (lightweight, no allocations)
        tracker.ShouldBeOfType<NoOpMetricsTracker>();

        // Should not throw when called
        tracker.IncrementItemsStarted();
        tracker.IncrementItemsCompleted();
        tracker.SetActiveWorkers(8);
        tracker.SetQueueDepth(100);

        // ReSharper disable once DisposeOnUsingVariable
        await tracker.DisposeAsync();
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

        await using var tracker = new MetricsTracker(options, CancellationToken.None);

        // Poll for the snapshot with a timeout to avoid flakiness
        await DeadlineExtensions.ApplyDeadlineAsync(
            DateTime.UtcNow.AddMilliseconds(200),
            static () => Task.Delay(5),
            () => capturedSnapshot is null);

        capturedSnapshot.ShouldNotBeNull();
        capturedSnapshot!.ItemsPerSecond.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task MetricsTracker_CancellationDuringDispose_HandlesGracefully()
    {
        using var cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource();

        var options = new MetricsOptions
        {
            SampleInterval = TimeSpan.FromMilliseconds(10),
            OnMetricsSample = async _ => { await tcs.Task; }
        };

        var tracker = new MetricsTracker(options, cts.Token);
        tracker.IncrementItemsStarted();

        await Task.Delay(50, CancellationToken.None);

        await cts.CancelAsync();

        tcs.SetResult();

        var act = async () => await tracker.DisposeAsync();
        await act.ShouldNotThrowAsync();
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
            IsTransient = static ex => ex is InvalidOperationException,
            ErrorMode = ErrorMode.BestEffort,
            Metrics = new()
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
                var attempt = attempts.AddOrUpdate(x, 1, static (_, count) => count + 1);

                if (x % 5 == 0 && attempt <= 1) throw new InvalidOperationException("Transient error");

                return x * 2;
            },
            options);

        await Task.Delay(100, CancellationToken.None);

        capturedSnapshot.ShouldNotBeNull();
        capturedSnapshot!.ItemsStarted.ShouldBe(30);
        capturedSnapshot.ItemsCompleted.ShouldBe(30);
        capturedSnapshot.TotalRetries.ShouldBeGreaterThan(0);
        capturedSnapshot.ActiveWorkers.ShouldBe(4);
        capturedSnapshot.Elapsed.ShouldBeGreaterThan(TimeSpan.Zero);
        capturedSnapshot.ItemsPerSecond.ShouldBeGreaterThan(0);
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
            IsTransient = static ex => ex is InvalidOperationException,
            ErrorMode = ErrorMode.BestEffort
        };

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(5, ct);
                var attempt = attempts.AddOrUpdate(x, 1, static (_, count) => count + 1);

                if (x % 5 == 0 && attempt <= 1) throw new InvalidOperationException("Transient error");

                return x * 2;
            },
            options);

        results.Count.ShouldBe(20);
        attempts.Values.ShouldContain(static v => v > 1);
    }

    [Fact]
    public async Task MetricsTracker_DisposeDuringWait_HandlesCatchBlock()
    {
        var longRunningTcs = new TaskCompletionSource<bool>();

        var options = new MetricsOptions
        {
            SampleInterval = TimeSpan.FromMilliseconds(10),
            OnMetricsSample = async _ => { await longRunningTcs.Task; }
        };

        var tracker = new MetricsTracker(options, CancellationToken.None);
        tracker.IncrementItemsStarted();

        await Task.Delay(50, CancellationToken.None);

        var disposeTask = Task.Run(async () => { await tracker.DisposeAsync(); });

        await Task.Delay(100, CancellationToken.None);

        longRunningTcs.SetResult(true);

        await disposeTask;

        disposeTask.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public async Task MetricsTracker_WithNullCallback_SampleMetricsReturnsEarly()
    {
        var tracker = new MetricsTracker(new() { OnMetricsSample = null }, CancellationToken.None);

        try
        {
            tracker.IncrementItemsStarted();
            tracker.IncrementItemsCompleted();
        }
        finally
        {
            await tracker.DisposeAsync();
        }
    }

    [Fact]
    public async Task MetricsTracker_CallbackThrowsException_DoesNotCrash()
    {
        var callbackCount = 0;
        var tracker = new MetricsTracker(new()
            {
                SampleInterval = TimeSpan.FromMilliseconds(20),
                OnMetricsSample = _ =>
                {
                    Interlocked.Increment(ref callbackCount);
                    throw new InvalidOperationException("Metrics callback error!");
                }
            },
            CancellationToken.None);

        try
        {
            tracker.IncrementItemsStarted();
            tracker.IncrementItemsCompleted();

            // Wait for metrics timer to fire - sample interval is 20ms
            // Using 500ms (25x interval) for reliability in CI/CD environments
            // Timer needs time to initialize and fire at least once
            await Task.Delay(500, CancellationToken.None);

            // Should not crash despite exception
            callbackCount.ShouldBeGreaterThan(0,
                "callback should have been invoked at least once after waiting 25x the sample interval");
        }
        finally
        {
            await tracker.DisposeAsync();
        }
    }

    [Fact]
    public Task MetricsTracker_Dispose_WithCallbackThrows_HandlesGracefully()
    {
        var tracker = new MetricsTracker(new()
            {
                SampleInterval = TimeSpan.FromMilliseconds(100),
                OnMetricsSample = static _ => throw new InvalidOperationException("Error!")
            },
            CancellationToken.None);

        tracker.IncrementItemsCompleted();

        // Should not throw despite callback exception
        var act = async () => await tracker.DisposeAsync();
        return act.ShouldNotThrowAsync();
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
        eventSource.GetItemsStarted().ShouldBeGreaterThanOrEqualTo(0);
        eventSource.GetItemsCompleted().ShouldBeGreaterThanOrEqualTo(0);
        eventSource.GetTotalRetries().ShouldBeGreaterThanOrEqualTo(0);
        eventSource.GetTotalFailures().ShouldBeGreaterThanOrEqualTo(0);
        eventSource.GetThrottleEvents().ShouldBeGreaterThanOrEqualTo(0);
        eventSource.GetDrainEvents().ShouldBeGreaterThanOrEqualTo(0);
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
            IsTransient = static ex => ex is InvalidOperationException,
            ErrorMode = ErrorMode.BestEffort,
            Metrics = new()
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
                if (x != 5) return new(x * 2);

                var attempt = Interlocked.Increment(ref attemptCount);
                return attempt <= 2 // Fail first 2 attempts
                    ? throw new InvalidOperationException("Transient")
                    : new ValueTask<int>(x * 2);
            },
            options);

        await Task.Delay(150, CancellationToken.None); // Wait for sampling

        snapshot.ShouldNotBeNull();
        snapshot!.TotalRetries.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task NoOpMetricsTracker_AllMethods_DoNotThrow()
    {
        // Test that all NoOpMetricsTracker methods can be called without throwing
        await using var tracker = MetricsTrackerBase.Create(null, CancellationToken.None);

        tracker.ShouldBeOfType<NoOpMetricsTracker>();

        // Call all increment methods
        tracker.IncrementItemsStarted();
        tracker.IncrementItemsCompleted();
        tracker.IncrementRetries();
        tracker.IncrementFailures();
        tracker.IncrementThrottleEvents();
        tracker.IncrementDrainEvents();

        // Call all set methods
        tracker.SetActiveWorkers(10);
        tracker.SetQueueDepth(50);

        // Call dispose - using statement handles this but also call explicitly
        // ReSharper disable once DisposeOnUsingVariable
        await tracker.DisposeAsync();
    }
}