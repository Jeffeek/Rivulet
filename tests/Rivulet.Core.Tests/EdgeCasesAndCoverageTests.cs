using FluentAssertions;
using System.Collections.Concurrent;

namespace Rivulet.Core.Tests;

public class EdgeCasesAndCoverageTests
{
    [Fact]
    public void ParallelOptionsRivulet_DefaultValues_AreCorrect()
    {
        var options = new ParallelOptionsRivulet();

        options.MaxDegreeOfParallelism.Should().Be(Math.Max(1, Environment.ProcessorCount));
        options.PerItemTimeout.Should().BeNull();
        options.ErrorMode.Should().Be(ErrorMode.FailFast);
        options.OnErrorAsync.Should().BeNull();
        options.OnStartItemAsync.Should().BeNull();
        options.OnCompleteItemAsync.Should().BeNull();
        options.OnThrottleAsync.Should().BeNull();
        options.OnDrainAsync.Should().BeNull();
        options.IsTransient.Should().BeNull();
        options.MaxRetries.Should().Be(0);
        options.BaseDelay.Should().Be(TimeSpan.FromMilliseconds(100));
        options.ChannelCapacity.Should().Be(1024);
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
            ChannelCapacity = 500
        };

        options.MaxDegreeOfParallelism.Should().Be(10);
        options.PerItemTimeout.Should().Be(TimeSpan.FromSeconds(5));
        options.ErrorMode.Should().Be(ErrorMode.BestEffort);
        options.OnErrorAsync.Should().NotBeNull();
        options.OnStartItemAsync.Should().NotBeNull();
        options.OnCompleteItemAsync.Should().NotBeNull();
        options.OnThrottleAsync.Should().NotBeNull();
        options.OnDrainAsync.Should().NotBeNull();
        options.IsTransient.Should().NotBeNull();
        options.MaxRetries.Should().Be(5);
        options.BaseDelay.Should().Be(TimeSpan.FromMilliseconds(200));
        options.ChannelCapacity.Should().Be(500);
    }

    [Fact]
    public void ErrorMode_AllValues_AreDefined()
    {
        Enum.GetValues<ErrorMode>().Should().Contain([
            ErrorMode.FailFast,
            ErrorMode.CollectAndContinue,
            ErrorMode.BestEffort
        ]);
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

        results.Should().HaveCount(100);
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

        results.Should().HaveCount(10);
        maxConcurrent.Should().Be(1);
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

        results.Should().HaveCount(5);
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
        exception.Message.Should().Be("Source enumeration error");
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

        processedCount.Should().Be(50);
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

        await act.Should().ThrowAsync<AggregateException>();
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

        results.Should().HaveCount(20);
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

        errorCount.Should().BeGreaterThanOrEqualTo(1);
        results.Count.Should().BeLessThan(100);
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

        results.Should().HaveCount(30);
        workerIndices.Should().HaveCount(30);
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

        results.Should().HaveCount(10);
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

        processedIndices.Should().HaveCount(50);
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

        var exception = await act.Should().ThrowAsync<AggregateException>();
        exception.Which.InnerExceptions.Should().HaveCount(2);
        exception.Which.InnerExceptions.Should().ContainItemsAssignableTo<Exception>();
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
            }
        };

        await act.Should().ThrowAsync<InvalidOperationException>();
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
        var cts = new CancellationTokenSource();
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

        await act.Should().ThrowAsync<OperationCanceledException>();
        results.Count.Should().BeGreaterThan(0).And.BeLessThan(100);
    }

    [Fact]
    public async Task SelectParallelStreamAsync_CancellationDuringOrderedOutput_ThrowsOperationCanceledException()
    {
        var source = Enumerable.Range(1, 100).ToAsyncEnumerable();
        var cts = new CancellationTokenSource();
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

        await act.Should().ThrowAsync<OperationCanceledException>();
        results.Count.Should().BeGreaterThan(0).And.BeLessThan(100);
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

        results.Should().HaveCount(50);
        results.Should().BeEquivalentTo(Enumerable.Range(1, 50).Select(x => x * 2));
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

        results.Should().HaveCount(50);
        results.Should().Equal(Enumerable.Range(1, 50).Select(x => x * 2));
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

        count.Should().Be(25);
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

        count.Should().Be(25);
    }
}
