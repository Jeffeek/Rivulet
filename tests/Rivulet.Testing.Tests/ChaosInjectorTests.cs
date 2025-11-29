using System.Diagnostics.CodeAnalysis;

namespace Rivulet.Testing.Tests;

[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public class ChaosInjectorTests
{
    [Fact]
    public async Task ExecuteAsync_WithZeroFailureRate_ShouldNeverFail()
    {
        var injector = new ChaosInjector(failureRate: 0.0);

        for (var i = 0; i < 100; i++)
        {
            var result = await injector.ExecuteAsync(() => Task.FromResult(42));
            result.ShouldBe(42);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithFullFailureRate_ShouldAlwaysThrow()
    {
        var injector = new ChaosInjector(failureRate: 1.0);

        var act = async () => await injector.ExecuteAsync(() => Task.FromResult(42));

        await act.ShouldThrowAsync<ChaosException>();
    }

    [Fact]
    public async Task ExecuteAsync_WithArtificialDelay_ShouldDelay()
    {
        var injector = new ChaosInjector(failureRate: 0.0, artificialDelay: TimeSpan.FromMilliseconds(100));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await injector.ExecuteAsync(() => Task.FromResult(42));
        sw.Stop();

        sw.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(90);
    }

    [Fact]
    public async Task ExecuteAsync_WithDelay_ShouldRespectCancellation()
    {
        var injector = new ChaosInjector(failureRate: 0.0, artificialDelay: TimeSpan.FromSeconds(10));
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var act = async () => await injector.ExecuteAsync(() => Task.FromResult(42), cts.Token);

        await act.ShouldThrowAsync<TaskCanceledException>();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPassThroughActionResult()
    {
        var injector = new ChaosInjector(failureRate: 0.0);

        var result = await injector.ExecuteAsync(() => Task.FromResult("test result"));

        result.ShouldBe("test result");
    }

    [Fact]
    public async Task ExecuteAsync_WithActionThatThrows_ShouldPropagateException()
    {
        var injector = new ChaosInjector(failureRate: 0.0);

        var act = async () => await injector.ExecuteAsync<int>(() => throw new ArgumentException("Action error"));

        var ex = await act.ShouldThrowAsync<ArgumentException>();
        ex.Message.ShouldContain("Action error");
    }

    [Fact]
    public async Task ExecuteAsync_WithStatisticalFailureRate_ShouldMatchExpectedRate()
    {
        const double failureRate = 0.3;
        const int iterations = 1000;
        var injector = new ChaosInjector(failureRate: failureRate);

        var failures = 0;

        for (var i = 0; i < iterations; i++)
        {
            try
            {
                await injector.ExecuteAsync(() => Task.FromResult(42));
            }
            catch (ChaosException)
            {
                failures++;
            }
        }

        var actualRate = (double)failures / iterations;
        actualRate.ShouldBe(failureRate, 0.1);
    }

    [Fact]
    public async Task ExecuteAsync_WithZeroDelay_ShouldExecuteImmediately()
    {
        var injector = new ChaosInjector(failureRate: 0.0, artificialDelay: TimeSpan.Zero);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await injector.ExecuteAsync(() => Task.FromResult(42));
        sw.Stop();

        sw.ElapsedMilliseconds.ShouldBeLessThan(50);
    }

    [Fact]
    public void Constructor_WithInvalidFailureRate_ShouldThrow()
    {
        var act = () => new ChaosInjector(failureRate: -0.1);

        act.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithFailureRateGreaterThanOne_ShouldThrow()
    {
        var act = () => new ChaosInjector(failureRate: 1.1);

        act.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task ConcurrentCalls_ShouldAllWork()
    {
        var injector = new ChaosInjector(failureRate: 0.1);

        var tasks = Enumerable.Range(1, 100)
            .Select(async i =>
            {
                try
                {
                    return await injector.ExecuteAsync(() => Task.FromResult(i));
                }
                catch (ChaosException)
                {
                    return -1;
                }
            })
            .ToArray();

        var results = await Task.WhenAll(tasks);

        results.Count().ShouldBe(100);
        results.Where(r => r > 0).ShouldNotBeEmpty();
        results.Where(r => r == -1).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ShouldFail_WithZeroRate_ShouldAlwaysReturnFalse()
    {
        var injector = new ChaosInjector(failureRate: 0.0);

        for (var i = 0; i < 100; i++)
        {
            injector.ShouldFail().ShouldBeFalse();
        }

        await Task.CompletedTask;
    }

    [Fact]
    public async Task ShouldFail_WithFullRate_ShouldAlwaysReturnTrue()
    {
        var injector = new ChaosInjector(failureRate: 1.0);

        for (var i = 0; i < 100; i++)
        {
            injector.ShouldFail().ShouldBeTrue();
        }

        await Task.CompletedTask;
    }

    [Fact]
    public async Task ShouldFail_WithMidRate_ShouldReturnMixedResults()
    {
        var injector = new ChaosInjector(failureRate: 0.5);

        var results = Enumerable.Range(0, 1000)
            .Select(_ => injector.ShouldFail())
            .ToList();

        var trueCount = results.Count(r => r);
        var falseCount = results.Count(r => !r);

        trueCount.ShouldBeGreaterThan(0);
        falseCount.ShouldBeGreaterThan(0);
        var actualRate = (double)trueCount / results.Count;
        actualRate.ShouldBe(0.5, 0.1);

        await Task.CompletedTask;
    }

    [Fact]
    public async Task ChaosException_ShouldHaveCorrectMessage()
    {
        var injector = new ChaosInjector(failureRate: 1.0);

        try
        {
            await injector.ExecuteAsync(() => Task.FromResult(42));
            Assert.Fail("Should have thrown");
        }
        catch (ChaosException ex)
        {
            ex.Message.ShouldBe("Chaos injected failure");
        }
    }

    [Fact]
    public async Task MultipleInjectors_ShouldBeIndependent()
    {
        var injector1 = new ChaosInjector(failureRate: 0.0);
        var injector2 = new ChaosInjector(failureRate: 1.0);

        var result1 = await injector1.ExecuteAsync(() => Task.FromResult(1));
        result1.ShouldBe(1);

        var act2 = async () => await injector2.ExecuteAsync(() => Task.FromResult(2));
        await act2.ShouldThrowAsync<ChaosException>();
    }
}
