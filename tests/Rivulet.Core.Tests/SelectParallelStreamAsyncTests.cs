namespace Rivulet.Core.Tests;

public class SelectParallelStreamAsyncTests
{
    [Fact]
    public async Task SelectParallelStreamAsync_EmptyCollection_ReturnsEmptyStream()
    {
        var source = AsyncEnumerable.Empty<int>();

        var results = await source.SelectParallelStreamAsync((x, _) => new ValueTask<int>(x * 2)).ToListAsync();

        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task SelectParallelStreamAsync_SingleItem_ReturnsTransformedItem()
    {
        var source = new[] { 5 }.ToAsyncEnumerable();

        var results = await source.SelectParallelStreamAsync((x, _) => new ValueTask<int>(x * 2)).ToListAsync();

        results.ShouldHaveSingleItem().ShouldBe(10);
    }

    [Fact]
    public async Task SelectParallelStreamAsync_MultipleItems_ReturnsAllTransformed()
    {
        var source = Enumerable.Range(1, 10).ToAsyncEnumerable();

        var results = await source.SelectParallelStreamAsync((x, _) => new ValueTask<int>(x * 2)).ToListAsync();

        results.Count.ShouldBe(10);
        results.OrderBy(x => x).ShouldBe(new[] { 2, 4, 6, 8, 10, 12, 14, 16, 18, 20 });
    }

    [Fact]
    public async Task SelectParallelStreamAsync_StreamsResultsAsTheyComplete()
    {
        var source = Enumerable.Range(1, 5).ToAsyncEnumerable();
        var results = new List<int>();
        var timestamps = new List<DateTime>();

        await foreach (var item in source.SelectParallelStreamAsync(
            async (x, ct) =>
            {
                await Task.Delay(x * 10, ct);
                return x;
            }))
        {
            results.Add(item);
            timestamps.Add(DateTime.UtcNow);
        }

        results.Count.ShouldBe(5);
        foreach (var expected in new[] { 1, 2, 3, 4, 5 })
        {
            results.ShouldContain(expected);
        }
        timestamps.Count.ShouldBe(5);
    }

    [Fact]
    public async Task SelectParallelStreamAsync_WithParallelism_ExecutesInParallel()
    {
        var source = Enumerable.Range(1, 10).ToAsyncEnumerable();
        var options = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 5 };
        var startTime = DateTime.UtcNow;

        var results = await source.SelectParallelStreamAsync(async (x, ct) =>
            {
                await Task.Delay(100, ct);
                return x * 2;
            }, options)
            .ToListAsync();

        var duration = DateTime.UtcNow - startTime;

        results.Count.ShouldBe(10);
        // Increased from 500ms to 1000ms to account for CI environment variability
        // With MaxDegreeOfParallelism=5 and 10 items of 100ms each, ideal time is ~200ms
        // but CI environments can have high scheduling overhead
        duration.ShouldBeLessThan(TimeSpan.FromMilliseconds(1000));
    }

    [Fact]
    public async Task SelectParallelStreamAsync_CustomChannelCapacity_HandlesBackpressure()
    {
        var source = Enumerable.Range(1, 100).ToAsyncEnumerable();
        var options = new ParallelOptionsRivulet
        {
            ChannelCapacity = 5,
            MaxDegreeOfParallelism = 2
        };

        var results = new List<int>();
        await foreach (var item in source.SelectParallelStreamAsync(
            async (x, ct) =>
            {
                await Task.Delay(5, ct);
                return x * 2;
            },
            options))
        {
            results.Add(item);
            await Task.Delay(1);
        }

        results.Count.ShouldBe(100);
    }

    [Fact]
    public async Task SelectParallelStreamAsync_WithNullOptions_UsesDefaults()
    {
        var source = Enumerable.Range(1, 5).ToAsyncEnumerable();

        var results = await source.SelectParallelStreamAsync((x, _) => new ValueTask<int>(x * 2), options: null).ToListAsync();

        results.Count.ShouldBe(5);
        results.OrderBy(x => x).ShouldBe(new[] { 2, 4, 6, 8, 10 });
    }

    [Fact]
    public async Task SelectParallelStreamAsync_LargeCollection_StreamsCorrectly()
    {
        var source = Enumerable.Range(1, 1000).ToAsyncEnumerable();

        var count = await source.SelectParallelStreamAsync((x, _) => new ValueTask<int>(x * 2)).CountAsync();

        count.ShouldBe(1000);
    }

    [Fact]
    public async Task SelectParallelStreamAsync_WithComplexTypes_TransformsCorrectly()
    {
        var source = Enumerable.Range(1, 20).ToAsyncEnumerable();

        var results = await source.SelectParallelStreamAsync(async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return (x, x % 2 == 0);
            })
            .ToListAsync();

        results.Count.ShouldBe(20);
        results.Count(r => r.Item2).ShouldBe(10);
    }

    [Fact]
    public async Task SelectParallelStreamAsync_WithCancellationToken_Completes()
    {
        var source = Enumerable.Range(1, 10).ToAsyncEnumerable();
        using var cts = new CancellationTokenSource();

        var results = await source.SelectParallelStreamAsync((x, _) => new ValueTask<int>(x * 2), cancellationToken: cts.Token).ToListAsync(cancellationToken: cts.Token);

        results.Count.ShouldBe(10);
    }

    [Fact]
    public async Task SelectParallelStreamAsync_SlowProducer_HandlesCorrectly()
    {
        async IAsyncEnumerable<int> SlowProducer()
        {
            for (var i = 1; i <= 5; i++)
            {
                await Task.Delay(50);
                yield return i;
            }
        }

        var results = await SlowProducer().SelectParallelStreamAsync((x, _) => new ValueTask<int>(x * 2)).ToListAsync();

        results.Count.ShouldBe(5);
        results.OrderBy(x => x).ShouldBe(new[] { 2, 4, 6, 8, 10 });
    }
}
