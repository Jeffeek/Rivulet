using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rivulet.Core;

namespace Rivulet.RetryPolicies.Tests;

public class PolicyExtensionsTests
{
    [Fact]
    public async Task SelectParallelAsync_WithRetryPolicy_ShouldRetryFailedItems()
    {
        var items = Enumerable.Range(1, 5).ToList();
        var attemptCounts = new System.Collections.Concurrent.ConcurrentDictionary<int, int>();

        var policy = new RetryPolicy<int>(maxRetries: 2, baseDelay: TimeSpan.FromMilliseconds(10));

        var results = await items.SelectParallelAsync(
            async (item, ct) =>
            {
                attemptCounts.AddOrUpdate(item, 1, (_, count) => count + 1);

                // Fail on first attempt for even numbers
                if (attemptCounts[item] == 1 && item % 2 == 0)
                {
                    throw new InvalidOperationException();
                }

                await Task.Delay(10, ct);
                return item * 2;
            },
            policy);

        results.Should().HaveCount(5);
        results.Should().Contain([2, 4, 6, 8, 10]);

        // Even numbers should have 2 attempts
        attemptCounts[2].Should().Be(2);
        attemptCounts[4].Should().Be(2);
    }

    [Fact]
    public async Task SelectParallelAsync_WithTimeoutPolicy_ShouldTimeoutSlowOperations()
    {
        var items = Enumerable.Range(1, 3).ToList();
        var policy = new TimeoutPolicy<int>(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await items.SelectParallelAsync(
                async (item, ct) =>
                {
                    await Task.Delay(200, ct);
                    return item * 2;
                },
                policy);
        });
    }

    [Fact]
    public async Task SelectParallelAsync_WithFallbackPolicy_ShouldUseFallbackOnFailure()
    {
        var items = Enumerable.Range(1, 5).ToList();
        var policy = new FallbackPolicy<int>(-1);

        var results = await items.SelectParallelAsync(
            (item, _) =>
            {
                if (item % 2 == 0)
                {
                    throw new InvalidOperationException();
                }
                return ValueTask.FromResult(item * 2);
            },
            policy);

        results.Should().HaveCount(5);
        results.Should().Contain([2, -1, 6, -1, 10]); // Even numbers fall back to -1
    }

    [Fact]
    public async Task SelectParallelAsync_WithCompositePolicy_ShouldApplyAllPolicies()
    {
        var items = Enumerable.Range(1, 3).ToList();
        var attemptCounts = new System.Collections.Concurrent.ConcurrentDictionary<int, int>();

        var retryPolicy = new RetryPolicy<int>(maxRetries: 2, baseDelay: TimeSpan.FromMilliseconds(10));
        var fallbackPolicy = new FallbackPolicy<int>(-1);
        var compositePolicy = new CompositePolicy<int>(fallbackPolicy, retryPolicy);

        var results = await items.SelectParallelAsync(
            (item, _) =>
            {
                attemptCounts.AddOrUpdate(item, 1, (_, count) => count + 1);
                throw new InvalidOperationException();
            },
            compositePolicy);

        results.Should().HaveCount(3);
        results.Should().AllSatisfy(r => r.Should().Be(-1));

        // Should retry 2 times then fall back
        attemptCounts.Values.Should().AllSatisfy(count => count.Should().Be(3));
    }

    [Fact]
    public async Task WrapWith_ShouldCreateCompositePolicy()
    {
        var retryPolicy = new RetryPolicy<int>(maxRetries: 2, baseDelay: TimeSpan.FromMilliseconds(10));
        var fallbackPolicy = new FallbackPolicy<int>(99);

        var compositePolicy = retryPolicy.WrapWith(fallbackPolicy);

        var attemptCount = 0;
        var result = await compositePolicy.ExecuteAsync(_ =>
        {
            attemptCount++;
            throw new InvalidOperationException();
        });

        result.Should().Be(99);
        attemptCount.Should().Be(3); // Initial + 2 retries
    }

    [Fact]
    public async Task SelectParallelAsync_WithNullSource_ShouldThrowArgumentNullException()
    {
        IEnumerable<int> source = null!;
        var policy = new RetryPolicy<int>();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await source.SelectParallelAsync((item, _) => ValueTask.FromResult(item), policy);
        });
    }

    [Fact]
    public async Task SelectParallelAsync_WithNullPolicy_ShouldThrowArgumentNullException()
    {
        var items = Enumerable.Range(1, 5);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await items.SelectParallelAsync((item, _) => ValueTask.FromResult(item), null!);
        });
    }

    [Fact]
    public async Task SelectParallelAsync_WithParallelOptions_ShouldRespectConcurrency()
    {
        var items = Enumerable.Range(1, 20).ToList();
        var maxConcurrent = 0;
        var currentConcurrent = 0;
        var lockObj = new object();

        var policy = new RetryPolicy<int>(maxRetries: 0);

        var results = await items.SelectParallelAsync(
            async (item, ct) =>
            {
                lock (lockObj)
                {
                    currentConcurrent++;
                    maxConcurrent = Math.Max(maxConcurrent, currentConcurrent);
                }

                await Task.Delay(50, ct);

                lock (lockObj)
                {
                    currentConcurrent--;
                }

                return item * 2;
            },
            policy,
            new ParallelOptionsRivulet { MaxDegreeOfParallelism = 5 });

        results.Should().HaveCount(20);
        maxConcurrent.Should().BeLessOrEqualTo(5);
    }
}
