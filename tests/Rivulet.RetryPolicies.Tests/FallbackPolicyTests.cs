using System;
using System.Threading.Tasks;

namespace Rivulet.RetryPolicies.Tests;

public class FallbackPolicyTests
{
    [Fact]
    public async Task ExecuteAsync_WithSuccessfulOperation_ShouldReturnResult()
    {
        var policy = new FallbackPolicy<int>(99);

        var result = await policy.ExecuteAsync(_ => ValueTask.FromResult(42));

        result.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsync_WithFailure_ShouldReturnFallbackValue()
    {
        var policy = new FallbackPolicy<int>(99);

        var result = await policy.ExecuteAsync(_ => throw new InvalidOperationException("Error"));

        result.Should().Be(99);
    }

    [Fact]
    public async Task ExecuteAsync_WithDynamicFallback_ShouldInvokeFallbackFunction()
    {
        var policy = new FallbackPolicy<int>((ex, _) => ValueTask.FromResult(ex.Message.Length));

        var result = await policy.ExecuteAsync(_ => throw new InvalidOperationException("TestError"));

        result.Should().Be(9); // Length of "TestError"
    }

    [Fact]
    public async Task ExecuteAsync_WithShouldFallbackPredicate_ShouldOnlyFallbackOnMatchingExceptions()
    {
        var policy = new FallbackPolicy<int>(
            fallbackValue: 99,
            shouldFallback: ex => ex is InvalidOperationException);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await policy.ExecuteAsync(_ => throw new ArgumentException("Non-fallback error"));
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithOnFallbackCallback_ShouldInvokeCallback()
    {
        var fallbackInvoked = false;
        Exception? capturedException = null;

        var policy = new FallbackPolicy<int>(
            fallbackValue: 99,
            onFallback: ex =>
            {
                fallbackInvoked = true;
                capturedException = ex;
                return ValueTask.CompletedTask;
            });

        var result = await policy.ExecuteAsync(_ => throw new InvalidOperationException("Test error"));

        result.Should().Be(99);
        fallbackInvoked.Should().BeTrue();
        capturedException.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task ExecuteAsync_WithNullOperation_ShouldThrowArgumentNullException()
    {
        var policy = new FallbackPolicy<int>(99);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await policy.ExecuteAsync(null!);
        });
    }

    [Fact]
    public void Constructor_WithNullFallbackFunc_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new FallbackPolicy<int>(null!));
    }

    [Fact]
    public async Task Wrap_ShouldCreateCompositePolicy()
    {
        var fallbackPolicy = new FallbackPolicy<int>(99);
        var timeoutPolicy = new TimeoutPolicy<int>(TimeSpan.FromMilliseconds(50));

        var compositePolicy = fallbackPolicy.Wrap(timeoutPolicy);

        var result = await compositePolicy.ExecuteAsync(async ct =>
        {
            await Task.Delay(200, ct);
            return 42;
        });

        result.Should().Be(99); // Should fall back after timeout
    }

    [Fact]
    public async Task ExecuteAsync_WithAsyncFallback_ShouldWork()
    {
        var policy = new FallbackPolicy<int>(async (_, ct) =>
        {
            await Task.Delay(10, ct);
            return 100;
        });

        var result = await policy.ExecuteAsync(_ => throw new InvalidOperationException());

        result.Should().Be(100);
    }

    [Fact]
    public async Task ExecuteAsync_WithReferenceType_ShouldReturnFallbackInstance()
    {
        var fallbackValue = new { Value = 99 };
        var policy = new FallbackPolicy<object>(fallbackValue);

        var result = await policy.ExecuteAsync(_ => throw new InvalidOperationException());

        result.Should().BeSameAs(fallbackValue);
    }
}
