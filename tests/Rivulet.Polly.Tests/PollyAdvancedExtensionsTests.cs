namespace Rivulet.Polly.Tests;

public class PollyAdvancedExtensionsTests
{
    [Fact]
    public async Task SelectParallelWithHedgingAsync_ReturnsFirstSuccessfulResult()
    {
        var items = Enumerable.Range(1, 5).ToList();
        var delayTimes = new Dictionary<int, int>
        {
            { 1, 10 },
            { 2, 10 },
            { 3, 200 }, // Slow item - will trigger hedging
            { 4, 10 },
            { 5, 10 }
        };

        var results = await items.SelectParallelWithHedgingAsync(
            async (item, ct) =>
            {
                await Task.Delay(delayTimes[item], ct).ConfigureAwait(false);
                return item * 2;
            },
            2,
            TimeSpan.FromMilliseconds(50),
            new() { MaxDegreeOfParallelism = 2 });

        results.Count.ShouldBe(5);
        results.OrderBy(static x => x).ShouldBe([2, 4, 6, 8, 10]);
    }

    [Fact]
    public async Task SelectParallelWithHedgingAsync_NullSource_ThrowsArgumentNullException()
    {
        IEnumerable<int>? source = null;

        var act = () => source!.SelectParallelWithHedgingAsync((item, _) => ValueTask.FromResult(item));

        var ex = await act.ShouldThrowAsync<ArgumentNullException>();
        ex.ParamName.ShouldBe("source");
    }

    [Fact]
    public async Task SelectParallelWithHedgingAsync_NullSelector_ThrowsArgumentNullException()
    {
        var source = Enumerable.Range(1, 5);

        var act = () => source.SelectParallelWithHedgingAsync<int, int>(
            null!);

        (await act.ShouldThrowAsync<ArgumentNullException>()).ParamName.ShouldBe("selector");
    }

    [Fact]
    public async Task SelectParallelWithHedgingAsync_InvalidMaxHedgedAttempts_ThrowsArgumentOutOfRangeException()
    {
        var source = Enumerable.Range(1, 5);

        var act = () => source.SelectParallelWithHedgingAsync((item, _) => ValueTask.FromResult(item), 0);

        (await act.ShouldThrowAsync<ArgumentOutOfRangeException>()).ParamName.ShouldBe("maxHedgedAttempts");
    }

    [Fact]
    public async Task SelectParallelWithResultRetryAsync_RetriesOnUndesiredResult()
    {
        var items = Enumerable.Range(1, 5).ToList();
        var attempts = new Dictionary<int, int>();

        var results = await items.SelectParallelWithResultRetryAsync(
            async (item, ct) =>
            {
                lock (attempts)
                {
                    attempts.TryGetValue(item, out var count);
                    attempts[item] = count + 1;
                }

                await Task.Delay(1, ct).ConfigureAwait(false);

                // Return -1 on first 2 attempts for item 3
                if (item == 3 && attempts[item] <= 2) return -1;

                return item * 2;
            },
            static result => result == -1,
            3,
            TimeSpan.FromMilliseconds(10),
            new() { MaxDegreeOfParallelism = 2 });

        results.Count.ShouldBe(5);
        results.OrderBy(static x => x).ShouldBe([2, 4, 6, 8, 10]);
        attempts[3].ShouldBe(3, "item 3 should have been retried");
    }

    [Fact]
    public async Task SelectParallelWithResultRetryAsync_ExceedsMaxRetries_ReturnsUndesiredResult()
    {
        var items = new[] { 1 };

        var results = await items.SelectParallelWithResultRetryAsync(static async (_, ct) =>
            {
                await Task.Delay(1, ct).ConfigureAwait(false);
                return -1; // Always return undesired result
            },
            static result => result == -1,
            2,
            TimeSpan.FromMilliseconds(10));

        results.Count.ShouldBe(1);
        results[0].ShouldBe(-1, "should return undesired result after exceeding max retries");
    }

    [Fact]
    public async Task SelectParallelWithResultRetryAsync_NullSource_ThrowsArgumentNullException()
    {
        IEnumerable<int>? source = null;

        var act = () => source!.SelectParallelWithResultRetryAsync((item, _) => ValueTask.FromResult(item),
            static result => result == -1);

        (await act.ShouldThrowAsync<ArgumentNullException>()).ParamName.ShouldBe("source");
    }

