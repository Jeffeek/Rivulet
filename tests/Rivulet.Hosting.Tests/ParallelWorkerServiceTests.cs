using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Rivulet.Core;
using System.Collections.Concurrent;

namespace Rivulet.Hosting.Tests;

public class ParallelWorkerServiceTests
{
    private class TestWorkerService : ParallelWorkerService<int, string>
    {
        private readonly IAsyncEnumerable<int> _sourceItems;
        public ConcurrentBag<int> ProcessedItems { get; } = new();
        public ConcurrentBag<string> Results { get; } = new();
        public int ProcessCallCount => _processCallCount;
        private int _processCallCount;

        public TestWorkerService(
            ILogger<TestWorkerService> logger,
            IAsyncEnumerable<int> sourceItems,
            ParallelOptionsRivulet? options = null)
            : base(logger, options)
        {
            _sourceItems = sourceItems;
        }

        protected override IAsyncEnumerable<int> GetSourceItems(CancellationToken cancellationToken)
        {
            return _sourceItems;
        }

        protected override Task<string> ProcessAsync(int item, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _processCallCount);
            ProcessedItems.Add(item);
            return Task.FromResult($"Processed-{item}");
        }

        protected override Task OnResultAsync(string result, CancellationToken cancellationToken)
        {
            Results.Add(result);
            return Task.CompletedTask;
        }
    }

    private class DelayedWorkerService : ParallelWorkerService<int, int>
    {
        private readonly IAsyncEnumerable<int> _sourceItems;
        private readonly int _delayMs;

        public DelayedWorkerService(
            ILogger<DelayedWorkerService> logger,
            IAsyncEnumerable<int> sourceItems,
            int delayMs,
            ParallelOptionsRivulet? options = null)
            : base(logger, options)
        {
            _sourceItems = sourceItems;
            _delayMs = delayMs;
        }

        protected override IAsyncEnumerable<int> GetSourceItems(CancellationToken cancellationToken)
        {
            return _sourceItems;
        }

        protected override async Task<int> ProcessAsync(int item, CancellationToken cancellationToken)
        {
            await Task.Delay(_delayMs, cancellationToken);
            return item * 2;
        }
    }

    private class ThrowingWorkerService : ParallelWorkerService<int, int>
    {
        private readonly IAsyncEnumerable<int> _sourceItems;

        public ThrowingWorkerService(ILogger<ThrowingWorkerService> logger, IAsyncEnumerable<int> sourceItems)
            : base(logger)
        {
            _sourceItems = sourceItems;
        }

        protected override IAsyncEnumerable<int> GetSourceItems(CancellationToken cancellationToken)
        {
            return _sourceItems;
        }

        protected override Task<int> ProcessAsync(int item, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Test exception");
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
        var logger = NullLogger<TestWorkerService>.Instance;
        var items = GenerateItemsAsync(5);
        var service = new TestWorkerService(logger, items);

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        await Task.Delay(50);
        cts.Cancel();

        await service.StopAsync(CancellationToken.None);

        service.ProcessedItems.Should().HaveCount(5);
        service.Results.Should().HaveCount(5);
        service.ProcessedItems.Should().BeEquivalentTo(new[] { 1, 2, 3, 4, 5 });
    }

    [Fact]
    public async Task StartAsync_ShouldCallOnResultForEachResult()
    {
        var logger = NullLogger<TestWorkerService>.Instance;
        var items = GenerateItemsAsync(3);
        var service = new TestWorkerService(logger, items);

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        await Task.Delay(50);
        cts.Cancel();

        await service.StopAsync(CancellationToken.None);

        service.Results.Should().HaveCount(3);
        service.Results.Should().Contain("Processed-1");
        service.Results.Should().Contain("Processed-2");
        service.Results.Should().Contain("Processed-3");
    }

    [Fact]
    public async Task StartAsync_WithParallelOptions_ShouldProcessInParallel()
    {
        var logger = NullLogger<DelayedWorkerService>.Instance;
        var items = GenerateItemsAsync(6);
        var options = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 3 };
        var service = new DelayedWorkerService(logger, items, delayMs: 20, options);

        using var cts = new CancellationTokenSource();
        var startTime = DateTime.UtcNow;

        await service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();

        await service.StopAsync(CancellationToken.None);

        var elapsed = DateTime.UtcNow - startTime;
        elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public async Task StartAsync_WhenCancelled_ShouldStopGracefully()
    {
        var logger = NullLogger<TestWorkerService>.Instance;
        var items = GenerateItemsAsync(100, delayMs: 10);
        var service = new TestWorkerService(logger, items);

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        await Task.Delay(50);
        cts.Cancel();

        await service.StopAsync(CancellationToken.None);

        service.ProcessedItems.Count.Should().BeLessThan(100);
    }

    [Fact]
    public async Task StartAsync_WithException_ShouldHandleGracefully()
    {
        var logger = NullLogger<ThrowingWorkerService>.Instance;
        var items = GenerateItemsAsync(3);
        var service = new ThrowingWorkerService(logger, items);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        await service.StartAsync(cts.Token);
        await Task.Delay(100);

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        var items = GenerateItemsAsync(1);

        var act = () => new TestWorkerService(null!, items);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task StartAsync_WithNullOptions_ShouldUseDefaults()
    {
        var logger = NullLogger<TestWorkerService>.Instance;
        var items = GenerateItemsAsync(3);
        var service = new TestWorkerService(logger, items);

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        await Task.Delay(50, cts.Token);
        await cts.CancelAsync();

        await service.StopAsync(CancellationToken.None);

        service.ProcessedItems.Should().HaveCount(3);
    }

    [Fact]
    public async Task StartAsync_WithEmptySource_ShouldCompleteImmediately()
    {
        var logger = NullLogger<TestWorkerService>.Instance;
        var items = GenerateItemsAsync(0);
        var service = new TestWorkerService(logger, items);

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        await Task.Delay(30);
        cts.Cancel();

        await service.StopAsync(CancellationToken.None);

        service.ProcessedItems.Should().BeEmpty();
        service.Results.Should().BeEmpty();
        service.ProcessCallCount.Should().Be(0);
    }

    [Fact]
    public async Task ProcessAsync_ShouldBeCalledForEachItem()
    {
        var logger = NullLogger<TestWorkerService>.Instance;
        var items = GenerateItemsAsync(7);
        var service = new TestWorkerService(logger, items);

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        await Task.Delay(50);
        cts.Cancel();

        await service.StopAsync(CancellationToken.None);

        service.ProcessCallCount.Should().Be(7);
    }

    [Fact]
    public async Task Constructor_WithCustomOptions_ShouldUseProvidedOptions()
    {
        var logger = NullLogger<TestWorkerService>.Instance;
        var items = GenerateItemsAsync(3);
        var options = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 1 };
        var service = new TestWorkerService(logger, items, options);

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);
        await Task.Delay(50);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        service.ProcessedItems.Should().HaveCount(3);
    }
}
