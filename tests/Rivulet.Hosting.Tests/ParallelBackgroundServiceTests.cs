using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Rivulet.Core;
using System.Collections.Concurrent;

namespace Rivulet.Hosting.Tests;

public class ParallelBackgroundServiceTests
{
    private class TestBackgroundService(
        ILogger<TestBackgroundService> logger,
        IAsyncEnumerable<int> items,
        ParallelOptionsRivulet? options = null) : ParallelBackgroundService<int>(logger, options)
    {
        public ConcurrentBag<int> ProcessedItems { get; } = new();
        public int ProcessCallCount => _processCallCount;
        private int _processCallCount;

        protected override IAsyncEnumerable<int> GetItemsAsync(CancellationToken cancellationToken)
        {
            return items;
        }

        protected override ValueTask ProcessItemAsync(int item, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _processCallCount);
            ProcessedItems.Add(item);
            return ValueTask.CompletedTask;
        }
    }

    private static async IAsyncEnumerable<int> GenerateItemsAsync(int count, int delayMs = 0)
    {
        for (var i = 1; i <= count; i++)
        {
            if (delayMs > 0)
                await Task.Delay(delayMs);
            yield return i;
        }
    }

    [Fact]
    public async Task StartAsync_ShouldProcessAllItems()
    {
        var logger = NullLogger<TestBackgroundService>.Instance;
        var items = GenerateItemsAsync(5);
        var service = new TestBackgroundService(logger, items);

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        await Task.Delay(200, cts.Token);
        await cts.CancelAsync();

        await service.StopAsync(CancellationToken.None);

        service.ProcessedItems.Should().HaveCount(5);
        service.ProcessedItems.Should().BeEquivalentTo([1, 2, 3, 4, 5]);
    }

    [Fact]
    public async Task StartAsync_WithOptions_ShouldUseProvidedOptions()
    {
        var logger = NullLogger<TestBackgroundService>.Instance;
        var items = GenerateItemsAsync(10);
        var options = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 2 };
        var service = new TestBackgroundService(logger, items, options);

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        await Task.Delay(200, cts.Token);
        await cts.CancelAsync();

        await service.StopAsync(CancellationToken.None);

        service.ProcessedItems.Should().HaveCount(10);
    }


    [Fact]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        var items = GenerateItemsAsync(1);

        var act = () => new TestBackgroundService(null!, items);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task StartAsync_WithNullOptions_ShouldUseDefaults()
    {
        var logger = NullLogger<TestBackgroundService>.Instance;
        var items = GenerateItemsAsync(3);
        var service = new TestBackgroundService(logger, items);

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        await Task.Delay(200, cts.Token);
        await cts.CancelAsync();

        await service.StopAsync(CancellationToken.None);

        service.ProcessedItems.Should().HaveCount(3);
    }

    [Fact]
    public async Task StartAsync_WithEmptySource_ShouldCompleteImmediately()
    {
        var logger = NullLogger<TestBackgroundService>.Instance;
        var items = GenerateItemsAsync(0);
        var service = new TestBackgroundService(logger, items);

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        await Task.Delay(100, cts.Token);
        await cts.CancelAsync();

        await service.StopAsync(CancellationToken.None);

        service.ProcessedItems.Should().BeEmpty();
        service.ProcessCallCount.Should().Be(0);
    }

    [Fact]
    public async Task ProcessItemAsync_ShouldBeCalledForEachItem()
    {
        var logger = NullLogger<TestBackgroundService>.Instance;
        var items = GenerateItemsAsync(7);
        var service = new TestBackgroundService(logger, items);

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        await Task.Delay(200, cts.Token);
        await cts.CancelAsync();

        await service.StopAsync(CancellationToken.None);

        service.ProcessCallCount.Should().Be(7);
    }
}
