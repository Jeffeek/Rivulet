using FluentAssertions;
using Rivulet.Core;
using System.Collections.Concurrent;
using Rivulet.Core.Observability;

namespace Rivulet.Diagnostics.Tests;

public class RivuletEventListenerBaseTests : IDisposable
{
    private readonly TestEventListener _listener = new();

    [Fact]
    public async Task EventListenerBase_ShouldReceiveCounters_WhenOperationsRun()
    {
        // Use longer operation (200ms per item) to ensure EventCounters poll DURING execution
        // EventCounters have ~1 second polling interval, so operation needs to run for 1-2+ seconds
        // 10 items * 200ms / 2 parallelism = 1000ms (1 second) of operation time
        await Enumerable.Range(1, 10)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(async (x, ct) =>
            {
                await Task.Delay(200, ct);
                return x * 2;
            }, new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 2
            })
            .ToListAsync();

        // EventSource publishes counters every 1 second
        // Increased from 2000ms → 5000ms for Windows CI/CD reliability
        var deadline = DateTime.UtcNow.AddMilliseconds(5000);
        while (_listener.ReceivedCounters.IsEmpty && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        _listener.ReceivedCounters.Should().NotBeEmpty();
        _listener.ReceivedCounters.Keys.Should().Contain(RivuletMetricsConstants.CounterNames.ItemsStarted);
    }

    [Fact]
    public async Task EventListenerBase_ShouldHandleMissingDisplayName()
    {
        // Use longer operation (200ms per item) to ensure EventCounters poll DURING execution
        // 5 items * 200ms / 2 parallelism = 500ms of operation time
        await Enumerable.Range(1, 5)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(async (x, ct) =>
            {
                await Task.Delay(200, ct);
                return x;
            }, new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 2
            })
            .ToListAsync();

        // EventSource publishes counters every 1 second - poll with timeout
        // Increased from 2000ms → 5000ms for Windows CI/CD reliability (1/180 failures)
        var deadline = DateTime.UtcNow.AddMilliseconds(5000);
        while (_listener.ReceivedCounters.IsEmpty && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        _listener.ReceivedCounters.Should().NotBeEmpty();
        foreach (var counter in _listener.ReceivedCounters)
        {
            counter.Value.DisplayName.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task EventListenerBase_ShouldHandleMissingDisplayUnits()
    {
        // Use longer operation (200ms per item) to ensure EventCounters poll DURING execution
        // 5 items * 200ms / 2 parallelism = 500ms of operation time
        await Enumerable.Range(1, 5)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(async (x, ct) =>
            {
                await Task.Delay(200, ct);
                return x;
            }, new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 2
            })
            .ToListAsync();

        // EventSource publishes counters every 1 second - poll with timeout
        // Increased from 2000ms → 5000ms for Windows CI/CD reliability (2/180 failures)
        var deadline = DateTime.UtcNow.AddMilliseconds(5000);
        while (_listener.ReceivedCounters.IsEmpty && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        _listener.ReceivedCounters.Should().NotBeEmpty();
        foreach (var counter in _listener.ReceivedCounters)
        {
            counter.Value.DisplayUnits.Should().NotBeNull();
        }
    }

    [Fact]
    public void EventListenerBase_WithSyncDispose_ShouldDisposeCleanly()
    {
        var listener = new TestEventListener();

        // Run a simple operation asynchronously
        var task = Enumerable.Range(1, 3)
            .SelectParallelAsync((x, _) => ValueTask.FromResult(x), new ParallelOptionsRivulet());

#pragma warning disable xUnit1031
        task.Wait();
#pragma warning restore xUnit1031

        // Dispose using sync Dispose() method
        listener.Dispose();

        // Should not throw and should complete cleanly
        listener.ReceivedCounters.Should().NotBeNull();
    }

    public void Dispose()
    {
        _listener.Dispose();
    }

    private sealed class TestEventListener : RivuletEventListenerBase
    {
        public ConcurrentDictionary<string, CounterData> ReceivedCounters { get; } = new();

        protected override void OnCounterReceived(string name, string displayName, double value, string displayUnits)
        {
            ReceivedCounters[name] = new CounterData(displayName, displayUnits);
        }
    }

    private sealed record CounterData(string DisplayName, string DisplayUnits);
}