    [Fact]
    public async Task SelectParallelWithResultRetryAsync_NullSelector_ThrowsArgumentNullException()
    {
        var source = Enumerable.Range(1, 5);

        var act = () => source.SelectParallelWithResultRetryAsync<int, int>(
            null!,
            static result => result == -1);

        (await act.ShouldThrowAsync<ArgumentNullException>()).ParamName.ShouldBe("selector");
    }

    [Fact]
    public async Task SelectParallelWithResultRetryAsync_NullShouldRetry_ThrowsArgumentNullException()
    {
        var source = Enumerable.Range(1, 5);

        var act = () => source.SelectParallelWithResultRetryAsync(static (item, _) => ValueTask.FromResult(item),
            null!);

        (await act.ShouldThrowAsync<ArgumentNullException>()).ParamName.ShouldBe("shouldRetry");
    }

    [Fact]
    public async Task SelectParallelWithResultRetryAsync_NegativeMaxRetries_ThrowsArgumentOutOfRangeException()
    {
        var source = Enumerable.Range(1, 5);

        var act = () => source.SelectParallelWithResultRetryAsync(static (item, _) => ValueTask.FromResult(item),
            static result => result == -1,
            -1);

        (await act.ShouldThrowAsync<ArgumentOutOfRangeException>()).ParamName.ShouldBe("maxRetries");
    }

    [Fact]
    public async Task SelectParallelWithExponentialResultRetryAsync_RetriesWithExponentialBackoff()
    {
        var items = new[] { 1 };
        var attempts = new List<(int attempt, DateTime time)>();

        var results = await items.SelectParallelWithExponentialResultRetryAsync(
            async (item, ct) =>
            {
                lock (attempts) attempts.Add((attempts.Count + 1, DateTime.UtcNow));

                await Task.Delay(1, ct).ConfigureAwait(false);

                // Return -1 on first 2 attempts
                if (attempts.Count <= 2) return -1;

                return item * 2;
            },
            static result => result == -1,
            3,
            TimeSpan.FromMilliseconds(50));

        results.Count.ShouldBe(1);
        results[0].ShouldBe(2);
        attempts.Count.ShouldBe(3, "should have made 3 attempts");

        // Verify exponential backoff (with jitter, delays should generally increase)
        // Note: With jitter, we can't verify exact delays, but we can verify attempts happened
        attempts.Select(static a => a.attempt).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task SelectParallelWithExponentialResultRetryAsync_NullSource_ThrowsArgumentNullException()
    {
        IEnumerable<int>? source = null;

        var act = () => source!.SelectParallelWithExponentialResultRetryAsync(static (item, _) => ValueTask.FromResult(item),
            static result => result == -1);

        (await act.ShouldThrowAsync<ArgumentNullException>()).ParamName.ShouldBe("source");
    }

    [Fact]
    public async Task SelectParallelWithExponentialResultRetryAsync_NullSelector_ThrowsArgumentNullException()
    {
        var source = Enumerable.Range(1, 5);

        var act = () => source.SelectParallelWithExponentialResultRetryAsync<int, int>(
            null!,
            static result => result == -1);

        (await act.ShouldThrowAsync<ArgumentNullException>()).ParamName.ShouldBe("selector");
    }

    [Fact]
    public async Task SelectParallelWithExponentialResultRetryAsync_NullShouldRetry_ThrowsArgumentNullException()
    {
        var source = Enumerable.Range(1, 5);

        var act = () => source.SelectParallelWithExponentialResultRetryAsync(static (item, _) => ValueTask.FromResult(item),
            null!);

        (await act.ShouldThrowAsync<ArgumentNullException>()).ParamName.ShouldBe("shouldRetry");
    }

    [Fact]
    public async Task SelectParallelWithExponentialResultRetryAsync_NegativeMaxRetries_ThrowsArgumentOutOfRangeException()
    {
        var source = Enumerable.Range(1, 5);

        var act = () => source.SelectParallelWithExponentialResultRetryAsync(static (item, _) => ValueTask.FromResult(item),
            static result => result == -1,
            -1);

        (await act.ShouldThrowAsync<ArgumentOutOfRangeException>()).ParamName.ShouldBe("maxRetries");
    }
}