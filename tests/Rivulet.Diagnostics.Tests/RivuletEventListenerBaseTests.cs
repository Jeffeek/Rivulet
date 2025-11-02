using FluentAssertions;
using Rivulet.Core;
using System.Collections.Concurrent;

namespace Rivulet.Diagnostics.Tests;

public class RivuletEventListenerBaseTests : IDisposable
{
    private readonly TestEventListener _listener;

    public RivuletEventListenerBaseTests()
    {
        _listener = new TestEventListener();
    }

    [Fact]
    public async Task EventListenerBase_ShouldReceiveCounters_WhenOperationsRun()
    {
        await Enumerable.Range(1, 10)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x * 2;
            }, new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 2
            })
            .ToListAsync();

        await Task.Delay(1100);

        _listener.ReceivedCounters.Should().NotBeEmpty();
        _listener.ReceivedCounters.Keys.Should().Contain("items-started");
    }

    [Fact]
    public async Task EventListenerBase_ShouldHandleMissingDisplayName()
    {
        await Enumerable.Range(1, 5)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x;
            }, new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 2
            })
            .ToListAsync();

        await Task.Delay(1100);

        _listener.ReceivedCounters.Should().NotBeEmpty();
        foreach (var counter in _listener.ReceivedCounters)
        {
            counter.Value.DisplayName.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task EventListenerBase_ShouldHandleMissingDisplayUnits()
    {
        await Enumerable.Range(1, 5)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x;
            }, new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 2
            })
            .ToListAsync();

        await Task.Delay(1100);

        _listener.ReceivedCounters.Should().NotBeEmpty();
        foreach (var counter in _listener.ReceivedCounters)
        {
            counter.Value.DisplayUnits.Should().NotBeNull();
        }
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
