using System.Collections.Concurrent;

namespace Rivulet.Core.Tests;

public sealed class ErrorHandlingTests
{
    [Fact]
    public async Task FailFast_SelectParallelAsync_ThrowsImmediatelyOnFirstError()
    {
        var source = Enumerable.Range(1, 100);
        var options = new ParallelOptionsRivulet { ErrorMode = ErrorMode.FailFast, MaxDegreeOfParallelism = 10 };
        var processedCount = 0;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(((Func<Task<List<int>>>?)Act)!);
        exception.Message.ShouldBe("Error at 5");
        processedCount.ShouldBeLessThan(100);
        return;

        Task<List<int>> Act() =>
            source.SelectParallelAsync(async (x, ct) =>
                {
                    Interlocked.Increment(ref processedCount);
                    await Task.Delay(10, ct);
                    if (x == 5) throw new InvalidOperationException("Error at 5");

                    return x * 2;
                },
                options);
    }

    [Fact]
    public async Task FailFast_SelectParallelStreamAsync_ThrowsOnFirstError()
    {
        var source = Enumerable.Range(1, 100).ToAsyncEnumerable();
        var options = new ParallelOptionsRivulet { ErrorMode = ErrorMode.FailFast, MaxDegreeOfParallelism = 10 };

        await Assert.ThrowsAnyAsync<Exception>(Act);
        return;

        async Task Act()
        {
            // ReSharper disable once PossibleMultipleEnumeration
            await foreach (var _ in source.SelectParallelStreamAsync(static async (x, ct) =>
                               {
                                   await Task.Delay(10, ct);
                                   if (x == 5) throw new InvalidOperationException("Error at 5");

                                   return x * 2;
                               },
                               options))
            {
                // Consuming items
            }
        }
    }

    [Fact]
    public async Task CollectAndContinue_SelectParallelAsync_CollectsAllErrorsAndThrowsAggregateException()
    {
        var source = Enumerable.Range(1, 20);
        var options = new ParallelOptionsRivulet { ErrorMode = ErrorMode.CollectAndContinue, MaxDegreeOfParallelism = 5 };

        var act = () => source.SelectParallelAsync(
            static async (x, ct) =>
            {
                await Task.Delay(10, ct);
                if (x % 5 == 0) throw new InvalidOperationException($"Error at {x}");

                return x * 2;
            },
            options);

        var exception = await act.ShouldThrowAsync<AggregateException>();
        exception.InnerExceptions.Count.ShouldBe(4);
        exception.InnerExceptions.ShouldAllBe(static x => x.GetType() == typeof(InvalidOperationException));
    }

    [Fact]
    public async Task CollectAndContinue_ProcessesSuccessfulItemsDespiteErrors()
    {
        var source = Enumerable.Range(1, 10);
        var options = new ParallelOptionsRivulet { ErrorMode = ErrorMode.CollectAndContinue, MaxDegreeOfParallelism = 3 };

        try
        {
            _ = await source.SelectParallelAsync(
                static async (x, ct) =>
                {
                    await Task.Delay(10, ct);
                    if (x == 5) throw new InvalidOperationException("Error at 5");

                    return x * 2;
                },
                options);

            Assert.Fail("Expected AggregateException");
        }
        catch (AggregateException ex)
        {
            ex.InnerExceptions.ShouldHaveSingleItem();
        }
    }

