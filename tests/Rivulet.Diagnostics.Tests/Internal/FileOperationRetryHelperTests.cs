using System.Diagnostics.CodeAnalysis;
using Rivulet.Diagnostics.Internal;

namespace Rivulet.Diagnostics.Tests.Internal;

[SuppressMessage("ReSharper", "ArgumentsStyleLiteral")]
public sealed class FileOperationRetryHelperTests
{
    [Fact]
    public void ExecuteWithRetry_WithNullOperation_ShouldThrow() =>
        Should.Throw<ArgumentNullException>(static () =>
            FileOperationRetryHelper.ExecuteWithRetry(null!));

    [Fact]
    public void ExecuteWithRetry_WithZeroRetries_ShouldThrow() =>
        Should.Throw<ArgumentOutOfRangeException>(static () =>
            FileOperationRetryHelper.ExecuteWithRetry(static () => { }, retries: 0));

    [Fact]
    public void ExecuteWithRetry_WithNegativeRetries_ShouldThrow() =>
        Should.Throw<ArgumentOutOfRangeException>(static () =>
            FileOperationRetryHelper.ExecuteWithRetry(static () => { }, retries: -1));

    [Fact]
    public void ExecuteWithRetry_WithNegativeDelay_ShouldThrow() =>
        Should.Throw<ArgumentOutOfRangeException>(static () =>
            FileOperationRetryHelper.ExecuteWithRetry(static () => { }, delayMilliseconds: -1));

    [Fact]
    public void ExecuteWithRetry_WithSuccessfulOperation_ShouldExecuteOnce()
    {
        var executionCount = 0;

        FileOperationRetryHelper.ExecuteWithRetry(() => executionCount++);

        executionCount.ShouldBe(1);
    }

    [Fact]
    public void ExecuteWithRetry_WithTransientIOException_ShouldRetryAndSucceed()
    {
        var executionCount = 0;

        FileOperationRetryHelper.ExecuteWithRetry(() =>
            {
                executionCount++;
                if (executionCount < 2)
                    throw new IOException("Transient failure");
            },
            retries: 3,
            delayMilliseconds: 1);

        executionCount.ShouldBe(2);
    }

    [Fact]
    public void ExecuteWithRetry_WithPersistentIOException_ShouldExhaustRetriesAndThrow()
    {
        var executionCount = 0;

        Should.Throw<IOException>(() =>
            FileOperationRetryHelper.ExecuteWithRetry(() =>
                {
                    executionCount++;
                    throw new IOException("Persistent failure");
                },
                retries: 3,
                delayMilliseconds: 1));

        executionCount.ShouldBe(3);
    }

    [Fact]
    public void ExecuteWithRetry_WithNonIOException_ShouldNotRetry()
    {
        var executionCount = 0;

        Should.Throw<InvalidOperationException>(() =>
            FileOperationRetryHelper.ExecuteWithRetry(() =>
                {
                    executionCount++;
                    throw new InvalidOperationException("Non-IO exception");
                },
                retries: 3,
                delayMilliseconds: 1));

        executionCount.ShouldBe(1);
    }

    [Fact]
    public void ExecuteWithRetry_WithCustomRetryCount_ShouldRespectRetries()
    {
        var executionCount = 0;

        Should.Throw<IOException>(() =>
            FileOperationRetryHelper.ExecuteWithRetry(() =>
                {
                    executionCount++;
                    throw new IOException("Always fails");
                },
                retries: 5,
                delayMilliseconds: 1));

        executionCount.ShouldBe(5);
    }

    [Fact]
    public void ExecuteWithRetry_WithZeroDelay_ShouldWorkWithoutWaiting()
    {
        var executionCount = 0;

        FileOperationRetryHelper.ExecuteWithRetry(() =>
            {
                executionCount++;
                if (executionCount < 2)
                    throw new IOException("Transient failure");
            },
            retries: 3,
            delayMilliseconds: 0);

        executionCount.ShouldBe(2);
    }

    [Fact]
    public void ExecuteWithRetry_WithDefaultParameters_ShouldUseDefaults()
    {
        var executionCount = 0;

        FileOperationRetryHelper.ExecuteWithRetry(() =>
        {
            executionCount++;
            if (executionCount < 3)
                throw new IOException("Fails twice");
        });

        // Default is 3 retries, so this should succeed on attempt 3
        executionCount.ShouldBe(3);
    }

    [Fact]
    public void ExecuteWithRetry_WithDelayParameter_ShouldWaitBetweenRetries()
    {
        var executionCount = 0;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        FileOperationRetryHelper.ExecuteWithRetry(() =>
            {
                executionCount++;
                if (executionCount < 3)
                    throw new IOException("Fails twice");
            },
            retries: 3,
            delayMilliseconds: 50);

        stopwatch.Stop();

        executionCount.ShouldBe(3);
        // Should have waited at least 100ms (2 retries * 50ms each)
        stopwatch.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(100);
    }

    [Fact]
    public void ExecuteWithRetry_WithSuccessOnLastAttempt_ShouldSucceed()
    {
        var executionCount = 0;
        const int maxRetries = 3;

        FileOperationRetryHelper.ExecuteWithRetry(() =>
            {
                executionCount++;
                if (executionCount < maxRetries)
                    throw new IOException($"Attempt {executionCount} failed");
            },
            // ReSharper disable once ArgumentsStyleNamedExpression
            retries: maxRetries,
            delayMilliseconds: 1);

        executionCount.ShouldBe(maxRetries);
    }

    [Fact]
    public void ExecuteWithRetry_WithOneRetry_ShouldWorkCorrectly()
    {
        var executionCount = 0;

        FileOperationRetryHelper.ExecuteWithRetry(() =>
            {
                executionCount++;
            },
            retries: 1,
            delayMilliseconds: 1);

        executionCount.ShouldBe(1);
    }

    [Fact]
    public void ExecuteWithRetry_WithComplexOperation_ShouldExecuteSuccessfully()
    {
        var result = 0;

        FileOperationRetryHelper.ExecuteWithRetry(() =>
            {
                // Simulate a file write operation
                result = 42;
            },
            retries: 3,
            delayMilliseconds: 1);

        result.ShouldBe(42);
    }
}
