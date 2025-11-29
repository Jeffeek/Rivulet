using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Rivulet.Core.Resilience;

namespace Rivulet.Core.Tests;

[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public class EdgeCasesAndCoverageTests
{
    [Fact]
    public void ParallelOptionsRivulet_DefaultValues_AreCorrect()
    {
        var options = new ParallelOptionsRivulet();

        options.MaxDegreeOfParallelism.ShouldBe(Math.Max(1, Environment.ProcessorCount));
        options.PerItemTimeout.ShouldBeNull();
        options.ErrorMode.ShouldBe(ErrorMode.FailFast);
        options.OnErrorAsync.ShouldBeNull();
        options.OnStartItemAsync.ShouldBeNull();
        options.OnCompleteItemAsync.ShouldBeNull();
        options.OnThrottleAsync.ShouldBeNull();
        options.OnDrainAsync.ShouldBeNull();
        options.IsTransient.ShouldBeNull();
        options.MaxRetries.ShouldBe(0);
        options.BaseDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
        options.BackoffStrategy.ShouldBe(BackoffStrategy.Exponential);
        options.ChannelCapacity.ShouldBe(1024);
    }

    [Fact]
    public void ParallelOptionsRivulet_CanSetAllProperties()
    {
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 10,
            PerItemTimeout = TimeSpan.FromSeconds(5),
            ErrorMode = ErrorMode.BestEffort,
            OnErrorAsync = (_, _) => ValueTask.FromResult(true),
            OnStartItemAsync = _ => ValueTask.CompletedTask,
            OnCompleteItemAsync = _ => ValueTask.CompletedTask,
            OnThrottleAsync = _ => ValueTask.CompletedTask,
            OnDrainAsync = _ => ValueTask.CompletedTask,
            IsTransient = _ => true,
            MaxRetries = 5,
            BaseDelay = TimeSpan.FromMilliseconds(200),
            BackoffStrategy = BackoffStrategy.ExponentialJitter,
            ChannelCapacity = 500
        };

        options.MaxDegreeOfParallelism.ShouldBe(10);
        options.PerItemTimeout.ShouldBe(TimeSpan.FromSeconds(5));
        options.ErrorMode.ShouldBe(ErrorMode.BestEffort);
        options.OnErrorAsync.ShouldNotBeNull();
        options.OnStartItemAsync.ShouldNotBeNull();
        options.OnCompleteItemAsync.ShouldNotBeNull();
        options.OnThrottleAsync.ShouldNotBeNull();
        options.OnDrainAsync.ShouldNotBeNull();
        options.IsTransient.ShouldNotBeNull();
        options.MaxRetries.ShouldBe(5);
        options.BaseDelay.ShouldBe(TimeSpan.FromMilliseconds(200));
        options.BackoffStrategy.ShouldBe(BackoffStrategy.ExponentialJitter);
        options.ChannelCapacity.ShouldBe(500);
    }

    [Fact]
    public void ErrorMode_AllValues_AreDefined()
    {
        var values = Enum.GetValues<ErrorMode>();
        values.ShouldContain(ErrorMode.FailFast);
        values.ShouldContain(ErrorMode.CollectAndContinue);
        values.ShouldContain(ErrorMode.BestEffort);
    }

    [Fact]
    public async Task SelectParallelAsync_VerySmallChannelCapacity_StillWorks()
    {
        var source = Enumerable.Range(1, 100);
        var options = new ParallelOptionsRivulet
        {
            ChannelCapacity = 1,
            MaxDegreeOfParallelism = 2
        };

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(5, ct);
                return x * 2;
            },
            options);

        results.Count.ShouldBe(100);
    }

    [Fact]
    public async Task SelectParallelAsync_SingleDegreeOfParallelism_ProcessesSequentially()
    {
        var source = Enumerable.Range(1, 10);
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 1
        };
        var concurrentCount = 0;
        var maxConcurrent = 0;
        var lockObj = new object();

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                lock (lockObj)
                {
                    concurrentCount++;
                    maxConcurrent = Math.Max(maxConcurrent, concurrentCount);
                }

                await Task.Delay(10, ct);

                lock (lockObj)
                {
                    concurrentCount--;
                }

                return x * 2;
            },
            options);

        results.Count.ShouldBe(10);
        maxConcurrent.ShouldBe(1);
    }

    [Fact]
    public async Task SelectParallelStreamAsync_EmptyAfterSomeItems_CompletesCorrectly()
    {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        async IAsyncEnumerable<int> Source()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            for (var i = 1; i <= 5; i++)
            {
                yield return i;
            }
        }

        var results = await Source().SelectParallelStreamAsync((x, _) => new ValueTask<int>(x * 2)).ToListAsync();

        results.Count.ShouldBe(5);
    }

    [Fact]
    public async Task SelectParallelAsync_ExceptionInWriter_PropagatesCorrectly()
    {
        IEnumerable<int> FaultySource()
        {
            yield return 1;
            yield return 2;
            throw new InvalidOperationException("Source enumeration error");
        }

        async Task<List<int>> Act() => await FaultySource().SelectParallelAsync((x, _) => new ValueTask<int>(x * 2));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(((Func<Task<List<int>>>?)Act)!);
        exception.Message.ShouldBe("Source enumeration error");
    }

    [Fact]
    public async Task SelectParallelStreamAsync_ExceptionInSource_PropagatesCorrectly()
    {
        async IAsyncEnumerable<int> FaultySource()
        {
            yield return 1;
            yield return 2;
            await Task.Yield();
            throw new InvalidOperationException("Async source error");
        }

        async Task Act() => await FaultySource().SelectParallelStreamAsync((x, _) => new ValueTask<int>(x * 2)).ToListAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(Act);
    }

    [Fact]
    public async Task CollectAsync_ImplicitlyTested_ThroughForEachParallelAsync()
    {
        var source = Enumerable.Range(1, 50).ToAsyncEnumerable();
        var processedCount = 0;

        await source.ForEachParallelAsync(
            (_, _) =>
            {
                Interlocked.Increment(ref processedCount);
                return ValueTask.CompletedTask;
            });

        processedCount.ShouldBe(50);
    }

    [Fact]
    public async Task SelectParallelAsync_CatchClause_CatchesNonFailFastErrors()
    {
        var source = Enumerable.Range(1, 10);
        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.CollectAndContinue
        };

        var act = async () => await source.SelectParallelAsync(
            (x, _) =>
            {
                if (x == 5)
                    throw new InvalidOperationException("Error");
                return new ValueTask<int>(x * 2);
            },
            options);

        await act.ShouldThrowAsync<AggregateException>();
    }

    [Fact]
    public async Task SelectParallelStreamAsync_ReaderTaskCompletion_HandlesWorkerCompletion()
    {
        var source = Enumerable.Range(1, 20).ToAsyncEnumerable();
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 3
        };

        var results = await source.SelectParallelStreamAsync(async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x * 2;
            }, options)
            .ToListAsync();

        results.Count.ShouldBe(20);
    }

    [Fact]
    public async Task OnErrorAsync_ReturnsFalse_InBestEffort_CallsCallback()
    {
        var source = Enumerable.Range(1, 100);
        var errorCount = 0;
        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.BestEffort,
            OnErrorAsync = (_, _) =>
            {
                Interlocked.Increment(ref errorCount);
                return ValueTask.FromResult(false);
            }
        };

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(10, ct);
                if (x == 10)
                    throw new InvalidOperationException("Error");
                ct.ThrowIfCancellationRequested();
                return x * 2;
            },
            options);

        errorCount.ShouldBeGreaterThanOrEqualTo(1);
        results.Count.ShouldBeLessThan(100);
    }

    [Fact]
    public async Task SelectParallelStreamAsync_WorkerIndexCalculation_WorksCorrectly()
    {
        var source = Enumerable.Range(1, 30).ToAsyncEnumerable();
        var workerIndices = new ConcurrentBag<int>();
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 3,
            OnStartItemAsync = idx =>
            {
                workerIndices.Add(idx);
                return ValueTask.CompletedTask;
            }
        };

        var results = await source.SelectParallelStreamAsync((x, _) => new ValueTask<int>(x * 2), options).ToListAsync();

        results.Count.ShouldBe(30);
        workerIndices.Count.ShouldBe(30);
    }

    [Fact]
    public async Task ChannelWriter_TryComplete_CalledInFinally()
    {
        IEnumerable<int> Source()
        {
            yield return 1;
            yield return 2;
            throw new InvalidOperationException("Writer error");
        }

        async Task<List<int>> Act() => await Source().SelectParallelAsync((x, _) => new ValueTask<int>(x * 2));

        await Assert.ThrowsAsync<InvalidOperationException>(((Func<Task<List<int>>>?)Act)!);
    }

    [Fact]
    public async Task SelectParallelStreamAsync_OutputWriter_TryComplete_CalledInFinally()
    {
        var source = Enumerable.Range(1, 10).ToAsyncEnumerable();
        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.BestEffort,
            MaxDegreeOfParallelism = 2
        };

        var results = await source.SelectParallelStreamAsync(async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x * 2;
            }, options)
            .ToListAsync();

        results.Count.ShouldBe(10);
    }

    [Fact]
    public async Task SelectParallelAsync_IndexIncrement_WorksAcrossMultipleIterations()
    {
        var source = Enumerable.Range(1, 50);
        var processedIndices = new ConcurrentBag<int>();
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 5,
            OnStartItemAsync = idx =>
            {
                processedIndices.Add(idx);
                return ValueTask.CompletedTask;
            }
        };

        await source.SelectParallelAsync(
            (x, _) => new ValueTask<int>(x * 2),
            options);

        processedIndices.Count.ShouldBe(50);
    }

    [Fact]
    public async Task WaitToWriteAsync_ReturnsFalse_HandledGracefully()
    {
        var source = Enumerable.Range(1, 1000);
        var options = new ParallelOptionsRivulet
        {
            ChannelCapacity = 1,
            MaxDegreeOfParallelism = 1,
            ErrorMode = ErrorMode.FailFast
        };

        async Task<List<int>> Act() =>
            await source.SelectParallelAsync(async (x, ct) =>
            {
                await Task.Delay(100, ct);
                if (x == 5) throw new InvalidOperationException("Error at 5");
                return x * 2;
            }, options);

        await Assert.ThrowsAnyAsync<Exception>(((Func<Task<List<int>>>?)Act)!);
    }

    [Fact]
    public async Task MultipleExceptionTypes_InCollectAndContinue_AllCollected()
    {
        var source = Enumerable.Range(1, 10);
        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.CollectAndContinue
        };

        var act = async () => await source.SelectParallelAsync(
            (x, _) =>
            {
                return x switch
                {
                    3 => throw new InvalidOperationException("Error 3"),
                    7 => throw new ArgumentException("Error 7"),
                    _ => new ValueTask<int>(x * 2)
                };
            },
            options);

        var exception = await act.ShouldThrowAsync<AggregateException>();
        exception.InnerExceptions.Count.ShouldBe(2);
        exception.InnerExceptions.ShouldAllBe(x => x != null!);
    }

    [Fact]
    public async Task SelectParallelStreamAsync_WriterException_PropagatesAfterConsumption()
    {
        var source = GetSourceWithDelayedError();
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 2,
            ErrorMode = ErrorMode.BestEffort
        };

        var act = async () =>
        {
            await foreach (var _ in source.SelectParallelStreamAsync(
                async (x, ct) =>
                {
                    await Task.Delay(1, ct);
                    return x * 2;
                },
                options))
            {
                // Consuming items
            }
        };

        await act.ShouldThrowAsync<InvalidOperationException>();
    }

    private static async IAsyncEnumerable<int> GetSourceWithDelayedError()
    {
        for (var i = 1; i <= 5; i++)
        {
            await Task.Delay(1);
            yield return i;
        }
        throw new InvalidOperationException("Writer error after items");
    }

    [Fact]
    public async Task SelectParallelStreamAsync_CancellationDuringUnorderedOutput_ThrowsOperationCanceledException()
    {
        var source = Enumerable.Range(1, 100).ToAsyncEnumerable();
        using var cts = new CancellationTokenSource();
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            OrderedOutput = false
        };

        var results = new List<int>();

        var act = async () =>
        {
            await foreach (var result in source.SelectParallelStreamAsync(
                async (x, ct) =>
                {
                    await Task.Delay(10, ct);
                    return x * 2;
                },
                options,
                cts.Token))
            {
                results.Add(result);
                if (results.Count == 10)
                {
                    await cts.CancelAsync();
                }
            }
        };

        await act.ShouldThrowAsync<OperationCanceledException>();
        results.Count.ShouldBeGreaterThan(0);
        results.Count.ShouldBeLessThan(100);
    }

    [Fact]
    public async Task SelectParallelStreamAsync_CancellationDuringOrderedOutput_ThrowsOperationCanceledException()
    {
        var source = Enumerable.Range(1, 100).ToAsyncEnumerable();
        using var cts = new CancellationTokenSource();
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            OrderedOutput = true
        };

        var results = new List<int>();

        var act = async () =>
        {
            await foreach (var result in source.SelectParallelStreamAsync(
                async (x, ct) =>
                {
                    await Task.Delay(10, ct);
                    return x * 2;
                },
                options,
                cts.Token))
            {
                results.Add(result);
                if (results.Count == 15)
                {
                    await cts.CancelAsync();
                }
            }
        };

        await act.ShouldThrowAsync<OperationCanceledException>();
        results.Count.ShouldBeGreaterThan(0);
        results.Count.ShouldBeLessThan(100);
    }

    [Fact]
    public async Task SelectParallelStreamAsync_CompleteSuccessfully_UnorderedOutput_ReachesTaskWhenAll()
    {
        var source = Enumerable.Range(1, 50).ToAsyncEnumerable();
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 8,
            OrderedOutput = false
        };

        var results = await source.SelectParallelStreamAsync(
            async (x, ct) =>
            {
                await Task.Delay(5, ct);
                return x * 2;
            },
            options).ToListAsync();

        results.Count.ShouldBe(50);
        results.OrderBy(x => x).ShouldBe(Enumerable.Range(1, 50).Select(x => x * 2));
    }

    [Fact]
    public async Task SelectParallelStreamAsync_CompleteSuccessfully_OrderedOutput_ReachesTaskWhenAll()
    {
        var source = Enumerable.Range(1, 50).ToAsyncEnumerable();
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 8,
            OrderedOutput = true
        };

        var results = await source.SelectParallelStreamAsync(
            async (x, ct) =>
            {
                await Task.Delay(5, ct);
                return x * 2;
            },
            options).ToListAsync();

        results.Count.ShouldBe(50);
        results.ShouldBe(Enumerable.Range(1, 50).Select(x => x * 2));
    }

    [Fact]
    public async Task SelectParallelStreamAsync_UnorderedOutput_FullConsumption_CoversAwaitForeach()
    {
        var source = Enumerable.Range(1, 25).ToAsyncEnumerable();
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 5,
            OrderedOutput = false
        };

        var count = await source.SelectParallelStreamAsync(async (x, ct) =>
            {
                await Task.Delay(2, ct);
                return x * 3;
            }, options)
            .CountAsync();

        count.ShouldBe(25);
    }

    [Fact]
    public async Task SelectParallelStreamAsync_OrderedOutput_FullConsumption_CoversAwaitForeach()
    {
        var source = Enumerable.Range(1, 25).ToAsyncEnumerable();
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 5,
            OrderedOutput = true
        };

        var count = await source.SelectParallelStreamAsync(async (x, ct) =>
            {
                await Task.Delay(2, ct);
                return x * 3;
            }, options)
            .CountAsync();

        count.ShouldBe(25);
    }
}