    [Fact]
    public async Task BestEffort_SelectParallelAsync_SwallowsErrorsAndContinues()
    {
        var source = Enumerable.Range(1, 20);
        var options = new ParallelOptionsRivulet { ErrorMode = ErrorMode.BestEffort, MaxDegreeOfParallelism = 5 };
        var errorCount = 0;

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(10, ct);
                if (x % 5 != 0) return x * 2;

                Interlocked.Increment(ref errorCount);
                throw new InvalidOperationException($"Error at {x}");
            },
            options);

        results.Count.ShouldBe(16);
        errorCount.ShouldBe(4);
    }

    [Fact]
    public async Task BestEffort_SelectParallelStreamAsync_SwallowsErrorsAndStreamsSuccessfulResults()
    {
        var source = Enumerable.Range(1, 20).ToAsyncEnumerable();
        var options = new ParallelOptionsRivulet { ErrorMode = ErrorMode.BestEffort, MaxDegreeOfParallelism = 5 };
        var results = await source.SelectParallelStreamAsync(static async (x, ct) =>
                {
                    await Task.Delay(10, ct);
                    if (x % 5 == 0) throw new InvalidOperationException($"Error at {x}");

                    return x * 2;
                },
                options)
            .ToListAsync();

        results.Count.ShouldBe(16);
        foreach (var value in new[] { 10, 20, 30, 40 }) results.ShouldNotContain(value);
    }

    [Fact]
    public async Task OnErrorAsync_FailFast_IsCalled()
    {
        var source = Enumerable.Range(1, 100);
        var errorHandled = false;
        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.FailFast,
            OnErrorAsync = (_, _) =>
            {
                errorHandled = true;
                return ValueTask.FromResult(false);
            }
        };

        await Assert.ThrowsAnyAsync<Exception>(((Func<Task<List<int>>>?)Act)!);
        errorHandled.ShouldBeTrue();
        return;

        Task<List<int>> Act() =>
            source.SelectParallelAsync(static async (x, ct) =>
                {
                    await Task.Delay(10, ct);
                    return x == 10 ? throw new InvalidOperationException("Test error") : x;
                },
                options);
    }

    [Fact]
    public async Task OnErrorAsync_BestEffort_InvokedForEachError()
    {
        var source = Enumerable.Range(1, 20);
        var errorIndices = new ConcurrentBag<int>();
        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.BestEffort,
            OnErrorAsync = (idx, _) =>
            {
                errorIndices.Add(idx);
                return ValueTask.FromResult(true);
            }
        };

        var results = await source.SelectParallelAsync(
            static async (x, ct) =>
            {
                await Task.Delay(10, ct);
                if (x % 5 == 0) throw new InvalidOperationException($"Error at {x}");

                return x * 2;
            },
            options);

        results.Count.ShouldBe(16);
        errorIndices.Count.ShouldBe(4);
    }

    [Fact]
    public async Task OnErrorAsync_CollectAndContinue_InvokedForEachError()
    {
        var source = Enumerable.Range(1, 20);
        var errorMessages = new ConcurrentBag<string>();
        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.CollectAndContinue,
            OnErrorAsync = (_, ex) =>
            {
                errorMessages.Add(ex.Message);
                return ValueTask.FromResult(true);
            }
        };

        await Assert.ThrowsAsync<AggregateException>(((Func<Task<List<int>>>?)Act)!);
        errorMessages.Count.ShouldBe(2);
        errorMessages.ShouldContain("Error at 5");
        errorMessages.ShouldContain("Error at 15");
        return;

        Task<List<int>> Act() =>
            source.SelectParallelAsync(static async (x, ct) =>
                {
                    await Task.Delay(10, ct);
                    if (x is 5 or 15) throw new InvalidOperationException($"Error at {x}");

                    return x * 2;
                },
                options);
    }

    [Fact]
    public async Task OnErrorAsync_ReturningFalse_InCollectAndContinue_CancelsWork()
    {
        var source = Enumerable.Range(1, 100);
        var firstError = true;
        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.CollectAndContinue,
            OnErrorAsync = (_, _) =>
            {
                if (!firstError) return ValueTask.FromResult(true);

                firstError = false;
                return ValueTask.FromResult(false);
            }
        };

        await Assert.ThrowsAsync<AggregateException>(((Func<Task<List<int>>>?)Act)!);
        return;

        Task<List<int>> Act() =>
            source.SelectParallelAsync(static async (x, ct) =>
                {
                    await Task.Delay(10, ct);
                    if (x % 10 == 0) throw new InvalidOperationException($"Error at {x}");

                    return x * 2;
                },
                options);
    }

    [Fact]
    public async Task CollectAndContinue_SelectParallelStreamAsync_SkipsErrorItems()
    {
        var source = Enumerable.Range(1, 10).ToAsyncEnumerable();
        var options = new ParallelOptionsRivulet { ErrorMode = ErrorMode.CollectAndContinue };
        var results = await source.SelectParallelStreamAsync(static async (x, ct) =>
                {
                    await Task.Delay(10, ct);
                    if (x == 5) throw new InvalidOperationException("Error at 5");

                    return x * 2;
                },
                options)
            .ToListAsync();

        results.Count.ShouldBe(9);
        results.ShouldNotContain(10);
    }

    [Fact]
    public async Task OnErrorAsync_SelectParallelStreamAsync_BestEffort_ReturnsFalse_CancelsWork()
    {
        var source = Enumerable.Range(1, 100).ToAsyncEnumerable();
        var errorCaught = false;
        var processedCount = 0;
        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.BestEffort,
            MaxDegreeOfParallelism = 4,
            OnErrorAsync = (_, _) =>
            {
                if (errorCaught) return ValueTask.FromResult(true);

                errorCaught = true;
                return ValueTask.FromResult(false);
            }
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(Act);
        errorCaught.ShouldBeTrue();
        processedCount.ShouldBeLessThan(100);
        return;

        async Task Act()
        {
            // ReSharper disable once PossibleMultipleEnumeration
            await foreach (var _ in source.SelectParallelStreamAsync(async (x, ct) =>
                               {
                                   Interlocked.Increment(ref processedCount);
                                   await Task.Delay(10, ct);
                                   if (x == 10) throw new InvalidOperationException($"Error at {x}");

                                   return x * 2;
                               },
                               options))
            {
                // Consuming items
            }
        }
    }

    [Fact]
    public async Task OnErrorAsync_SelectParallelStreamAsync_CollectAndContinue_ReturnsFalse_CancelsWork()
    {
        var source = Enumerable.Range(1, 100).ToAsyncEnumerable();
        var errorCaught = false;
        var processedCount = 0;
        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.CollectAndContinue,
            MaxDegreeOfParallelism = 4,
            OnErrorAsync = (_, _) =>
            {
                if (errorCaught) return ValueTask.FromResult(true);

                errorCaught = true;
                return ValueTask.FromResult(false);
            }
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(Act);
        errorCaught.ShouldBeTrue();
        processedCount.ShouldBeLessThan(100);
        return;

        async Task Act()
        {
            // ReSharper disable once PossibleMultipleEnumeration
            await foreach (var _ in source.SelectParallelStreamAsync(async (x, ct) =>
                               {
                                   Interlocked.Increment(ref processedCount);
                                   await Task.Delay(10, ct);
                                   if (x == 10) throw new InvalidOperationException($"Error at {x}");

                                   return x * 2;
                               },
                               options))
            {
                // Consuming items
            }
        }
    }
}