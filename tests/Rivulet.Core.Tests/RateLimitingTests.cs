using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Rivulet.Core.Resilience;

namespace Rivulet.Core.Tests;

[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public sealed class RateLimitingTests
{
    [Fact]
    public async Task RateLimit_EnforcesTokensPerSecond()
    {
        // Process 150 items with burst=50, rate=100/sec
        // First 50 use burst capacity (instant), next 100 limited by rate
        var source = Enumerable.Range(1, 150);
        var sw = Stopwatch.StartNew();

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 50,
            RateLimit = new()
            {
                TokensPerSecond = 100, // 100 ops/sec
                BurstCapacity = 50     // Can burst 50
            }
        };

        var results = await source.SelectParallelAsync(
            static async (x, ct) =>
            {
                await Task.Delay(1, ct); // Minimal work
                return x * 2;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        sw.Stop();

        results.Count.ShouldBe(150);
        // First 50 burst immediately, remaining 100 at 100/sec = 1 second minimum
        // Total should be at least 1 second
        sw.Elapsed.ShouldBeGreaterThan(TimeSpan.FromMilliseconds(900));
        sw.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(3)); // Upper bound for sanity
    }

    [Fact]
    public async Task RateLimit_AllowsBurstCapacity()
    {
        var source = Enumerable.Range(1, 20);
        var sw = Stopwatch.StartNew();

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 20,
            RateLimit = new()
            {
                TokensPerSecond = 10, // Low rate
                BurstCapacity = 20    // But allow burst of 20
            }
        };

        var results = await source.SelectParallelAsync(
            static async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x * 2;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        sw.Stop();

        results.Count.ShouldBe(20);
        // First 20 items should process quickly due to burst capacity
        // Should complete faster than if rate-limited from start
        sw.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task RateLimit_WithTokensPerOperation()
    {
        // 20 ops * 5 tokens/op = 100 tokens needed
        // Burst = 25 tokens (5 ops can burst)
        // Remaining 75 tokens at 50/sec = 1.5 seconds
        var source = Enumerable.Range(1, 20);
        var sw = Stopwatch.StartNew();

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 20,
            RateLimit = new()
            {
                TokensPerSecond = 50, BurstCapacity = 25, TokensPerOperation = 5 // Each operation costs 5 tokens
            }
        };

        var results = await source.SelectParallelAsync(
            static async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x * 2;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        sw.Stop();

        results.Count.ShouldBe(20);
        // Should take at least 1.4 seconds (allowing for CI/CD variance)
        sw.Elapsed.ShouldBeGreaterThan(TimeSpan.FromMilliseconds(1400));
        sw.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task RateLimit_WithStreamingOperations()
    {
        // 80 items, burst=30, rate=100/sec
        // First 30 burst, remaining 50 at 100/sec = 0.5 sec minimum
        var source = AsyncEnumerable.Range(1, 80);
        var sw = Stopwatch.StartNew();

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 30, RateLimit = new() { TokensPerSecond = 100, BurstCapacity = 30 }
        };

        var count = await source.SelectParallelStreamAsync(static async (x, ct) =>
                {
                    await Task.Delay(1, ct);
                    return x * 2;
                },
                options)
            .CountAsync();

        sw.Stop();

        count.ShouldBe(80);
        sw.Elapsed.ShouldBeGreaterThan(TimeSpan.FromMilliseconds(450));
        sw.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task RateLimit_WithoutRateLimitOption_NoThrottling()
    {
        var source = Enumerable.Range(1, 50);
        var sw = Stopwatch.StartNew();

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 50
            // No RateLimit option
        };

        var results = await source.SelectParallelAsync(
            static async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x * 2;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        sw.Stop();

        results.Count.ShouldBe(50);
        // Without rate limiting, should complete very quickly
        sw.Elapsed.ShouldBeLessThan(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void TokenBucket_Constructor_ThrowsOnNullOptions()
    {
        var act = static () => new TokenBucket(null!);
        act.ShouldThrow<ArgumentNullException>().Message.ShouldContain("options");
    }

    [Fact]
    public void TokenBucket_Constructor_ValidatesOptions()
    {
        var act = static () => new TokenBucket(new() { TokensPerSecond = 0 });
        act.ShouldThrow<ArgumentException>().Message.ShouldContain("TokensPerSecond");
    }

    [Fact]
    public void RateLimitOptions_Validation_ThrowsOnInvalidTokensPerSecond()
    {
        var act = static () => new RateLimitOptions { TokensPerSecond = 0 }.Validate();
        act.ShouldThrow<ArgumentException>().Message.ShouldContain("TokensPerSecond");
    }

    [Fact]
    public void RateLimitOptions_Validation_ThrowsOnInvalidBurstCapacity()
    {
        var act = static () => new RateLimitOptions { BurstCapacity = 0 }.Validate();
        act.ShouldThrow<ArgumentException>().Message.ShouldContain("BurstCapacity");
    }

    [Fact]
    public void RateLimitOptions_Validation_ThrowsOnInvalidTokensPerOperation()
    {
        var act = static () => new RateLimitOptions { TokensPerOperation = 0 }.Validate();
        act.ShouldThrow<ArgumentException>().Message.ShouldContain("TokensPerOperation");
    }

    [Fact]
    public void RateLimitOptions_Validation_ThrowsWhenBurstLessThanTokensPerOperation()
    {
        var act = static () => new RateLimitOptions { BurstCapacity = 5, TokensPerOperation = 10 }.Validate();

        act.ShouldThrow<ArgumentException>().Message.ShouldContain("BurstCapacity");
    }

    [Fact]
    public async Task RateLimit_WithCancellation_CancelsCorrectly()
    {
        var source = Enumerable.Range(1, 100);
        using var cts = new CancellationTokenSource();

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            RateLimit = new()
            {
                TokensPerSecond = 10, // Very slow
                BurstCapacity = 10
            }
        };

        var processedCount = 0;

        var task = source.SelectParallelAsync(
            async (x, ct) =>
            {
                Interlocked.Increment(ref processedCount);
                if (processedCount >= 15) await cts.CancelAsync();

                await Task.Delay(1, ct);
                return x * 2;
            },
            options,
            cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);

        processedCount.ShouldBeLessThan(100);
    }

    [Fact]
    public async Task RateLimit_WithRetries_AppliesRateLimitToRetries()
    {
        // 15 items, each fails once then succeeds = 30 total operations
        // Burst = 20, remaining 10 at 50/sec = 0.2 sec minimum
        var source = Enumerable.Range(1, 15);
        var attempts = new ConcurrentDictionary<int, int>();
        var sw = Stopwatch.StartNew();

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 15,
            MaxRetries = 2,
            IsTransient = static ex => ex is InvalidOperationException,
            RateLimit = new() { TokensPerSecond = 50, BurstCapacity = 20 }
        };

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(1, ct);
                var attemptCount = attempts.AddOrUpdate(x, 1, static (_, count) => count + 1);

                // Fail first attempt
                return attemptCount < 2 ? throw new InvalidOperationException("Transient") : x * 2;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        sw.Stop();

        results.Count.ShouldBe(15);
        // Should take at least 0.1 seconds for the rate-limited portion (with tolerance for timing)
        sw.Elapsed.ShouldBeGreaterThan(TimeSpan.FromMilliseconds(100));
        sw.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task RateLimit_WithOrderedOutput_MaintainsOrder()
    {
        var source = Enumerable.Range(1, 20);

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 10, OrderedOutput = true,
            RateLimit = new() { TokensPerSecond = 100, BurstCapacity = 100 }
        };

        var results = await source.SelectParallelAsync(
            static async (x, ct) =>
            {
                await Task.Delay(Random.Shared.Next(1, 10), ct);
                return x * 2;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Count.ShouldBe(20);
        results.ShouldBeInOrder();
        results.ShouldBe(Enumerable.Range(1, 20).Select(static x => x * 2));
    }

    [Fact]
    public async Task RateLimit_HighThroughput_HandlesLargeVolume()
    {
        // 1200 items, burst=500, rate=1000/sec
        // First 500 burst, remaining 700 at 1000/sec = 0.7 sec minimum
        var source = Enumerable.Range(1, 1200);
        var sw = Stopwatch.StartNew();

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 64,
            RateLimit = new()
            {
                TokensPerSecond = 1000, // High rate
                BurstCapacity = 500
            }
        };

        var results = await source.SelectParallelAsync(
            static async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x * 2;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        sw.Stop();

        results.Count.ShouldBe(1200);
        // Should take at least 0.6 seconds
        sw.Elapsed.ShouldBeGreaterThan(TimeSpan.FromMilliseconds(600));
        sw.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task RateLimit_LowRate_EnforcesStrictLimiting()
    {
        // 12 items, burst=3, rate=5/sec
        // First 3 burst, remaining 9 at 5/sec = 1.8 sec minimum
        var source = Enumerable.Range(1, 12);
        var sw = Stopwatch.StartNew();

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 12,
            RateLimit = new()
            {
                TokensPerSecond = 5, // Very low rate: 5 ops/sec
                BurstCapacity = 3
            }
        };

        var results = await source.SelectParallelAsync(
            static async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x * 2;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        sw.Stop();

        results.Count.ShouldBe(12);
        // Should take at least 1.7 seconds
        sw.Elapsed.ShouldBeGreaterThan(TimeSpan.FromMilliseconds(1700));
        sw.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(4));
    }

    [Fact]
    public async Task RateLimit_WithErrorModes_AppliesCorrectly()
    {
        var source = Enumerable.Range(1, 20);

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 10,
            ErrorMode = ErrorMode.BestEffort,
            RateLimit = new() { TokensPerSecond = 100, BurstCapacity = 100 }
        };

        var results = await source.SelectParallelAsync(
            static async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x % 5 == 0 ? throw new InvalidOperationException("Error") : x * 2;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        // Should get 16 results (20 - 4 failures)
        results.Count.ShouldBe(16);
    }

    [Fact]
    public void TokenBucket_TryAcquire_ReturnsTrue_WhenTokensAvailable()
    {
        var bucket = new TokenBucket(new() { TokensPerSecond = 100, BurstCapacity = 10, TokensPerOperation = 1 });

        var result = bucket.TryAcquire();
        result.ShouldBeTrue();
    }

    [Fact]
    public void TokenBucket_TryAcquire_ReturnsFalse_WhenTokensExhausted()
    {
        var bucket = new TokenBucket(new() { TokensPerSecond = 10, BurstCapacity = 2, TokensPerOperation = 1 });

        bucket.TryAcquire().ShouldBeTrue();
        bucket.TryAcquire().ShouldBeTrue();

        var result = bucket.TryAcquire();
        result.ShouldBeFalse();
    }

    [Fact]
    public void TokenBucket_GetAvailableTokens_ReturnsCorrectValue()
    {
        var bucket = new TokenBucket(new() { TokensPerSecond = 100, BurstCapacity = 50, TokensPerOperation = 5 });

        var tokens = bucket.GetAvailableTokens();
        tokens.ShouldBe(50);
    }

    [Fact]
    public async Task TokenBucket_GetAvailableTokens_RefillsOverTime()
    {
        var bucket = new TokenBucket(new() { TokensPerSecond = 100, BurstCapacity = 10, TokensPerOperation = 1 });

        // Exhaust bucket
        for (var i = 0; i < 10; i++) bucket.TryAcquire().ShouldBeTrue();

        bucket.GetAvailableTokens().ShouldBeLessThan(1.1);

        // Wait for refill
        await Task.Delay(200, CancellationToken.None); // 200ms should add ~20 tokens (100 tokens/sec = 0.1 tokens/ms)

        var tokens = bucket.GetAvailableTokens();
        tokens.ShouldBeGreaterThan(5);
        tokens.ShouldBeLessThanOrEqualTo(10);
    }

    [Fact]
    public void TokenBucket_TryAcquire_WithMultipleTokensPerOperation()
    {
        var bucket = new TokenBucket(new() { TokensPerSecond = 100, BurstCapacity = 10, TokensPerOperation = 5 });

        // Should succeed with 10 tokens available and 5 needed
        bucket.TryAcquire().ShouldBeTrue();

        // Should succeed again (10 - 5 = 5 remaining)
        bucket.TryAcquire().ShouldBeTrue();

        // Should fail now (0 tokens remaining)
        bucket.TryAcquire().ShouldBeFalse();
    }

    [Fact]
    public void TokenBucket_RapidCalls_HandlesZeroElapsedTime()
    {
        var bucket = new TokenBucket(new() { TokensPerSecond = 1000, BurstCapacity = 100, TokensPerOperation = 1 });

        // Make rapid successive calls to try to hit elapsedTicks <= 0 case
        // GetAvailableTokens() calls RefillTokens() internally
        for (var i = 0; i < 1000; i++) _ = bucket.GetAvailableTokens();

        // Should not crash and should maintain valid state
        bucket.GetAvailableTokens().ShouldBeLessThanOrEqualTo(100);
        bucket.GetAvailableTokens().ShouldBeGreaterThanOrEqualTo(0);
    }
}
