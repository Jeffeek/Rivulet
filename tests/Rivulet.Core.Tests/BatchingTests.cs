using FluentAssertions;
using System.Collections.Concurrent;

namespace Rivulet.Core.Tests;

public class BatchingTests
{
    [Fact]
    public async Task BatchParallelAsync_ExactBatches_ProcessesCorrectly()
    {
        var source = Enumerable.Range(1, 100);
        var batchSizes = new ConcurrentBag<int>();

        var results = await source.BatchParallelAsync(
            batchSize: 10,
            async (batch, ct) =>
            {
                batchSizes.Add(batch.Count);
                await Task.Delay(5, ct);
                return batch.Sum();
            });

        results.Should().HaveCount(10);
        batchSizes.Should().AllSatisfy(size => size.Should().Be(10));
        results.Sum().Should().Be(5050);
    }

    [Fact]
    public async Task BatchParallelAsync_PartialFinalBatch_IncludesRemainder()
    {
        var source = Enumerable.Range(1, 25);
        var batchSizes = new ConcurrentBag<int>();

        var results = await source.BatchParallelAsync(
            batchSize: 10,
            async (batch, ct) =>
            {
                batchSizes.Add(batch.Count);
                await Task.Delay(5, ct);
                return batch.Sum();
            });

        results.Should().HaveCount(3);
        batchSizes.Should().Contain(10);
        batchSizes.Should().Contain(5);
        results.Sum().Should().Be(325);
    }

    [Fact]
    public async Task BatchParallelAsync_EmptySource_ReturnsEmptyList()
    {
        var source = Enumerable.Empty<int>();

        var results = await source.BatchParallelAsync(
            batchSize: 10,
            async (batch, _) =>
            {
                await Task.CompletedTask;
                return batch.Sum();
            });

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task BatchParallelAsync_SingleItem_CreatesOneBatch()
    {
        var source = new[] { 42 };

        var results = await source.BatchParallelAsync(
            batchSize: 10,
            async (batch, _) =>
            {
                await Task.CompletedTask;
                return batch.First();
            });

        results.Should().HaveCount(1);
        results[0].Should().Be(42);
    }

    [Fact]
    public async Task BatchParallelAsync_InvalidBatchSize_ThrowsArgumentException()
    {
        var source = Enumerable.Range(1, 10);

        var act = async () => await source.BatchParallelAsync(
            batchSize: 0,
            async (batch, _) =>
            {
                await Task.CompletedTask;
                return batch.Sum();
            });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Batch size must be at least 1.*");
    }

    [Fact]
    public async Task BatchParallelAsync_NegativeBatchSize_ThrowsArgumentException()
    {
        var source = Enumerable.Range(1, 10);

        var act = async () => await source.BatchParallelAsync(
            batchSize: -5,
            async (batch, _) =>
            {
                await Task.CompletedTask;
                return batch.Sum();
            });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Batch size must be at least 1.*");
    }

    [Fact]
    public async Task BatchParallelAsync_WithOrderedOutput_MaintainsOrder()
    {
        var source = Enumerable.Range(1, 50);

        var results = await source.BatchParallelAsync(
            batchSize: 10,
            async (batch, ct) =>
            {
                await Task.Delay(Random.Shared.Next(5, 20), ct);
                return batch.First();
            },
            new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 5,
                OrderedOutput = true
            });

        results.Should().HaveCount(5);
        results.Should().Equal(1, 11, 21, 31, 41);
    }

    [Fact]
    public async Task BatchParallelAsync_WithProgress_ReportsProgress()
    {
        var source = Enumerable.Range(1, 100);
        var snapshots = new ConcurrentBag<ProgressSnapshot>();

        var results = await source.BatchParallelAsync(
            batchSize: 10,
            async (batch, ct) =>
            {
                await Task.Delay(10, ct);
                return batch.Sum();
            },
            new ParallelOptionsRivulet
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
            });

