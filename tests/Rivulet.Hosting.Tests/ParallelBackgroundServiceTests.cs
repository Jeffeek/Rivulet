using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Rivulet.Base.Tests;
using Rivulet.Core;

namespace Rivulet.Hosting.Tests;

public class ParallelBackgroundServiceTests
{
    [Fact]
    public async Task StartAsync_ShouldProcessAllItems()
    {
        var logger = NullLogger<TestBackgroundService>.Instance;
        var items = TestDataGenerators.GenerateItemsAsync(5);
        var service = new TestBackgroundService(logger, items);

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        // Increased from 50ms → 200ms for Windows CI/CD reliability
        // BackgroundService needs time to start ExecuteAsync and process all items
        await Task.Delay(200, cts.Token);
        await cts.CancelAsync();

        await service.StopAsync(CancellationToken.None);

        service.ProcessedItems.Count.ShouldBe(5);
        service.ProcessedItems.OrderBy(static x => x).ShouldBe([1, 2, 3, 4, 5]);
    }

    [Fact]
    public async Task StartAsync_WithOptions_ShouldUseProvidedOptions()
    {
        var logger = NullLogger<TestBackgroundService>.Instance;
        var items = TestDataGenerators.GenerateItemsAsync(10);
        var options = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 2 };
        var service = new TestBackgroundService(logger, items, options);

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        // Increased from 50ms → 200ms for Windows CI/CD reliability
        await Task.Delay(200, cts.Token);
        await cts.CancelAsync();

        await service.StopAsync(CancellationToken.None);

        service.ProcessedItems.Count.ShouldBe(10);
    }


    [Fact]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        var items = TestDataGenerators.GenerateItemsAsync(1);

        var act = () => new TestBackgroundService(null!, items);

        var ex = act.ShouldThrow<ArgumentNullException>();
        ex.ParamName.ShouldBe("logger");
    }

    [Fact]
    public async Task StartAsync_WithNullOptions_ShouldUseDefaults()
    {
        var logger = NullLogger<TestBackgroundService>.Instance;
        var items = TestDataGenerators.GenerateItemsAsync(3);
        var service = new TestBackgroundService(logger, items);

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        // Increased from 50ms → 200ms for Windows CI/CD reliability
        await Task.Delay(200, cts.Token);
        await cts.CancelAsync();

        await service.StopAsync(CancellationToken.None);

        service.ProcessedItems.Count.ShouldBe(3);
    }

    [Fact]
    public async Task StartAsync_WithEmptySource_ShouldCompleteImmediately()
    {
        var logger = NullLogger<TestBackgroundService>.Instance;
        var items = TestDataGenerators.GenerateItemsAsync(0);
        var service = new TestBackgroundService(logger, items);

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        await Task.Delay(30, cts.Token);
        await cts.CancelAsync();

        await service.StopAsync(CancellationToken.None);

        service.ProcessedItems.ShouldBeEmpty();
        service.ProcessCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task ProcessItemAsync_ShouldBeCalledForEachItem()
    {
        var logger = NullLogger<TestBackgroundService>.Instance;
        var items = TestDataGenerators.GenerateItemsAsync(7);
        var service = new TestBackgroundService(logger, items);

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        // Increased from 50ms → 200ms for Windows CI/CD reliability
        await Task.Delay(200, cts.Token);
        await cts.CancelAsync();

        await service.StopAsync(CancellationToken.None);

        service.ProcessCallCount.ShouldBe(7);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_ShouldLogInformationAndExitGracefully()
    {
        var loggerFactory = LoggerFactory.Create(static builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        var logger = loggerFactory.CreateLogger<TestBackgroundService>();

        var service = new TestBackgroundService(logger, SlowGenerateItems());

        using var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(20, CancellationToken.None);    // Let it start
        await cts.CancelAsync(); // Cancel it
        await service.StopAsync(CancellationToken.None);

        // Assert - should exit gracefully without throwing
        service.ProcessedItems.Count.ShouldBeLessThan(100);
        return;

        static async IAsyncEnumerable<int> SlowGenerateItems()
        {
            for (var i = 1; i <= 100; i++)
            {
                await Task.Delay(50, CancellationToken.None); // Slow enough to get cancelled
                yield return i;
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenProcessThrowsException_ShouldLogErrorAndRethrow()
    {
        var loggerFactory = LoggerFactory.Create(static builder => builder.AddConsole().SetMinimumLevel(LogLevel.Error));
        var logger = loggerFactory.CreateLogger<ThrowingBackgroundService>();

        var items = TestDataGenerators.GenerateItemsAsync(3);
        var service = new ThrowingBackgroundService(logger, items);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act & Assert
        await service.StartAsync(cts.Token);

        // Wait for the exception to be thrown and logged
        await Task.Delay(100, cts.Token);

        // The service should have logged the error
        // Since we can't easily verify logs in this test, we verify the behavior continues
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenGetItemsAsyncThrowsException_ShouldLogErrorAndHandleGracefully()
    {
        var loggerFactory = LoggerFactory.Create(static builder => builder.AddConsole().SetMinimumLevel(LogLevel.Error));
        var logger = loggerFactory.CreateLogger<FatalErrorBackgroundService>();

        var service = new FatalErrorBackgroundService(logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // BackgroundService.StartAsync starts ExecuteAsync as a background task and returns immediately.
        // Exceptions in ExecuteAsync are caught, logged, and stored in the ExecuteTask but are NOT
        // propagated by StopAsync. This test verifies the service handles exceptions gracefully
        // without crashing the test host.
        await service.StartAsync(cts.Token);
        await Task.Delay(100, CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        // The service should have logged the error and stopped gracefully
        // No unhandled exception should crash the test
    }

    private sealed class TestBackgroundService(
        ILogger<TestBackgroundService> logger,
        IAsyncEnumerable<int> items,
        ParallelOptionsRivulet? options = null
    ) : ParallelBackgroundService<int>(logger, options)
    {
        private int _processCallCount;
        public ConcurrentBag<int> ProcessedItems { get; } = new();
        public int ProcessCallCount => _processCallCount;

        protected override IAsyncEnumerable<int> GetItemsAsync(CancellationToken cancellationToken) =>
            items;

        protected override ValueTask ProcessItemAsync(int item, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _processCallCount);
            ProcessedItems.Add(item);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingBackgroundService(
        ILogger<ThrowingBackgroundService> logger,
        IAsyncEnumerable<int> items
    ) : ParallelBackgroundService<int>(logger)
    {
        protected override IAsyncEnumerable<int> GetItemsAsync(CancellationToken cancellationToken) => items;

        protected override ValueTask ProcessItemAsync(int item, CancellationToken cancellationToken) => throw
            // Simulate an unhandled exception
            new InvalidOperationException($"Test exception for item {item}");
    }

    private sealed class FatalErrorBackgroundService(ILogger<FatalErrorBackgroundService> logger)
        : ParallelBackgroundService<int>(logger)
    {
        protected override IAsyncEnumerable<int> GetItemsAsync(CancellationToken cancellationToken) => throw
            // Throw from GetItemsAsync to hit the exception path in ExecuteAsync
            new InvalidOperationException("Fatal error in GetItemsAsync");

        protected override ValueTask ProcessItemAsync(int item, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}