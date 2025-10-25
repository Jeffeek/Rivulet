using FluentAssertions;

namespace Rivulet.Core.Tests;

public class SelectParallelAsyncTests
{
    [Fact]
    public async Task SelectParallelAsync_EmptyCollection_ReturnsEmptyList()
    {
        var source = Enumerable.Empty<int>();

        var results = await source.SelectParallelAsync(
            (x, _) => new ValueTask<int>(x * 2));

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SelectParallelAsync_SingleItem_ReturnsTransformedItem()
    {
        var source = new[] { 5 };

        var results = await source.SelectParallelAsync(
            (x, _) => new ValueTask<int>(x * 2));

        results.Should().ContainSingle().Which.Should().Be(10);
    }

    [Fact]
    public async Task SelectParallelAsync_MultipleItems_ReturnsAllTransformed()
    {
        var source = Enumerable.Range(1, 10);

        var results = await source.SelectParallelAsync(
            (x, _) => new ValueTask<int>(x * 2));

        results.Should().HaveCount(10);
        results.OrderBy(x => x).Should().BeEquivalentTo([2, 4, 6, 8, 10, 12, 14, 16, 18, 20]);
    }

    [Fact]
    public async Task SelectParallelAsync_LargeCollection_ProcessesAllItems()
    {
        var source = Enumerable.Range(1, 1000);

        var results = await source.SelectParallelAsync(
            (x, _) => new ValueTask<int>(x * 2));

        results.Should().HaveCount(1000);
        results.Sum().Should().Be(1001000);
    }

    [Fact]
    public async Task SelectParallelAsync_WithDelay_ExecutesInParallel()
    {
        var source = Enumerable.Range(1, 10);
        var options = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 5 };
        var startTime = DateTime.UtcNow;

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(100, ct);
                return x * 2;
            },
            options);

        var duration = DateTime.UtcNow - startTime;

        results.Should().HaveCount(10);
        duration.Should().BeLessThan(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public async Task SelectParallelAsync_CustomMaxDegreeOfParallelism_RespectsLimit()
    {
        var source = Enumerable.Range(1, 20);
        var concurrentCount = 0;
        var maxConcurrent = 0;
        var lockObj = new object();
        var options = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 3 };

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                lock (lockObj)
                {
                    concurrentCount++;
                    maxConcurrent = Math.Max(maxConcurrent, concurrentCount);
                }

                await Task.Delay(50, ct);

                lock (lockObj)
                {
                    concurrentCount--;
                }

                return x;
            },
            options);

        results.Should().HaveCount(20);
        maxConcurrent.Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public async Task SelectParallelAsync_WithNullOptions_UsesDefaults()
    {
        var source = Enumerable.Range(1, 5);

        var results = await source.SelectParallelAsync(
            (x, _) => new ValueTask<int>(x * 2),
            options: null);

        results.Should().HaveCount(5);
        results.Should().BeEquivalentTo([2, 4, 6, 8, 10]);
    }

    [Fact]
    public async Task SelectParallelAsync_CustomChannelCapacity_HandlesBackpressure()
    {
        var source = Enumerable.Range(1, 100);
        var options = new ParallelOptionsRivulet
        {
            ChannelCapacity = 10,
            MaxDegreeOfParallelism = 2
        };

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x * 2;
            },
            options);

        results.Should().HaveCount(100);
    }

    [Fact]
    public async Task SelectParallelAsync_WithComplexTransformation_ProducesCorrectResults()
    {
        var source = Enumerable.Range(1, 50);

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return new { Original = x, Squared = x * x, IsEven = x % 2 == 0 };
            });

        results.Should().HaveCount(50);
        results.Where(r => r.IsEven).Should().HaveCount(25);
        results.First(r => r.Original == 5).Squared.Should().Be(25);
    }

    [Fact]
    public async Task SelectParallelAsync_CancellationToken_DefaultValue_Completes()
    {
        var source = Enumerable.Range(1, 10);

        var results = await source.SelectParallelAsync(
            (x, _) => new ValueTask<int>(x * 2));

        results.Should().HaveCount(10);
    }

    [Fact]
    public async Task SelectParallelAsync_WithStrings_TransformsCorrectly()
    {
        var source = new[] { "hello", "world", "test", "xunit" };

        var results = await source.SelectParallelAsync(
            (s, _) => new ValueTask<string>(s.ToUpper()));

        results.Should().BeEquivalentTo(new[] { "HELLO", "WORLD", "TEST", "XUNIT" });
    }
}
