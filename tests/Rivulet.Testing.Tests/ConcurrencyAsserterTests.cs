namespace Rivulet.Testing.Tests;

public sealed class ConcurrencyAsserterTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithZeroValues()
    {
        var asserter = new ConcurrencyAsserter();

        asserter.CurrentConcurrency.ShouldBe(0);
        asserter.MaxConcurrency.ShouldBe(0);
    }

    [Fact]
    public void EnterAsync_ShouldIncrementCurrentConcurrency()
    {
        var asserter = new ConcurrencyAsserter();

        using var scope = asserter.Enter();

        asserter.CurrentConcurrency.ShouldBe(1);
        asserter.MaxConcurrency.ShouldBe(1);
    }

    [Fact]
    public void DisposingScope_ShouldDecrementCurrentConcurrency()
    {
        var asserter = new ConcurrencyAsserter();

        var scope = asserter.Enter();
        scope.Dispose();

        asserter.CurrentConcurrency.ShouldBe(0);
        asserter.MaxConcurrency.ShouldBe(1);
    }

    [Fact]
    public void MultipleEnters_ShouldTrackMaxConcurrency()
    {
        var asserter = new ConcurrencyAsserter();

        var scope1 = asserter.Enter();
        var scope2 = asserter.Enter();
        var scope3 = asserter.Enter();

        asserter.CurrentConcurrency.ShouldBe(3);
        asserter.MaxConcurrency.ShouldBe(3);

        scope1.Dispose();
        scope2.Dispose();

        asserter.CurrentConcurrency.ShouldBe(1);
        asserter.MaxConcurrency.ShouldBe(3);

        scope3.Dispose();
    }

    [Fact]
    public async Task ConcurrentOperations_ShouldTrackCorrectly()
    {
        var asserter = new ConcurrencyAsserter();

        var tasks = Enumerable.Range(0, 10)
            .Select(async _ =>
            {
                using var scope = asserter.Enter();
                await Task.Delay(50, CancellationToken.None);
            })
            .ToArray();

        await Task.WhenAll(tasks);

        asserter.CurrentConcurrency.ShouldBe(0);
        asserter.MaxConcurrency.ShouldBeGreaterThan(1);
    }

    [Fact]
    public void Reset_ShouldResetCounters()
    {
        var asserter = new ConcurrencyAsserter();

        var scope1 = asserter.Enter();
        asserter.Enter();
        scope1.Dispose();

        asserter.Reset();

        asserter.CurrentConcurrency.ShouldBe(0);
        asserter.MaxConcurrency.ShouldBe(0);
    }

    [Fact]
    public async Task StressTest_ManyThreads_ShouldMaintainAccuracy()
    {
        var asserter = new ConcurrencyAsserter();

        var tasks = Enumerable.Range(0, 1000)
            .Select(async _ =>
            {
                using var scope = asserter.Enter();
                await Task.Delay(1, CancellationToken.None);
            })
            .ToArray();

        await Task.WhenAll(tasks);

        asserter.CurrentConcurrency.ShouldBe(0);
        asserter.MaxConcurrency.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void MaxConcurrency_ShouldOnlyIncrease()
    {
        var asserter = new ConcurrencyAsserter();

        var scope1 = asserter.Enter();
        var scope2 = asserter.Enter();
        var scope3 = asserter.Enter();

        asserter.MaxConcurrency.ShouldBe(3);

        scope1.Dispose();
        scope2.Dispose();

        asserter.MaxConcurrency.ShouldBe(3);

        var scope4 = asserter.Enter();

        asserter.MaxConcurrency.ShouldBe(3);

        scope3.Dispose();
        scope4.Dispose();
    }

    [Fact]
    public void DisposingScope_MultipleTimes_ShouldBeIdempotent()
    {
        var asserter = new ConcurrencyAsserter();

        var scope = asserter.Enter();
        scope.Dispose();
        scope.Dispose();
        scope.Dispose();

        asserter.CurrentConcurrency.ShouldBe(0);
    }

    [Fact]
    public async Task ParallelExecutions_ShouldTrackConcurrency()
    {
        var asserter = new ConcurrencyAsserter();
        var concurrentExecutions = 0;
        var maxObserved = 0;
        var lockObj = new object();

        var tasks = Enumerable.Range(0, 50)
            .Select(async _ =>
            {
                using var scope = asserter.Enter();

                lock (lockObj)
                {
                    concurrentExecutions++;
                    if (concurrentExecutions > maxObserved) maxObserved = concurrentExecutions;
                }

                await Task.Delay(10, CancellationToken.None);

                lock (lockObj) concurrentExecutions--;
            })
            .ToArray();

        await Task.WhenAll(tasks);

        asserter.CurrentConcurrency.ShouldBe(0);
        asserter.MaxConcurrency.ShouldBeGreaterThan(1);
        asserter.MaxConcurrency.ShouldBe(maxObserved);
    }

    [Fact]
    public void UsingStatement_ShouldAutomaticallyDisposeScope()
    {
        var asserter = new ConcurrencyAsserter();

        using (asserter.Enter()) asserter.CurrentConcurrency.ShouldBe(1);

        asserter.CurrentConcurrency.ShouldBe(0);
    }

    [Fact]
    public void NestedScopes_ShouldTrackCorrectly()
    {
        var asserter = new ConcurrencyAsserter();

        using (asserter.Enter())
        {
            asserter.CurrentConcurrency.ShouldBe(1);

            using (asserter.Enter())
            {
                asserter.CurrentConcurrency.ShouldBe(2);

                using (asserter.Enter()) asserter.CurrentConcurrency.ShouldBe(3);

                asserter.CurrentConcurrency.ShouldBe(2);
            }

            asserter.CurrentConcurrency.ShouldBe(1);
        }

        asserter.CurrentConcurrency.ShouldBe(0);
        asserter.MaxConcurrency.ShouldBe(3);
    }

    [Fact]
    public async Task ExtremeContention_ShouldHandleCompareExchangeRetries()
    {
        // This test creates extreme contention to force the CompareExchange retry path
        // Using Task.Run to create true thread contention instead of async state machine
        var asserter = new ConcurrencyAsserter();
        var startSignal = new TaskCompletionSource<bool>();

        var tasks = Enumerable.Range(0, 100)
            .Select(_ => Task.Run(async () =>
            {
                await startSignal.Task; // Wait for signal to start all at once
                using var scope = asserter.Enter();
                await Task.Delay(1, CancellationToken.None);
            }))
            .ToArray();

        // Give threads time to all reach the wait point
        await Task.Delay(50, CancellationToken.None);

        // Release all threads at once to create maximum contention
        startSignal.SetResult(true);

        await Task.WhenAll(tasks);

        asserter.CurrentConcurrency.ShouldBe(0);
        asserter.MaxConcurrency.ShouldBeGreaterThan(50); // With 100 threads, expect high concurrency
    }

    [Fact]
    public void PropertyGetters_ShouldReturnCorrectValues()
    {
        var asserter = new ConcurrencyAsserter();

        // Initial state
        asserter.MaxConcurrency.ShouldBe(0);
        asserter.CurrentConcurrency.ShouldBe(0);

        // After entering
        var scope = asserter.Enter();
        asserter.CurrentConcurrency.ShouldBe(1);
        asserter.MaxConcurrency.ShouldBe(1);

        // After disposing
        scope.Dispose();
        asserter.CurrentConcurrency.ShouldBe(0);
        asserter.MaxConcurrency.ShouldBe(1);
    }

    [Fact]
    public async Task SequentialEnters_ShouldOnlyIncreaseConcurrencyByOne()
    {
        var asserter = new ConcurrencyAsserter();

        // Enter and exit sequentially - max should stay at 1
        for (var i = 0; i < 10; i++)
        {
            using (asserter.Enter())
            {
                await Task.Delay(1, CancellationToken.None);
                asserter.CurrentConcurrency.ShouldBe(1);
                asserter.MaxConcurrency.ShouldBe(1);
            }
        }

        asserter.CurrentConcurrency.ShouldBe(0);
        asserter.MaxConcurrency.ShouldBe(1);
    }

    [Fact]
    public void ResetDuringActiveScope_ShouldResetCountersButNotAffectScope()
    {
        var asserter = new ConcurrencyAsserter();
        var scope = asserter.Enter();

        asserter.CurrentConcurrency.ShouldBe(1);
        asserter.MaxConcurrency.ShouldBe(1);

        // Reset while scope is still active
        asserter.Reset();

        asserter.CurrentConcurrency.ShouldBe(0);
        asserter.MaxConcurrency.ShouldBe(0);

        // Disposing the scope should still decrement
        scope.Dispose();
        asserter.CurrentConcurrency.ShouldBe(-1); // Will go negative since we reset
    }

    [Fact]
    public async Task RapidEnterExit_ShouldMaintainCorrectCounts()
    {
        var asserter = new ConcurrencyAsserter();

        // Rapidly enter and exit without any delays
        var tasks = Enumerable.Range(0, 1000)
            .Select(_ => Task.Run(() =>
            {
                using var scope = asserter.Enter();
                // No delay - immediate exit
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        asserter.CurrentConcurrency.ShouldBe(0);
        asserter.MaxConcurrency.ShouldBeGreaterThan(0);
    }
}
