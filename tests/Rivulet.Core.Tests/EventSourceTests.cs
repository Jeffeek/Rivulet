using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using Rivulet.Core.Observability;

namespace Rivulet.Core.Tests;

/// <summary>
///     Tests for EventSource metrics that must run sequentially.
///     EventSource is a singleton, so these tests cannot run in parallel with each other.
/// </summary>
[Collection(TestCollections.EventSourceSequential)]
public sealed class EventSourceTests
{
    [Fact]
    public async Task EventCounters_AreExposedAndIncremented()
    {
        var source = Enumerable.Range(1, 50);

        var options = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 4 };

        var initialStarted = RivuletEventSource.Log.GetItemsStarted();
        var initialCompleted = RivuletEventSource.Log.GetItemsCompleted();

        var results = await source.SelectParallelAsync(
            static async (x, ct) =>
            {
                await Task.Delay(5, ct);
                return x * 2;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        var finalStarted = RivuletEventSource.Log.GetItemsStarted();
        var finalCompleted = RivuletEventSource.Log.GetItemsCompleted();

        results.Count.ShouldBe(50);
        // These tests run sequentially, so we can use exact assertions
        (finalStarted - initialStarted).ShouldBe(50);
        (finalCompleted - initialCompleted).ShouldBe(50);
    }

    [Fact]
    public async Task EventCounters_TrackFailures()
    {
        var source = Enumerable.Range(1, 30);

        var options = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 4, ErrorMode = ErrorMode.BestEffort };

        var initialFailures = RivuletEventSource.Log.GetTotalFailures();

        var results = await source.SelectParallelAsync(
            static async (x, ct) =>
            {
                await Task.Delay(5, ct);
                return x % 5 == 0 ? throw new InvalidOperationException("Error") : x * 2;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        var finalFailures = RivuletEventSource.Log.GetTotalFailures();

        results.Count.ShouldBe(24); // 30 - 6 failures
        // These tests run sequentially, so we can use exact assertions
        (finalFailures - initialFailures).ShouldBe(6);
    }

    [Fact]
    public async Task EventCounters_TrackRetries()
    {
        var source = Enumerable.Range(1, 10);
        var attempts = new ConcurrentDictionary<int, int>();

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4, MaxRetries = 2, IsTransient = static ex => ex is InvalidOperationException
        };

        var initialRetries = RivuletEventSource.Log.GetTotalRetries();

        var results = await source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(5, ct);
                var attemptCount = attempts.AddOrUpdate(x, 1, static (_, count) => count + 1);
                return attemptCount < 2 ? throw new InvalidOperationException("Transient") : x * 2;
            },
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        var finalRetries = RivuletEventSource.Log.GetTotalRetries();

        results.Count.ShouldBe(10);
        // These tests run sequentially, so we can use exact assertions
        (finalRetries - initialRetries).ShouldBe(10); // Each of 10 items retries exactly once
    }

    [Fact]
    public void EventSource_WithEventListener_CreatesCountersAndDisposes()
    {
        using var listener = new TestEventListener();
        listener.EnableEvents(RivuletEventSource.Log,
            EventLevel.Verbose,
            EventKeywords.All);

        RivuletEventSource.Log.IncrementItemsStarted();
        RivuletEventSource.Log.IncrementItemsCompleted();
        RivuletEventSource.Log.IncrementRetries();
        RivuletEventSource.Log.IncrementFailures();
        RivuletEventSource.Log.IncrementThrottleEvents();
        RivuletEventSource.Log.IncrementDrainEvents();

        RivuletEventSource.Log.GetItemsStarted().ShouldBeGreaterThan(0);
        RivuletEventSource.Log.GetItemsCompleted().ShouldBeGreaterThan(0);
        RivuletEventSource.Log.GetTotalRetries().ShouldBeGreaterThan(0);
        RivuletEventSource.Log.GetTotalFailures().ShouldBeGreaterThan(0);
        RivuletEventSource.Log.GetThrottleEvents().ShouldBeGreaterThan(0);
        RivuletEventSource.Log.GetDrainEvents().ShouldBeGreaterThan(0);

        listener.DisableEvents(RivuletEventSource.Log);
    }

    private sealed class TestEventListener : EventListener
    {
        // ReSharper disable once RedundantOverriddenMember
        protected override void OnEventSourceCreated(EventSource eventSource) =>
            base.OnEventSourceCreated(eventSource);

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            // No-op, just need to listen
        }
    }
}
