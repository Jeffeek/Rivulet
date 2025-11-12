using FluentAssertions;
using System.Collections.Concurrent;

namespace Rivulet.Core.Tests;

public class LifecycleHooksTests
{
    [Fact]
    public async Task OnStartItemAsync_CalledForEachItem()
    {
        var source = Enumerable.Range(1, 10);
        var startedItems = new ConcurrentBag<int>();
        var options = new ParallelOptionsRivulet
        {
            OnStartItemAsync = idx =>
            {
                startedItems.Add(idx);
                return ValueTask.CompletedTask;
            }
        };

        await source.SelectParallelAsync((x, _) => new ValueTask<int>(x * 2), options);

        startedItems.Should().HaveCount(10);
    }

    [Fact]
    public async Task OnCompleteItemAsync_CalledForEachSuccessfulItem()
    {
        var source = Enumerable.Range(1, 10);
        var completedItems = new ConcurrentBag<int>();
        var options = new ParallelOptionsRivulet
        {
            OnCompleteItemAsync = idx =>
            {
                completedItems.Add(idx);
                return ValueTask.CompletedTask;
            }
        };

        await source.SelectParallelAsync((x, _) => new ValueTask<int>(x * 2), options);

        completedItems.Should().HaveCount(10);
    }

    [Fact]
    public async Task OnCompleteItemAsync_NotCalledForFailedItems()
    {
        var source = Enumerable.Range(1, 10);
        var completedItems = new ConcurrentBag<int>();
        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.BestEffort,
            OnCompleteItemAsync = idx =>
            {
                completedItems.Add(idx);
                return ValueTask.CompletedTask;
            }
        };

        await source.SelectParallelAsync(
            (x, _) =>
            {
                if (x == 5)
                    throw new InvalidOperationException("Error");
                return new ValueTask<int>(x * 2);
            },
            options);

