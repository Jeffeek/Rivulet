using FluentAssertions;
using System.Collections.Concurrent;

namespace Rivulet.Core.Tests;

/// <summary>
/// Tests for EventSource metrics that must run sequentially.
/// EventSource is a singleton, so these tests cannot run in parallel with each other.
/// </summary>
[Collection("EventSource Sequential Tests")]
public class EventSourceTests
{
    [Fact]
    public async Task EventCounters_AreExposedAndIncremented()
    {
        var source = Enumerable.Range(1, 50);

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4
        };

        var initialStarted = RivuletEventSource.Log.GetItemsStarted();
        var initialCompleted = RivuletEventSource.Log.GetItemsCompleted();

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(5, ct);
                return x * 2;
            },
            options);

        var finalStarted = RivuletEventSource.Log.GetItemsStarted();
        var finalCompleted = RivuletEventSource.Log.GetItemsCompleted();

        results.Should().HaveCount(50);
        // These tests run sequentially, so we can use exact assertions
        (finalStarted - initialStarted).Should().Be(50);
        (finalCompleted - initialCompleted).Should().Be(50);
    }

    [Fact]
    public async Task EventCounters_TrackFailures()
    {
        var source = Enumerable.Range(1, 30);

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            ErrorMode = ErrorMode.BestEffort
        };

        var initialFailures = RivuletEventSource.Log.GetTotalFailures();

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(5, ct);
                if (x % 5 == 0)
                    throw new InvalidOperationException("Error");
                return x * 2;
            },
            options);

        var finalFailures = RivuletEventSource.Log.GetTotalFailures();

        results.Should().HaveCount(24); // 30 - 6 failures
        // These tests run sequentially, so we can use exact assertions
        (finalFailures - initialFailures).Should().Be(6);
    }

    [Fact]
    public async Task EventCounters_TrackRetries()
    {
        var source = Enumerable.Range(1, 10);
        var attempts = new ConcurrentDictionary<int, int>();

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            MaxRetries = 2,
            IsTransient = ex => ex is InvalidOperationException
        };

        var initialRetries = RivuletEventSource.Log.GetTotalRetries();

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(5, ct);
                var attemptCount = attempts.AddOrUpdate(x, 1, (_, count) => count + 1);
                if (attemptCount < 2)
                    throw new InvalidOperationException("Transient");
                return x * 2;
            },
            options);

        var finalRetries = RivuletEventSource.Log.GetTotalRetries();

        results.Should().HaveCount(10);
        // These tests run sequentially, so we can use exact assertions
        (finalRetries - initialRetries).Should().Be(10); // Each of 10 items retries exactly once
    }

    [Fact]
    public void EventSource_WithEventListener_CreatesCountersAndDisposes()
    {
        using var listener = new TestEventListener();
        listener.EnableEvents(RivuletEventSource.Log, System.Diagnostics.Tracing.EventLevel.Verbose,
            System.Diagnostics.Tracing.EventKeywords.All);

        RivuletEventSource.Log.IncrementItemsStarted();
        RivuletEventSource.Log.IncrementItemsCompleted();
        RivuletEventSource.Log.IncrementRetries();
        RivuletEventSource.Log.IncrementFailures();
        RivuletEventSource.Log.IncrementThrottleEvents();
        RivuletEventSource.Log.IncrementDrainEvents();

        RivuletEventSource.Log.GetItemsStarted().Should().BeGreaterThan(0);
        RivuletEventSource.Log.GetItemsCompleted().Should().BeGreaterThan(0);
        RivuletEventSource.Log.GetTotalRetries().Should().BeGreaterThan(0);
        RivuletEventSource.Log.GetTotalFailures().Should().BeGreaterThan(0);
        RivuletEventSource.Log.GetThrottleEvents().Should().BeGreaterThan(0);
        RivuletEventSource.Log.GetDrainEvents().Should().BeGreaterThan(0);

        listener.DisableEvents(RivuletEventSource.Log);
    }

    private class TestEventListener : System.Diagnostics.Tracing.EventListener
    {
        // ReSharper disable once RedundantOverriddenMember
        protected override void OnEventSourceCreated(System.Diagnostics.Tracing.EventSource eventSource)
        {
            base.OnEventSourceCreated(eventSource);
        }

        protected override void OnEventWritten(System.Diagnostics.Tracing.EventWrittenEventArgs eventData)
        {
            // No-op, just need to listen
        }
    }
}
