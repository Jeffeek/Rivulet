using FluentAssertions;
using Polly;

namespace Rivulet.Polly.Tests;

public class PollyParallelExtensionsTests
{
    [Fact]
    public async Task SelectParallelWithPolicyAsync_AppliesRetryPolicy_RetriesTransientFailures()
    {
        var attempts = new Dictionary<int, int>();
        var items = Enumerable.Range(1, 5).ToList();

        var retryPolicy = new ResiliencePipelineBuilder()
            .AddRetry(new()
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(10),
                BackoffType = DelayBackoffType.Constant
            })
            .Build();

        var results = await items.SelectParallelWithPolicyAsync(
            async (item, ct) =>
            {
                lock (attempts)
                {
                    attempts.TryGetValue(item, out var count);
                    attempts[item] = count + 1;
                }

                // Fail first 2 attempts for item 3
                if (item != 3 || attempts[item] > 2)
                    return item * 2;

                await Task.Delay(1, ct).ConfigureAwait(false);
                throw new InvalidOperationException($"Transient failure for item {item}");
            },
            retryPolicy,
            new() { MaxDegreeOfParallelism = 2 });

        results.Should().HaveCount(5);
        results.Should().BeEquivalentTo([2, 4, 6, 8, 10]);
        attempts[3].Should().Be(3, "item 3 should have been attempted 3 times");
    }

    [Fact]
    public async Task SelectParallelWithPolicyAsync_ResiliencePipelineT_AppliesResultBasedRetry()
    {
        var attempts = new Dictionary<int, int>();
        var items = Enumerable.Range(1, 5).ToList();

        var retryPolicy = new ResiliencePipelineBuilder<int>()
            .AddRetry(new()
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(10),
                BackoffType = DelayBackoffType.Constant,
                ShouldHandle = new PredicateBuilder<int>()
                    .Handle<Exception>()
                    .HandleResult(result => result == -1) // Retry when result is -1
            })
            .Build();

        var results = await items.SelectParallelWithPolicyAsync(
            async (item, ct) =>
            {
                lock (attempts)
                {
                    attempts.TryGetValue(item, out var count);
                    attempts[item] = count + 1;
                }

                await Task.Delay(1, ct).ConfigureAwait(false);

                // Return -1 on first 2 attempts for item 3
                if (item == 3 && attempts[item] <= 2)
                {
                    return -1;
                }

                return item * 2;
            },
            retryPolicy,
            new() { MaxDegreeOfParallelism = 2 });

        results.Should().HaveCount(5);
        results.Should().BeEquivalentTo([2, 4, 6, 8, 10]);
        attempts[3].Should().Be(3, "item 3 should have been attempted 3 times");
    }

    [Fact]
    public async Task SelectParallelWithPolicyAsync_WithTimeout_AppliesToItems()
    {
        var items = Enumerable.Range(1, 5).ToList();

        var timeoutPolicy = new ResiliencePipelineBuilder()
            .AddTimeout(TimeSpan.FromSeconds(5))  // Generous timeout to avoid flakiness
            .Build();

        // Just verify it works with timeout policy
        var results = await items.SelectParallelWithPolicyAsync(
            async (item, ct) =>
            {
                await Task.Delay(10, ct).ConfigureAwait(false);
                return item * 2;
            },
            timeoutPolicy,
            new() { MaxDegreeOfParallelism = 2 });

        results.Should().HaveCount(5);
        results.Should().BeEquivalentTo([2, 4, 6, 8, 10]);
    }

    [Fact]
    public async Task SelectParallelWithPolicyAsync_NullSource_ThrowsArgumentNullException()
    {
        IEnumerable<int>? source = null;
        var policy = ResiliencePipeline.Empty;

        var act = async () => await source!.SelectParallelWithPolicyAsync(
            (item, _) => ValueTask.FromResult(item),
            policy);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("source");
    }

    [Fact]
    public async Task SelectParallelWithPolicyAsync_NullSelector_ThrowsArgumentNullException()
    {
        var source = Enumerable.Range(1, 5);
        var policy = ResiliencePipeline.Empty;

        var act = async () => await source.SelectParallelWithPolicyAsync<int, int>(
            null!,
            policy);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("selector");
    }

    [Fact]
    public async Task SelectParallelWithPolicyAsync_NullPolicy_ThrowsArgumentNullException()
    {
        var source = Enumerable.Range(1, 5);

        var act = async () => await source.SelectParallelWithPolicyAsync(
            (item, _) => ValueTask.FromResult(item),
            (ResiliencePipeline)null!);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("policy");
    }

    [Fact]
    public async Task ForEachParallelWithPolicyAsync_ExecutesSideEffectsWithPolicy()
    {
        var items = Enumerable.Range(1, 10).ToList();
        var results = new List<int>();
        var attempts = new Dictionary<int, int>();

        var retryPolicy = new ResiliencePipelineBuilder()
            .AddRetry(new()
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromMilliseconds(10),
                BackoffType = DelayBackoffType.Constant
            })
            .Build();

        await items.ForEachParallelWithPolicyAsync(
            async (item, ct) =>
            {
                lock (attempts)
                {
                    attempts.TryGetValue(item, out var count);
                    attempts[item] = count + 1;
                }

                // Fail first attempt for item 5
                if (item == 5 && attempts[item] == 1)
                {
                    await Task.Delay(1, ct).ConfigureAwait(false);
                    throw new InvalidOperationException($"Transient failure for item {item}");
                }

                await Task.Delay(1, ct).ConfigureAwait(false);

                lock (results)
                {
                    results.Add(item);
                }
            },
            retryPolicy,
            new() { MaxDegreeOfParallelism = 4 });

        results.Should().HaveCount(10);
        results.Should().BeEquivalentTo(items);
        attempts[5].Should().Be(2, "item 5 should have been attempted twice");
    }

    [Fact]
    public async Task ForEachParallelWithPolicyAsync_NullSource_ThrowsArgumentNullException()
    {
        IEnumerable<int>? source = null;
        var policy = ResiliencePipeline.Empty;

        var act = async () => await source!.ForEachParallelWithPolicyAsync(
            (_, _) => ValueTask.CompletedTask,
            policy);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("source");
    }

    [Fact]
    public async Task ForEachParallelWithPolicyAsync_NullAction_ThrowsArgumentNullException()
    {
        var source = Enumerable.Range(1, 5);
        var policy = ResiliencePipeline.Empty;

        var act = async () => await source.ForEachParallelWithPolicyAsync(
            null!,
            policy);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("action");
    }

    [Fact]
    public async Task ForEachParallelWithPolicyAsync_NullPolicy_ThrowsArgumentNullException()
    {
        var source = Enumerable.Range(1, 5);

        var act = async () => await source.ForEachParallelWithPolicyAsync(
            (_, _) => ValueTask.CompletedTask,
            null!);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("policy");
    }
}
