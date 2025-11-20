using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Rivulet.RetryPolicies.Tests;

public class TimeoutPolicyTests
{
    [Fact]
    public async Task ExecuteAsync_WithinTimeout_ShouldReturnResult()
    {
        var policy = new TimeoutPolicy<int>(TimeSpan.FromSeconds(1));

        var result = await policy.ExecuteAsync(async ct =>
        {
            await Task.Delay(10, ct);
            return 42;
        });

        result.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsync_ExceedingTimeout_ShouldThrowTimeoutException()
    {
        var policy = new TimeoutPolicy<int>(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await policy.ExecuteAsync(async ct =>
            {
                await Task.Delay(200, ct);
                return 42;
            });
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithOnTimeoutCallback_ShouldInvokeCallback()
    {
        var timeoutInvoked = false;
        TimeSpan? capturedTimeout = null;

        var policy = new TimeoutPolicy<int>(
            TimeSpan.FromMilliseconds(50),
            timeout =>
            {
                timeoutInvoked = true;
                capturedTimeout = timeout;
                return ValueTask.CompletedTask;
            });

        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await policy.ExecuteAsync(async ct =>
            {
                await Task.Delay(200, ct);
                return 42;
            });
        });

        timeoutInvoked.Should().BeTrue();
        capturedTimeout.Should().Be(TimeSpan.FromMilliseconds(50));
    }

    [Fact]
    public async Task ExecuteAsync_WithExternalCancellation_ShouldThrowOperationCanceledException()
    {
        var policy = new TimeoutPolicy<int>(TimeSpan.FromSeconds(10));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await policy.ExecuteAsync(async ct =>
            {
                await Task.Delay(100, ct);
                return 42;
            }, cts.Token);
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithNullOperation_ShouldThrowArgumentNullException()
    {
        var policy = new TimeoutPolicy<int>(TimeSpan.FromSeconds(1));

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await policy.ExecuteAsync(null!);
        });
    }

    [Fact]
    public void Constructor_WithZeroTimeout_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimeoutPolicy<int>(TimeSpan.Zero));
    }

    [Fact]
    public void Constructor_WithNegativeTimeout_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimeoutPolicy<int>(TimeSpan.FromMilliseconds(-1)));
    }

    [Fact]
    public async Task Wrap_ShouldCreateCompositePolicy()
    {
        var timeoutPolicy = new TimeoutPolicy<int>(TimeSpan.FromMilliseconds(100));
        var fallbackPolicy = new FallbackPolicy<int>(99);

        var compositePolicy = timeoutPolicy.Wrap(fallbackPolicy);

        var result = await compositePolicy.ExecuteAsync(async ct =>
        {
            await Task.Delay(200, ct);
            return 42;
        });

        result.Should().Be(99); // Should fall back after timeout
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleQuickOperations_ShouldAllComplete()
    {
        var policy = new TimeoutPolicy<int>(TimeSpan.FromMilliseconds(500));
        var results = new List<int>();

        for (int i = 0; i < 10; i++)
        {
            var result = await policy.ExecuteAsync(async ct =>
            {
                await Task.Delay(10, ct);
                return i;
            });
            results.Add(result);
        }

        results.Should().HaveCount(10);
        results.Should().Equal([0, 1, 2, 3, 4, 5, 6, 7, 8, 9]);
    }
}