        results.Should().HaveCount(10);
        snapshots.Should().NotBeEmpty();
        snapshots.Max(s => s.ItemsCompleted).Should().Be(10);
    }

    [Fact]
    public async Task BatchParallelAsync_WithRetries_RetriesFailedBatches()
    {
        var source = Enumerable.Range(1, 30);
        var attemptCounts = new ConcurrentDictionary<int, int>();

        var results = await source.BatchParallelAsync(
            batchSize: 10,
            async (batch, ct) =>
            {
                var batchId = batch.First();
                var attempts = attemptCounts.AddOrUpdate(batchId, 1, (_, count) => count + 1);

                await Task.Delay(5, ct);

                if (batchId == 1 && attempts == 1)
                    throw new InvalidOperationException("Transient error");

                return batch.Sum();
            },
            new ParallelOptionsRivulet
            {
                MaxRetries = 2,
                IsTransient = ex => ex is InvalidOperationException,
                ErrorMode = ErrorMode.CollectAndContinue
            });

        results.Should().HaveCount(3);
        attemptCounts[1].Should().Be(2);
    }

    [Fact]
    public async Task BatchParallelAsync_WithErrorMode_BestEffort_SkipsFailedBatches()
    {
        var source = Enumerable.Range(1, 40);

        var results = await source.BatchParallelAsync(
            batchSize: 10,
            async (batch, ct) =>
            {
                await Task.Delay(5, ct);
                if (batch.First() == 11)
                    throw new InvalidOperationException("Permanent error");
                return batch.Sum();
            },
            new ParallelOptionsRivulet
            {
                MaxRetries = 0,
                ErrorMode = ErrorMode.BestEffort
            });

        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task BatchParallelAsync_Cancellation_ThrowsOperationCanceledException()
    {
        var source = Enumerable.Range(1, 1000);
        var cts = new CancellationTokenSource();

        var act = async () => await source.BatchParallelAsync(
            batchSize: 10,
            async (batch, ct) =>
            {
                await Task.Delay(50, ct);
                return batch.Sum();
            },
            cancellationToken: cts.Token);

        cts.CancelAfter(100);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task BatchParallelStreamAsync_ExactBatches_ProcessesCorrectly()
    {
        var source = AsyncEnumerable.Range(1, 100);
        var batchSizes = new ConcurrentBag<int>();

        var results = await source.BatchParallelStreamAsync(
            batchSize: 10,
            async (batch, ct) =>
            {
                batchSizes.Add(batch.Count);
                await Task.Delay(5, ct);
                return batch.Sum();
            }).ToListAsync();

        results.Should().HaveCount(10);
        batchSizes.Should().AllSatisfy(size => size.Should().Be(10));
        results.Sum().Should().Be(5050);
    }

    [Fact]
    public async Task BatchParallelStreamAsync_PartialFinalBatch_IncludesRemainder()
    {
        var source = AsyncEnumerable.Range(1, 25);
        var batchSizes = new ConcurrentBag<int>();

        var results = await source.BatchParallelStreamAsync(
            batchSize: 10,
            async (batch, ct) =>
            {
                batchSizes.Add(batch.Count);
                await Task.Delay(5, ct);
                return batch.Sum();
            }).ToListAsync();

        results.Should().HaveCount(3);
        batchSizes.Should().Contain(10);
        batchSizes.Should().Contain(5);
        results.Sum().Should().Be(325);
    }

    [Fact]
    public async Task BatchParallelStreamAsync_WithTimeout_FlushesOnTimeout()
    {
        var source = SlowAsyncEnumerable(30, delayMs: 50);
        var batchSizes = new ConcurrentBag<int>();

        var results = await source.BatchParallelStreamAsync(
            batchSize: 100,            async (batch, _) =>
            {
                batchSizes.Add(batch.Count);
                await Task.CompletedTask;
                return batch.Count;
            },
            batchTimeout: TimeSpan.FromMilliseconds(200)).ToListAsync();

        results.Should().NotBeEmpty();
        batchSizes.Should().Contain(size => size < 100);
    }

    [Fact]
    public async Task BatchParallelStreamAsync_EmptySource_ReturnsEmpty()
    {
        var source = AsyncEnumerable.Empty<int>();

        var results = await source.BatchParallelStreamAsync(
            batchSize: 10,
            async (batch, _) =>
            {
                await Task.CompletedTask;
                return batch.Sum();
            }).ToListAsync();

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task BatchParallelStreamAsync_InvalidBatchSize_ThrowsArgumentException()
    {
        var source = AsyncEnumerable.Range(1, 10);

        var act = async () =>
        {
            await foreach (var _ in source.BatchParallelStreamAsync(
                batchSize: 0,
                async (batch, _) =>
                {
                    await Task.CompletedTask;
                    return batch.Sum();
                }))
            {
            }
        };

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Batch size must be at least 1.*");
    }

    [Fact]
    public async Task BatchParallelStreamAsync_WithOrderedOutput_MaintainsOrder()
    {
        var source = AsyncEnumerable.Range(1, 50);

        var results = await source.BatchParallelStreamAsync(
            batchSize: 10,
            async (batch, ct) =>
            {
                await Task.Delay(Random.Shared.Next(5, 20), ct);
                return batch.First();
            },
            new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 5,
                OrderedOutput = true
            }).ToListAsync();

        results.Should().HaveCount(5);
        results.Should().Equal(1, 11, 21, 31, 41);
    }

    [Fact]
    public async Task BatchParallelStreamAsync_Cancellation_ThrowsOperationCanceledException()
    {
        var source = AsyncEnumerable.Range(1, 1000);
        var cts = new CancellationTokenSource();

        var act = async () =>
        {
            await foreach (var _ in source.BatchParallelStreamAsync(
                batchSize: 10,
                async (batch, ct) =>
                {
                    await Task.Delay(50, ct);
                    return batch.Sum();
                },
                cancellationToken: cts.Token))
            {
            }
        };

        cts.CancelAfter(100);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task BatchParallelAsync_LargeBatches_HandlesMemoryEfficiently()
    {
        var source = Enumerable.Range(1, 10000);

        var results = await source.BatchParallelAsync(
            batchSize: 1000,
            async (batch, ct) =>
            {
                await Task.Delay(1, ct);
                return batch.Count;
            },
            new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 4
            });

        results.Should().HaveCount(10);
        results.Should().AllSatisfy(count => count.Should().Be(1000));
    }

    [Fact]
    public async Task BatchParallelStreamAsync_WithProgress_TracksProgress()
    {
        var source = AsyncEnumerable.Range(1, 60);
        var snapshots = new ConcurrentBag<ProgressSnapshot>();

        var results = await source.BatchParallelStreamAsync(
            batchSize: 10,
            async (batch, ct) =>
            {
                await Task.Delay(10, ct);
                return batch.Sum();
            },
            new ParallelOptionsRivulet
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
            }).ToListAsync();

        results.Should().HaveCount(6);
        snapshots.Should().NotBeEmpty();
    }

    [Fact]
    public async Task BatchParallelAsync_BatchOfOne_ProcessesIndividually()
    {
        var source = Enumerable.Range(1, 10);

        var results = await source.BatchParallelAsync(
            batchSize: 1,
            async (batch, ct) =>
            {
                await Task.Delay(5, ct);
                return batch.Single();
            },
            new ParallelOptionsRivulet { OrderedOutput = true });

        results.Should().HaveCount(10);
        results.Should().Equal(Enumerable.Range(1, 10));
    }

    [Fact]
    public async Task BatchParallelStreamAsync_BatchOfOne_ProcessesIndividually()
    {
        var source = AsyncEnumerable.Range(1, 10);

        var results = await source.BatchParallelStreamAsync(
            batchSize: 1,
            async (batch, ct) =>
            {
                await Task.Delay(5, ct);
                return batch.Single();
            },
            new ParallelOptionsRivulet { OrderedOutput = true }).ToListAsync();

        results.Should().HaveCount(10);
        results.Should().Equal(Enumerable.Range(1, 10));
    }

    [Fact]
    public async Task BatchParallelAsync_AllExistingOptions_WorkCorrectly()
    {
        var source = Enumerable.Range(1, 50);
        var snapshots = new ConcurrentBag<ProgressSnapshot>();
        var attemptCounts = new ConcurrentDictionary<int, int>();

        var results = await source.BatchParallelAsync(
            batchSize: 10,
            async (batch, ct) =>
            {
                var batchId = batch.First();
                var attempts = attemptCounts.AddOrUpdate(batchId, 1, (_, count) => count + 1);

                await Task.Delay(10, ct);

                if (batchId == 21 && attempts == 1)
                    throw new InvalidOperationException("Transient");

                return batch.Sum();
            },
            new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 3,
                MaxRetries = 2,
                IsTransient = ex => ex is InvalidOperationException,
                ErrorMode = ErrorMode.CollectAndContinue,
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
            });

        results.Should().HaveCount(5);
        results.Should().Equal(55, 155, 255, 355, 455);
        snapshots.Should().NotBeEmpty();
        attemptCounts[21].Should().Be(2);
    }

    [Fact]
    public async Task BatchParallelStreamAsync_TimeoutWithSlowSource_FlushesPartialBatches()
    {
        var source = SlowAsyncEnumerable(15, delayMs: 100);

        var results = await source.BatchParallelStreamAsync(
            batchSize: 20,            async (batch, _) =>
            {
                await Task.CompletedTask;
                return batch.Count;
            },
            batchTimeout: TimeSpan.FromMilliseconds(300)).ToListAsync();

        results.Should().NotBeEmpty();
        results.Should().Contain(count => count < 20);
    }

    [Fact]
    public async Task BatchParallelStreamAsync_NoTimeout_OnlySizeBasedBatching()
    {
        var source = AsyncEnumerable.Range(1, 25);

        var results = await source.BatchParallelStreamAsync(
            batchSize: 10,
            async (batch, ct) =>
            {
                await Task.Delay(5, ct);
                return batch.Count;
            },
            batchTimeout: null
        ).ToListAsync();

        results.Should().HaveCount(3);
        results.Should().Contain(10);
        results.Should().Contain(5);
        results.Sum().Should().Be(25);
    }

    private static async IAsyncEnumerable<int> SlowAsyncEnumerable(int count, int delayMs)
    {
        for (var i = 1; i <= count; i++)
        {
            await Task.Delay(delayMs);
            yield return i;
        }
    }

    [Fact]
    public async Task BatchParallelStreamAsync_TimeoutTriggersBeforeBatchFull()
    {
        var source = SlowAsyncEnumerable(5, delayMs: 100);
        var batchSizes = new ConcurrentBag<int>();

        var results = await source.BatchParallelStreamAsync(
            batchSize: 100,            async (batch, _) =>
            {
                batchSizes.Add(batch.Count);
                await Task.CompletedTask;
                return batch.Sum();
            },
            batchTimeout: TimeSpan.FromMilliseconds(300)).ToListAsync();

        results.Should().NotBeEmpty();
        batchSizes.Should().AllSatisfy(size => size.Should().BeLessThan(100));
    }

    [Fact]
    public async Task BatchParallelAsync_WithPerItemTimeout_AppliesToBatches()
    {
        var source = Enumerable.Range(1, 20);

        var results = await source.BatchParallelAsync(
            batchSize: 10,
            async (batch, ct) =>
            {
                await Task.Delay(5, ct);
                return batch.Sum();
            },
            new ParallelOptionsRivulet
            {
                PerItemTimeout = TimeSpan.FromSeconds(1)
            });

        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task BatchParallelAsync_VeryLargeBatchSize_HandlesCorrectly()
    {
        var source = Enumerable.Range(1, 100);

        var results = await source.BatchParallelAsync(
            batchSize: 1000, async (batch, _) =>
            {
                await Task.CompletedTask;
                return batch.Count;
            });

        results.Should().HaveCount(1);
        results[0].Should().Be(100);
    }

    [Fact]
    public async Task BatchParallelStreamAsync_VeryLargeBatchSize_HandlesCorrectly()
    {
        var source = AsyncEnumerable.Range(1, 50);

        var results = await source.BatchParallelStreamAsync(
            batchSize: 1000,
            async (batch, _) =>
            {
                await Task.CompletedTask;
                return batch.Count;
            }).ToListAsync();

        results.Should().HaveCount(1);
        results[0].Should().Be(50);
    }

    [Fact]
    public async Task BatchParallelStreamAsync_CancellationDuringBatching_Cancels()
    {
        var source = SlowAsyncEnumerable(1000, delayMs: 10);
        var cts = new CancellationTokenSource();

        var act = async () =>
        {
            var count = 0;
            await foreach (var _ in source.BatchParallelStreamAsync(
                batchSize: 10,
                async (batch, ct) =>
                {
                    await Task.Delay(10, ct);
                    return batch.Sum();
                },
                cancellationToken: cts.Token))
            {
                count++;
                if (count >= 3)
                    await cts.CancelAsync();
            }
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task BatchParallelStreamAsync_TimeoutCancellation_HandlesGracefully()
    {
        var source = SlowAsyncEnumerable(100, delayMs: 10);
        var cts = new CancellationTokenSource();

        var act = async () =>
        {
            await foreach (var _ in source.BatchParallelStreamAsync(
                batchSize: 10,
                async (batch, ct) =>
                {
                    await Task.Delay(5, ct);
                    return batch.Sum();
                },
                batchTimeout: TimeSpan.FromMilliseconds(100),
                cancellationToken: cts.Token))
            {
            }
        };

        cts.CancelAfter(200);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task BatchParallelAsync_EmptyBatches_NeverYielded()
    {
        var source = Enumerable.Range(1, 20);
        var emptyBatchCount = 0;

        var results = await source.BatchParallelAsync(
            batchSize: 10,
            async (batch, _) =>
            {
                if (batch.Count == 0)
                    Interlocked.Increment(ref emptyBatchCount);
                await Task.CompletedTask;
                return batch.Count;
            });

        emptyBatchCount.Should().Be(0);
        results.Should().AllSatisfy(count => count.Should().BeGreaterThan(0));
    }

    [Fact]
    public async Task BatchParallelStreamAsync_EmptyBatches_NeverYielded()
    {
        var source = AsyncEnumerable.Range(1, 15);
        var emptyBatchCount = 0;

        var results = await source.BatchParallelStreamAsync(
            batchSize: 10,
            async (batch, _) =>
            {
                if (batch.Count == 0)
                    Interlocked.Increment(ref emptyBatchCount);
                await Task.CompletedTask;
                return batch.Count;
            }).ToListAsync();

        emptyBatchCount.Should().Be(0);
        results.Should().AllSatisfy(count => count.Should().BeGreaterThan(0));
    }

    [Fact]
    public async Task BatchParallelStreamAsync_NoTimeout_RemainderBatch_IsYielded()
    {
        var source = AsyncEnumerable.Range(1, 23);
        var batchContents = new ConcurrentBag<IReadOnlyList<int>>();

        var results = await source.BatchParallelStreamAsync(batchSize: 10, async (batch, _) =>
            {
                batchContents.Add(batch.ToList());
                await Task.CompletedTask;
                return batch.Count;
            }, batchTimeout: null)
            .ToListAsync();

        results.Should().HaveCount(3);
        results.Should().Contain(10);
        results.Should().Contain(3);
        results.Sum().Should().Be(23);
        batchContents.Should().HaveCount(3);
    }

    [Fact]
    public async Task BatchParallelStreamAsync_ManualIteration_YieldsAllResults()
    {
        var source = AsyncEnumerable.Range(1, 17);
        var yieldedResults = await source.BatchParallelStreamAsync(batchSize: 5, async (batch, ct) =>
            {
                await Task.Delay(1, ct);
                return batch.Sum();
            })
            .ToListAsync();

        yieldedResults.Should().HaveCount(4);
        yieldedResults.Sum().Should().Be(153);
    }

    [Fact]
    public async Task BatchParallelStreamAsync_NoTimeout_WithOptions_ProcessesCorrectly()
    {
        var source = AsyncEnumerable.Range(1, 30);
        var processedIndices = new ConcurrentBag<int>();

        var results = await source.BatchParallelStreamAsync(batchSize: 7, async (batch, ct) =>
            {
                await Task.Delay(5, ct);
                return batch.Count;
            }, new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 2,
                OnStartItemAsync = idx =>
                {
                    processedIndices.Add(idx);
                    return ValueTask.CompletedTask;
                }
            }, batchTimeout: null)
            .ToListAsync();

        results.Should().HaveCount(5);
        results.Sum().Should().Be(30);
        processedIndices.Should().HaveCount(5);
    }
}
