using FluentAssertions;

namespace Rivulet.Testing.Tests;

public class ConcurrencyAsserterTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithZeroValues()
    {
        var asserter = new ConcurrencyAsserter();

        asserter.CurrentConcurrency.Should().Be(0);
        asserter.MaxConcurrency.Should().Be(0);
    }

    [Fact]
    public void EnterAsync_ShouldIncrementCurrentConcurrency()
    {
        var asserter = new ConcurrencyAsserter();

        using var scope = asserter.Enter();

        asserter.CurrentConcurrency.Should().Be(1);
        asserter.MaxConcurrency.Should().Be(1);
    }

    [Fact]
    public void DisposingScope_ShouldDecrementCurrentConcurrency()
    {
        var asserter = new ConcurrencyAsserter();

        var scope = asserter.Enter();
        scope.Dispose();

        asserter.CurrentConcurrency.Should().Be(0);
        asserter.MaxConcurrency.Should().Be(1);
    }

    [Fact]
    public void MultipleEnters_ShouldTrackMaxConcurrency()
    {
        var asserter = new ConcurrencyAsserter();

        var scope1 = asserter.Enter();
        var scope2 = asserter.Enter();
        var scope3 = asserter.Enter();

        asserter.CurrentConcurrency.Should().Be(3);
        asserter.MaxConcurrency.Should().Be(3);

        scope1.Dispose();
        scope2.Dispose();

        asserter.CurrentConcurrency.Should().Be(1);
        asserter.MaxConcurrency.Should().Be(3);

        scope3.Dispose();
    }

    [Fact]
    public async Task ConcurrentOperations_ShouldTrackCorrectly()
    {
        var asserter = new ConcurrencyAsserter();

        var tasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            using var scope = asserter.Enter();
            await Task.Delay(50);
        }).ToArray();

        await Task.WhenAll(tasks);

        asserter.CurrentConcurrency.Should().Be(0);
        asserter.MaxConcurrency.Should().BeGreaterThan(1);
    }

    [Fact]
    public void Reset_ShouldResetCounters()
    {
        var asserter = new ConcurrencyAsserter();

        var scope1 = asserter.Enter();
        asserter.Enter();
        scope1.Dispose();

        asserter.Reset();

        asserter.CurrentConcurrency.Should().Be(0);
        asserter.MaxConcurrency.Should().Be(0);
    }

    [Fact]
    public async Task StressTest_ManyThreads_ShouldMaintainAccuracy()
    {
        var asserter = new ConcurrencyAsserter();

        var tasks = Enumerable.Range(0, 1000).Select(async _ =>
        {
            using var scope = asserter.Enter();
            await Task.Delay(1);
        }).ToArray();

        await Task.WhenAll(tasks);

        asserter.CurrentConcurrency.Should().Be(0);
        asserter.MaxConcurrency.Should().BeGreaterThan(0);
    }

    [Fact]
    public void MaxConcurrency_ShouldOnlyIncrease()
    {
        var asserter = new ConcurrencyAsserter();

        var scope1 = asserter.Enter();
        var scope2 = asserter.Enter();
        var scope3 = asserter.Enter();

        asserter.MaxConcurrency.Should().Be(3);

        scope1.Dispose();
        scope2.Dispose();

        asserter.MaxConcurrency.Should().Be(3);

        var scope4 = asserter.Enter();

        asserter.MaxConcurrency.Should().Be(3);

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

        asserter.CurrentConcurrency.Should().Be(0);
    }

    [Fact]
    public async Task ParallelExecutions_ShouldTrackConcurrency()
    {
        var asserter = new ConcurrencyAsserter();
        var concurrentExecutions = 0;
        var maxObserved = 0;
        var lockObj = new object();

        var tasks = Enumerable.Range(0, 50).Select(async _ =>
        {
            using var scope = asserter.Enter();

            lock (lockObj)
            {
                concurrentExecutions++;
                if (concurrentExecutions > maxObserved)
                    maxObserved = concurrentExecutions;
            }

            await Task.Delay(10);

            lock (lockObj)
            {
                concurrentExecutions--;
            }
        }).ToArray();

        await Task.WhenAll(tasks);

        asserter.CurrentConcurrency.Should().Be(0);
        asserter.MaxConcurrency.Should().BeGreaterThan(1);
        asserter.MaxConcurrency.Should().Be(maxObserved);
    }

    [Fact]
    public void UsingStatement_ShouldAutomaticallyDisposeScope()
    {
        var asserter = new ConcurrencyAsserter();

        using (asserter.Enter())
        {
            asserter.CurrentConcurrency.Should().Be(1);
        }

        asserter.CurrentConcurrency.Should().Be(0);
    }

    [Fact]
    public void NestedScopes_ShouldTrackCorrectly()
    {
        var asserter = new ConcurrencyAsserter();

        using (asserter.Enter())
        {
            asserter.CurrentConcurrency.Should().Be(1);

            using (asserter.Enter())
            {
                asserter.CurrentConcurrency.Should().Be(2);

                using (asserter.Enter())
                {
                    asserter.CurrentConcurrency.Should().Be(3);
                }

                asserter.CurrentConcurrency.Should().Be(2);
            }

            asserter.CurrentConcurrency.Should().Be(1);
        }

        asserter.CurrentConcurrency.Should().Be(0);
        asserter.MaxConcurrency.Should().Be(3);
    }
}
