using System;
using System.Threading.Tasks;

namespace Rivulet.RetryPolicies.Tests;

public class CompositePolicyTests
{
    [Fact]
    public async Task ExecuteAsync_WithTimeoutAndRetry_ShouldApplyBothPolicies()
    {
        var retryPolicy = new RetryPolicy<int>(maxRetries: 2, baseDelay: TimeSpan.FromMilliseconds(10));
        var timeoutPolicy = new TimeoutPolicy<int>(TimeSpan.FromMilliseconds(100));

        var compositePolicy = new CompositePolicy<int>(timeoutPolicy, retryPolicy);

        var attemptCount = 0;
        var result = await compositePolicy.ExecuteAsync(async ct =>
        {
            attemptCount++;
            if (attemptCount < 2)
            {
                throw new InvalidOperationException();
            }
            await Task.Delay(10, ct);
            return 42;
        });

        result.Should().Be(42);
        attemptCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_WithRetryAndFallback_ShouldRetryThenFallback()
    {
        var retryPolicy = new RetryPolicy<int>(maxRetries: 2, baseDelay: TimeSpan.FromMilliseconds(10));
        var fallbackPolicy = new FallbackPolicy<int>(99);

        var compositePolicy = new CompositePolicy<int>(fallbackPolicy, retryPolicy);

        var attemptCount = 0;
        var result = await compositePolicy.ExecuteAsync(_ =>
        {
            attemptCount++;
            throw new InvalidOperationException();
        });

        result.Should().Be(99); // Should fall back after retries exhausted
        attemptCount.Should().Be(3); // Initial + 2 retries
    }

    [Fact]
    public async Task ExecuteAsync_WithRetryAndFallback_ShouldApplyBothPolicies()
    {
        var retryPolicy = new RetryPolicy<int>(maxRetries: 2, baseDelay: TimeSpan.FromMilliseconds(10));
        var fallbackPolicy = new FallbackPolicy<int>(99);

        // Fallback wraps retry
        var compositePolicy = new CompositePolicy<int>(fallbackPolicy, retryPolicy);

        var attemptCount = 0;
        var result = await compositePolicy.ExecuteAsync(_ =>
        {
            attemptCount++;
            throw new InvalidOperationException();
        });

        result.Should().Be(99);
        attemptCount.Should().Be(3); // Initial + 2 retries, then fallback
    }

    [Fact]
    public async Task ExecuteAsync_WithNullOperation_ShouldThrowArgumentNullException()
    {
        var policy1 = new RetryPolicy<int>();
        var policy2 = new TimeoutPolicy<int>(TimeSpan.FromSeconds(1));
        var compositePolicy = new CompositePolicy<int>(policy1, policy2);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await compositePolicy.ExecuteAsync(null!);
        });
    }

    [Fact]
    public void Constructor_WithNullOuterPolicy_ShouldThrowArgumentNullException()
    {
        var innerPolicy = new RetryPolicy<int>();

        Assert.Throws<ArgumentNullException>(() =>
            new CompositePolicy<int>(null!, innerPolicy));
    }

    [Fact]
    public void Constructor_WithNullInnerPolicy_ShouldThrowArgumentNullException()
    {
        var outerPolicy = new RetryPolicy<int>();

        Assert.Throws<ArgumentNullException>(() =>
            new CompositePolicy<int>(outerPolicy, null!));
    }

    [Fact]
    public async Task Wrap_ShouldCreateNestedCompositePolicy()
    {
        var policy1 = new RetryPolicy<int>(maxRetries: 1, baseDelay: TimeSpan.FromMilliseconds(10));
        var policy2 = new TimeoutPolicy<int>(TimeSpan.FromMilliseconds(100));
        var policy3 = new FallbackPolicy<int>(99);

        var compositePolicy = policy1.Wrap(policy2).Wrap(policy3);

        var attemptCount = 0;
        var result = await compositePolicy.ExecuteAsync(_ =>
        {
            attemptCount++;
            throw new InvalidOperationException();
        });

        result.Should().Be(99);
        attemptCount.Should().Be(2); // Initial + 1 retry
    }

    [Fact]
    public async Task ExecuteAsync_WithSuccessfulOperation_ShouldReturnResultWithoutInvokingPolicies()
    {
        var retryInvoked = false;
        var retryPolicy = new RetryPolicy<int>(
            maxRetries: 2,
            onRetry: (_, _) =>
            {
                retryInvoked = true;
                return ValueTask.CompletedTask;
            });

        var timeoutInvoked = false;
        var timeoutPolicy = new TimeoutPolicy<int>(
            TimeSpan.FromSeconds(1),
            _ =>
            {
                timeoutInvoked = true;
                return ValueTask.CompletedTask;
            });

        var compositePolicy = new CompositePolicy<int>(timeoutPolicy, retryPolicy);

        var result = await compositePolicy.ExecuteAsync(_ => ValueTask.FromResult(42));

        result.Should().Be(42);
        retryInvoked.Should().BeFalse();
        timeoutInvoked.Should().BeFalse();
    }
}
