using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Rivulet.Core.Resilience;

namespace Rivulet.RetryPolicies.Tests;

public class RetryPolicyTests
{
    [Fact]
    public async Task ExecuteAsync_WithSuccessfulOperation_ShouldReturnResult()
    {
        var policy = new RetryPolicy<int>(maxRetries: 3);

        var result = await policy.ExecuteAsync(_ => ValueTask.FromResult(42));

        result.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsync_WithTransientFailure_ShouldRetryAndSucceed()
    {
        var attemptCount = 0;
        var policy = new RetryPolicy<int>(maxRetries: 3, baseDelay: TimeSpan.FromMilliseconds(10));

        var result = await policy.ExecuteAsync(_ =>
        {
            attemptCount++;
            if (attemptCount < 3)
            {
                throw new InvalidOperationException("Transient error");
            }
            return ValueTask.FromResult(42);
        });

        result.Should().Be(42);
        attemptCount.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_WithPermanentFailure_ShouldThrowAfterMaxRetries()
    {
        var attemptCount = 0;
        var policy = new RetryPolicy<int>(maxRetries: 3, baseDelay: TimeSpan.FromMilliseconds(10));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await policy.ExecuteAsync(_ =>
            {
                attemptCount++;
                throw new InvalidOperationException("Permanent error");
            });
        });

        attemptCount.Should().Be(4); // Initial attempt + 3 retries
    }

    [Fact]
    public async Task ExecuteAsync_WithShouldRetryPredicate_ShouldOnlyRetryMatchingExceptions()
    {
        var attemptCount = 0;
        var policy = new RetryPolicy<int>(
            maxRetries: 3,
            baseDelay: TimeSpan.FromMilliseconds(10),
            shouldRetry: ex => ex is InvalidOperationException);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await policy.ExecuteAsync(_ =>
            {
                attemptCount++;
                throw new ArgumentException("Non-retryable error");
            });
        });

        attemptCount.Should().Be(1); // Only initial attempt, no retries
    }

    [Fact]
    public async Task ExecuteAsync_WithOnRetryCallback_ShouldInvokeCallback()
    {
        var retryAttempts = new List<int>();
        var retryExceptions = new List<Exception>();

        var policy = new RetryPolicy<int>(
            maxRetries: 3,
            baseDelay: TimeSpan.FromMilliseconds(10),
            onRetry: (attempt, ex) =>
            {
                retryAttempts.Add(attempt);
                retryExceptions.Add(ex);
                return ValueTask.CompletedTask;
            });

        var attemptCount = 0;
        var result = await policy.ExecuteAsync(_ =>
        {
            attemptCount++;
            if (attemptCount < 3)
            {
                throw new InvalidOperationException($"Error {attemptCount}");
            }
            return ValueTask.FromResult(42);
        });

        result.Should().Be(42);
        retryAttempts.Should().Equal([1, 2]);
        retryExceptions.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_WithExponentialBackoff_ShouldRetryWithBackoff()
    {
        var retryCount = 0;

        var policy = new RetryPolicy<int>(
            maxRetries: 3,
            baseDelay: TimeSpan.FromMilliseconds(10),
            backoffStrategy: BackoffStrategy.Exponential,
            onRetry: (_, _) =>
            {
                retryCount++;
                return ValueTask.CompletedTask;
            });

        var attemptCount = 0;
        var result = await policy.ExecuteAsync(_ =>
        {
            attemptCount++;
            if (attemptCount < 4)
            {
                throw new InvalidOperationException();
            }
            return ValueTask.FromResult(42);
        });

        result.Should().Be(42);
        retryCount.Should().Be(3);
        attemptCount.Should().Be(4);
    }

    [Fact]
    public async Task ExecuteAsync_WithLinearBackoff_ShouldRetryWithLinearDelay()
    {
        var retryCount = 0;

        var policy = new RetryPolicy<int>(
            maxRetries: 3,
            baseDelay: TimeSpan.FromMilliseconds(10),
            backoffStrategy: BackoffStrategy.Linear,
            onRetry: (_, _) =>
            {
                retryCount++;
                return ValueTask.CompletedTask;
            });

        var attemptCount = 0;
        var result = await policy.ExecuteAsync(_ =>
        {
            attemptCount++;
            if (attemptCount < 4)
            {
                throw new InvalidOperationException();
            }
            return ValueTask.FromResult(42);
        });

        result.Should().Be(42);
        retryCount.Should().Be(3);
        attemptCount.Should().Be(4);
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_ShouldThrowOperationCanceledException()
    {
        var policy = new RetryPolicy<int>(maxRetries: 3);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await policy.ExecuteAsync(_ => ValueTask.FromResult(42), cts.Token);
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithNullOperation_ShouldThrowArgumentNullException()
    {
        var policy = new RetryPolicy<int>();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await policy.ExecuteAsync(null!);
        });
    }

    [Fact]
    public void Constructor_WithNegativeMaxRetries_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryPolicy<int>(maxRetries: -1));
    }

    [Fact]
    public async Task Wrap_ShouldCreateCompositePolicy()
    {
        var retryPolicy = new RetryPolicy<int>(maxRetries: 2, baseDelay: TimeSpan.FromMilliseconds(10));
        var fallbackPolicy = new FallbackPolicy<int>(99);

        var compositePolicy = retryPolicy.Wrap(fallbackPolicy);

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
    public async Task ExecuteAsync_WithExponentialJitter_ShouldAddRandomness()
    {
        var delays = new List<TimeSpan>();
        var lastTime = DateTime.UtcNow;

        var policy = new RetryPolicy<int>(
            maxRetries: 10,
            baseDelay: TimeSpan.FromMilliseconds(50),
            backoffStrategy: BackoffStrategy.ExponentialJitter,
            onRetry: (_, _) =>
            {
                var now = DateTime.UtcNow;
                delays.Add(now - lastTime);
                lastTime = now;
                return ValueTask.CompletedTask;
            });

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await policy.ExecuteAsync(_ => throw new InvalidOperationException());
        });

        delays.Should().HaveCount(10);
        // With jitter, delays should be random within exponential bounds
        delays[0].TotalMilliseconds.Should().BeLessThan(100);
        delays[9].TotalMilliseconds.Should().BeLessThan(50 * Math.Pow(2, 9)); // Should be within max bound
    }
}
