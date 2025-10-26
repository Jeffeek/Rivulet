using FluentAssertions;
using System.Collections.Concurrent;

namespace Rivulet.Core.Tests;

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
        var cts = new CancellationTokenSource();
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

        var task1 = source1.SelectParallelAsync(async (x, ct) => { await Task.Delay(10, ct); return x * 2; }, options1);
        var task2 = source2.SelectParallelAsync(async (x, ct) => { await Task.Delay(10, ct); return x * 3; }, options2);

        var results1 = await task1;
        var results2 = await task2;

        await Task.Delay(100);

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

        results.Should().BeEmpty();
        capturedSnapshot.Should().NotBeNull();
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
    public async Task EventCounters_AreExposedAndIncremented()
    {
        var source = Enumerable.Range(1, 50);

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4
        };

        var initialStarted = RivuletEventSource.Log.GetItemsStarted();
        var initialCompleted = RivuletEventSource.Log.GetItemsCompleted();

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(5, ct);
                return x * 2;
            },
            options);

        var finalStarted = RivuletEventSource.Log.GetItemsStarted();
        var finalCompleted = RivuletEventSource.Log.GetItemsCompleted();

        results.Should().HaveCount(50);
        (finalStarted - initialStarted).Should().Be(50);
        (finalCompleted - initialCompleted).Should().Be(50);
    }

    [Fact]
    public async Task EventCounters_TrackFailures()
    {
        var source = Enumerable.Range(1, 30);

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            ErrorMode = ErrorMode.BestEffort
        };

        var initialFailures = RivuletEventSource.Log.GetTotalFailures();

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(5, ct);
                if (x % 5 == 0)
                    throw new InvalidOperationException("Error");
                return x * 2;
            },
            options);

        var finalFailures = RivuletEventSource.Log.GetTotalFailures();

        results.Should().HaveCount(24); // 30 - 6 failures
        (finalFailures - initialFailures).Should().Be(6);
    }

    [Fact]
    public async Task EventCounters_TrackRetries()
    {
        var source = Enumerable.Range(1, 10);
        var attempts = new ConcurrentDictionary<int, int>();

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            MaxRetries = 2,
            IsTransient = ex => ex is InvalidOperationException
        };

        var initialRetries = RivuletEventSource.Log.GetTotalRetries();

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(5, ct);
                var attemptCount = attempts.AddOrUpdate(x, 1, (_, count) => count + 1);
                if (attemptCount < 2)
                    throw new InvalidOperationException("Transient");
                return x * 2;
            },
            options);

        var finalRetries = RivuletEventSource.Log.GetTotalRetries();

        results.Should().HaveCount(10);
        (finalRetries - initialRetries).Should().BeGreaterThan(0);
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
}
