using System;
using System.Threading.Tasks;
using Rivulet.Core.Resilience;

namespace Rivulet.RetryPolicies.Tests;

public class PolicyBuilderTests
{
    [Fact]
    public async Task RetryBuilder_WithDefaults_ShouldCreatePolicy()
    {
        var policy = PolicyBuilder<int>.Retry().Build();

        var attemptCount = 0;
        var result = await policy.ExecuteAsync(_ =>
        {
            attemptCount++;
            if (attemptCount < 3)
            {
                throw new InvalidOperationException();
            }
            return ValueTask.FromResult(42);
        });

        result.Should().Be(42);
        attemptCount.Should().Be(3);
    }

    [Fact]
    public async Task RetryBuilder_WithMaxRetries_ShouldRespectLimit()
    {
        var policy = PolicyBuilder<int>.Retry()
            .WithMaxRetries(5)
            .WithBaseDelay(TimeSpan.FromMilliseconds(5))
            .Build();

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
        attemptCount.Should().Be(4);
    }

    [Fact]
    public async Task RetryBuilder_WithBackoffStrategy_ShouldUseStrategy()
    {
        var retryCount = 0;

        var policy = PolicyBuilder<int>.Retry()
            .WithMaxRetries(3)
            .WithBaseDelay(TimeSpan.FromMilliseconds(10))
            .WithBackoffStrategy(BackoffStrategy.Linear)
            .OnRetry((_, _) =>
            {
                retryCount++;
                return ValueTask.CompletedTask;
            })
            .Build();

        var attemptCount = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await policy.ExecuteAsync(_ =>
            {
                attemptCount++;
                throw new InvalidOperationException();
            });
        });

        retryCount.Should().Be(3);
        attemptCount.Should().Be(4);
    }

    [Fact]
    public async Task RetryBuilder_WithHandle_ShouldOnlyRetrySpecifiedExceptions()
    {
        var policy = PolicyBuilder<int>.Retry()
            .WithMaxRetries(3)
            .WithBaseDelay(TimeSpan.FromMilliseconds(10))
            .Handle<InvalidOperationException>()
            .Build();

        var attemptCount = 0;
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await policy.ExecuteAsync(_ =>
            {
                attemptCount++;
                throw new ArgumentException();
            });
        });

        attemptCount.Should().Be(1); // No retries for non-matching exception
    }

    [Fact]
    public async Task RetryBuilder_WithHandlePredicate_ShouldFilterExceptions()
    {
        var policy = PolicyBuilder<int>.Retry()
            .WithMaxRetries(3)
            .WithBaseDelay(TimeSpan.FromMilliseconds(10))
            .Handle<InvalidOperationException>(ex => ex.Message.Contains("Retryable"))
            .Build();

        var attemptCount = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await policy.ExecuteAsync(_ =>
            {
                attemptCount++;
                throw new InvalidOperationException("Not retryable");
            });
        });

        attemptCount.Should().Be(1); // No retries for filtered exception
    }

    [Fact]
    public async Task RetryBuilder_WithOnRetry_ShouldInvokeCallback()
    {
        var retryCount = 0;

        var policy = PolicyBuilder<int>.Retry()
            .WithMaxRetries(3)
            .WithBaseDelay(TimeSpan.FromMilliseconds(10))
            .OnRetry((_, _) =>
            {
                retryCount++;
                return ValueTask.CompletedTask;
            })
            .Build();

        var attemptCount = 0;
        var result = await policy.ExecuteAsync(_ =>
        {
            attemptCount++;
            if (attemptCount < 3)
            {
                throw new InvalidOperationException();
            }
            return ValueTask.FromResult(42);
        });

        result.Should().Be(42);
        retryCount.Should().Be(2);
    }

    [Fact]
    public async Task TimeoutBuilder_ShouldCreatePolicy()
    {
        var policy = PolicyBuilder<int>.Timeout(TimeSpan.FromMilliseconds(100))
            .Build();

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
    public async Task TimeoutBuilder_WithOnTimeout_ShouldInvokeCallback()
    {
        var timeoutInvoked = false;

        var policy = PolicyBuilder<int>.Timeout(TimeSpan.FromMilliseconds(50))
            .OnTimeout(_ =>
            {
                timeoutInvoked = true;
                return ValueTask.CompletedTask;
            })
            .Build();

        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await policy.ExecuteAsync(async ct =>
            {
                await Task.Delay(200, ct);
                return 42;
            });
        });

        timeoutInvoked.Should().BeTrue();
    }

    [Fact]
    public async Task FallbackBuilder_WithStaticValue_ShouldCreatePolicy()
    {
        var policy = PolicyBuilder<int>.Fallback(99).Build();

        var result = await policy.ExecuteAsync(_ => throw new InvalidOperationException());

        result.Should().Be(99);
    }

    [Fact]
    public async Task FallbackBuilder_WithDynamicFunc_ShouldCreatePolicy()
    {
        var policy = PolicyBuilder<int>.Fallback((ex, _) => ValueTask.FromResult(ex.Message.Length))
            .Build();

        var result = await policy.ExecuteAsync(_ => throw new InvalidOperationException("Error"));

        result.Should().Be(5); // Length of "Error"
    }

    [Fact]
    public async Task FallbackBuilder_WithHandle_ShouldFilterExceptions()
    {
        var policy = PolicyBuilder<int>.Fallback(99)
            .Handle<InvalidOperationException>()
            .Build();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await policy.ExecuteAsync(_ => throw new ArgumentException());
        });
    }

    [Fact]
    public async Task FallbackBuilder_WithOnFallback_ShouldInvokeCallback()
    {
        var fallbackInvoked = false;

        var policy = PolicyBuilder<int>.Fallback(99)
            .OnFallback(_ =>
            {
                fallbackInvoked = true;
                return ValueTask.CompletedTask;
            })
            .Build();

        var result = await policy.ExecuteAsync(_ => throw new InvalidOperationException());

        result.Should().Be(99);
        fallbackInvoked.Should().BeTrue();
    }

    [Fact]
    public void RetryBuilder_WithNegativeMaxRetries_ShouldThrowArgumentOutOfRangeException()
    {
        var builder = PolicyBuilder<int>.Retry();

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithMaxRetries(-1));
    }

    [Fact]
    public void RetryBuilder_WithNegativeBaseDelay_ShouldThrowArgumentOutOfRangeException()
    {
        var builder = PolicyBuilder<int>.Retry();

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithBaseDelay(TimeSpan.FromMilliseconds(-1)));
    }

    [Fact]
    public void TimeoutBuilder_WithZeroTimeout_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PolicyBuilder<int>.Timeout(TimeSpan.Zero));
    }
}