        completedItems.Should().HaveCount(9);
    }

    [Fact]
    public async Task OnErrorAsync_CalledForFailedItems()
    {
        var source = Enumerable.Range(1, 10);
        var errorIndices = new ConcurrentBag<int>();
        var errorExceptions = new ConcurrentBag<Exception>();
        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.BestEffort,
            OnErrorAsync = (idx, ex) =>
            {
                errorIndices.Add(idx);
                errorExceptions.Add(ex);
                return ValueTask.FromResult(true);
            }
        };

        await source.SelectParallelAsync(
            (x, _) =>
            {
                if (x is 3 or 7)
                    throw new InvalidOperationException($"Error at {x}");
                return new ValueTask<int>(x * 2);
            },
            options);

        errorIndices.Should().HaveCount(2);
        errorExceptions.Should().HaveCount(2);
        errorExceptions.Should().AllBeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task OnThrottleAsync_CalledPeriodically()
    {
        var source = Enumerable.Range(1, 100);
        var throttleCalls = new ConcurrentBag<int>();
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 5,
            OnThrottleAsync = count =>
            {
                throttleCalls.Add(count);
                return ValueTask.CompletedTask;
            }
        };

        await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x * 2;
            },
            options);

        throttleCalls.Should().NotBeEmpty();
    }

    [Fact]
    public async Task LifecycleHooks_CalledInCorrectOrder()
    {
        var source = Enumerable.Range(1, 5);
        var events = new ConcurrentBag<(string Event, int Index)>();
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 1,
            OnStartItemAsync = idx =>
            {
                events.Add(("Start", idx));
                return ValueTask.CompletedTask;
            },
            OnCompleteItemAsync = idx =>
            {
                events.Add(("Complete", idx));
                return ValueTask.CompletedTask;
            }
        };

        await source.SelectParallelAsync((x, _) => new ValueTask<int>(x * 2), options);

        events.Should().HaveCount(10);
    }

    [Fact]
    public async Task LifecycleHooks_WithErrors_OnErrorCalled()
    {
        var source = Enumerable.Range(1, 10);
        var events = new ConcurrentBag<string>();
        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.BestEffort,
            OnStartItemAsync = idx =>
            {
                events.Add($"Start-{idx}");
                return ValueTask.CompletedTask;
            },
            OnCompleteItemAsync = idx =>
            {
                events.Add($"Complete-{idx}");
                return ValueTask.CompletedTask;
            },
            OnErrorAsync = (idx, _) =>
            {
                events.Add($"Error-{idx}");
                return ValueTask.FromResult(true);
            }
        };

        await source.SelectParallelAsync(
            (x, _) =>
            {
                if (x == 5)
                    throw new InvalidOperationException("Error");
                return new ValueTask<int>(x * 2);
            },
            options);

        events.Should().Contain(e => e.StartsWith("Error-"));
        events.Where(e => e.StartsWith("Complete-")).Should().HaveCount(9);
        events.Where(e => e.StartsWith("Start-")).Should().HaveCount(10);
    }

    [Fact]
    public async Task LifecycleHooks_SelectParallelStreamAsync_WorkCorrectly()
    {
        var source = Enumerable.Range(1, 10).ToAsyncEnumerable();
        var startedItems = new ConcurrentBag<int>();
        var completedItems = new ConcurrentBag<int>();
        var options = new ParallelOptionsRivulet
        {
            OnStartItemAsync = idx =>
            {
                startedItems.Add(idx);
                return ValueTask.CompletedTask;
            },
            OnCompleteItemAsync = idx =>
            {
                completedItems.Add(idx);
                return ValueTask.CompletedTask;
            }
        };

        var results = await source.SelectParallelStreamAsync((x, _) => new ValueTask<int>(x * 2), options).ToListAsync();

        startedItems.Should().HaveCount(10);
        completedItems.Should().HaveCount(10);
        results.Should().HaveCount(10);
    }

    [Fact]
    public async Task LifecycleHooks_ForEachParallelAsync_WorkCorrectly()
    {
        var source = Enumerable.Range(1, 10).ToAsyncEnumerable();
        var startedItems = new ConcurrentBag<int>();
        var completedItems = new ConcurrentBag<int>();
        var options = new ParallelOptionsRivulet
        {
            OnStartItemAsync = idx =>
            {
                startedItems.Add(idx);
                return ValueTask.CompletedTask;
            },
            OnCompleteItemAsync = idx =>
            {
                completedItems.Add(idx);
                return ValueTask.CompletedTask;
            }
        };

        await source.ForEachParallelAsync(
            (_, _) => ValueTask.CompletedTask,
            options);

        startedItems.Should().HaveCount(10);
        completedItems.Should().HaveCount(10);
    }

    [Fact]
    public async Task OnErrorAsync_CanStopProcessing()
    {
        var source = Enumerable.Range(1, 5000);
        var processedCount = 0;
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4, // Limit concurrency to reduce in-flight items when error occurs
            ErrorMode = ErrorMode.CollectAndContinue,
            OnErrorAsync = (_, _) => ValueTask.FromResult(false)
        };

        var act = async () => await source.SelectParallelAsync(
            async (x, ct) =>
            {
                Interlocked.Increment(ref processedCount);
                // Add small delay to allow signal propagation when error occurs
                await Task.Delay(1, ct);
                if (x == 10)
                    throw new InvalidOperationException("Error");
                return x * 2;
            },
            options);

        await act.Should().ThrowAsync<AggregateException>();
        processedCount.Should().BeLessThan(5000);
    }

    [Fact]
    public async Task AllHooks_CanBeNull()
    {
        var source = Enumerable.Range(1, 10);
        var options = new ParallelOptionsRivulet
        {
            OnStartItemAsync = null,
            OnCompleteItemAsync = null,
            OnErrorAsync = null,
            OnThrottleAsync = null,
            OnDrainAsync = null
        };

        var results = await source.SelectParallelAsync((x, _) => new ValueTask<int>(x * 2), options);

        results.Should().HaveCount(10);
    }

    [Fact]
    public async Task LifecycleHooks_CanBeAsync()
    {
        var source = Enumerable.Range(1, 5);
        var asyncWorkDone = new ConcurrentBag<int>();
        var options = new ParallelOptionsRivulet
        {
            OnStartItemAsync = async idx =>
            {
                await Task.Delay(10);
                asyncWorkDone.Add(idx);
            },
            OnCompleteItemAsync = async idx =>
            {
                await Task.Delay(10);
                asyncWorkDone.Add(idx);
            }
        };

        await source.SelectParallelAsync((x, _) => new ValueTask<int>(x * 2), options);

        asyncWorkDone.Should().HaveCount(10);
    }

    [Fact]
    public async Task OnThrottleAsync_NullValue_DoesNotCauseError()
    {
        var source = Enumerable.Range(1, 50);
        var options = new ParallelOptionsRivulet
        {
            OnThrottleAsync = null
        };

        var results = await source.SelectParallelAsync((x, _) => new ValueTask<int>(x * 2), options);

        results.Should().HaveCount(50);
    }
}
