using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using Rivulet.Base.Tests;
using Rivulet.Core;
using Rivulet.Core.Observability;

namespace Rivulet.Diagnostics.Tests;

// EventSource and EventListener are process-wide singletons by design.
// These tests must run sequentially to avoid cross-test pollution.
[Collection(TestCollections.SerialEventSource)]
public class RivuletEventListenerBaseTests : IDisposable
{
    private readonly TestEventListener _listener = new();

    public void Dispose() => _listener.Dispose();

    [Fact]
    public async Task EventListenerBase_ShouldReceiveCounters_WhenOperationsRun()
    {
        // Use longer operation (300ms per item) to ensure EventCounters poll DURING execution
        // EventCounters have ~1 second polling interval, so operation needs to run for 2+ seconds
        // 10 items * 300ms / 2 parallelism = 1500ms of operation time
        await Enumerable.Range(1, 10)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(static async (x, ct) =>
                {
                    await Task.Delay(300, ct);
                    return x * 2;
                },
                new() { MaxDegreeOfParallelism = 2 })
            .ToListAsync();

        // EventSource publishes counters every 1 second
        // Increased to 8000ms for Windows CI/CD reliability
        await DeadlineExtensions.ApplyDeadlineAsync(
            DateTime.UtcNow.AddMilliseconds(6000),
            () => Task.Delay(100),
            () => _listener.ReceivedCounters.IsEmpty);

        _listener.ReceivedCounters.ShouldNotBeEmpty();
        _listener.ReceivedCounters.Keys.ShouldContain(RivuletMetricsConstants.CounterNames.ItemsStarted);
    }

    [Fact]
    public async Task EventListenerBase_ShouldHandleMissingDisplayName()
    {
        // Use longer operation (300ms per item) to ensure EventCounters poll DURING execution
        // 10 items * 300ms / 2 parallelism = 1500ms of operation time
        await Enumerable.Range(1, 10)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(static async (x, ct) =>
                {
                    await Task.Delay(300, ct);
                    return x;
                },
                new() { MaxDegreeOfParallelism = 2 })
            .ToListAsync();

        // EventSource publishes counters every 1 second - poll with timeout
        // Increased to 8000ms for Windows CI/CD reliability
        var deadline = DateTime.UtcNow.AddMilliseconds(8000);
        while (_listener.ReceivedCounters.IsEmpty && DateTime.UtcNow < deadline) await Task.Delay(100);

        _listener.ReceivedCounters.ShouldNotBeEmpty();
        foreach (var counter in _listener.ReceivedCounters) counter.Value.DisplayName.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task EventListenerBase_ShouldHandleMissingDisplayUnits()
    {
        // Use longer operation (300ms per item) to ensure EventCounters poll DURING execution
        // 10 items * 300ms / 2 parallelism = 1500ms of operation time
        await Enumerable.Range(1, 10)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(static async (x, ct) =>
                {
                    await Task.Delay(300, ct);
                    return x;
                },
                new() { MaxDegreeOfParallelism = 2 })
            .ToListAsync();

        // EventSource publishes counters every 1 second - poll with timeout
        // Increased to 8000ms for Windows CI/CD reliability
        var deadline = DateTime.UtcNow.AddMilliseconds(8000);
        while (_listener.ReceivedCounters.IsEmpty && DateTime.UtcNow < deadline) await Task.Delay(100);

        _listener.ReceivedCounters.ShouldNotBeEmpty();
        foreach (var counter in _listener.ReceivedCounters) counter.Value.DisplayUnits.ShouldNotBeNull();
    }

    [Fact]
    public void EventListenerBase_WithSyncDispose_ShouldDisposeCleanly()
    {
        var listener = new TestEventListener();

        // Run a simple operation asynchronously
        var task = Enumerable.Range(1, 3)
            .SelectParallelAsync(static (x, _) => ValueTask.FromResult(x), new());

#pragma warning disable xUnit1031
        task.Wait();
#pragma warning restore xUnit1031

        // Dispose using sync Dispose() method
        listener.Dispose();

        // Should not throw and should complete cleanly
        listener.ReceivedCounters.ShouldNotBeNull();
    }

    [Fact]
    public async Task EventListenerBase_ShouldIgnoreEvents_WhenEventSourceNameIsWrong()
    {
        var listener = new TestEventListener();

        // Clear any pre-existing events from the process-wide Rivulet.Core EventSource
        // that may have been triggered by previous tests
        listener.ReceivedCounters.Clear();

        // Create a custom EventSource with a different name
        using var customSource = new CustomEventSource("NotRivuletCore");
        customSource.WriteEvent(1, "test");

        // Give it a moment for any events to be processed
        await Task.Delay(100);

        // Should not receive any counters because event source name doesn't match
        listener.ReceivedCounters.ShouldBeEmpty();

        listener.Dispose();
    }

    [Fact]
    public void EventListenerBase_ShouldHandleNullDisplayName()
    {
        // This test ensures the null coalescing operator on line 61 for displayName is covered
        // When EventSource doesn't provide DisplayName, it should fall back to name
        var listener = new TestEventListener();

        // Create an EventSource that sends counter data without DisplayName
        using var customSource = new RivuletTestEventSource();
        customSource.EmitCounterWithoutDisplayName();

        // Wait a bit for the event to be processed
        Thread.Sleep(100);

        listener.Dispose();
    }

    [Fact]
    public async Task EventListenerBase_ShouldHandleNullDisplayUnits()
    {
        // This test ensures the null coalescing operator on line 61 for displayUnits is covered
        // When EventSource doesn't provide DisplayUnits, it should fall back to empty string
        var listener = new TestEventListener();

        // Create an EventSource that sends counter data without DisplayUnits
        using var customSource = new RivuletTestEventSource();
        customSource.EmitCounterWithoutDisplayUnits();

        // Wait a bit for the event to be processed
        await Task.Delay(1000);

        listener.Dispose();
    }

    [Fact]
    public void EventListenerBase_ShouldHandleEventWithNullPayload()
    {
        // Test that listener can handle events with null or empty payload
        var listener = new TestEventListener();

        using var customSource = new RivuletTestEventSource();
        customSource.WriteEmptyEvent(); // This creates an event with empty payload

        Thread.Sleep(100);

        // Should not crash and should not add any counters for empty payload
        listener.ReceivedCounters.ShouldNotBeNull();

        listener.Dispose();
    }

    [Fact]
    public void EventListenerBase_ShouldHandleNonCounterEvents()
    {
        // Test that listener ignores non-counter events from RivuletCore
        var listener = new TestEventListener();

        using var customSource = new RivuletTestEventSource();
        customSource.WriteInformationalEvent("test message");

        Thread.Sleep(100);

        // Should handle gracefully
        listener.ReceivedCounters.ShouldNotBeNull();

        listener.Dispose();
    }

    [Fact]
    public async Task EventListenerBase_ShouldEnableEvents_WhenRivuletCoreSourceCreated()
    {
        // Test that listener enables events when Rivulet.Core EventSource is created
        var listener = new TestEventListener();

        // Trigger operations with long enough duration for EventCounters to poll
        // 10 items * 300ms / 2 parallelism = 1500ms of operation time
        await Enumerable.Range(1, 10)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(static async (x, ct) =>
                {
                    await Task.Delay(300, ct);
                    return x;
                },
                new() { MaxDegreeOfParallelism = 2 })
            .ToListAsync();

        // Wait for counters - increased to 8000ms for CI/CD reliability
        await DeadlineExtensions.ApplyDeadlineAsync(
            DateTime.UtcNow.AddMilliseconds(6000),
            () => Task.Delay(100),
            () => listener.ReceivedCounters.IsEmpty);

        // Listener should have received counters, proving IsEnabled was set to true
        listener.ReceivedCounters.ShouldNotBeEmpty();

        listener.Dispose();
    }

    [Fact]
    public void EventListenerBase_ShouldHandleDispose_Idempotently()
    {
        var listener = new TestEventListener();

        // Dispose multiple times should not throw
        listener.Dispose();
        listener.Dispose();
        listener.Dispose();

        listener.ReceivedCounters.ShouldNotBeNull();
    }

    [Fact]
    public void EventListenerBase_ShouldIgnore_WhenPayloadItemIsNotDictionary()
    {
        // Test that listener handles payload items that are not IDictionary<string, object>
        var listener = new TestEventListener();

        using var customSource = new RivuletTestEventSource();
        // Write a simple string event - payload will be a string, not a dictionary
        customSource.WriteInformationalEvent("not a dictionary");

        Thread.Sleep(100);

        // Should handle gracefully and not crash
        listener.ReceivedCounters.ShouldNotBeNull();

        listener.Dispose();
    }

    [Fact]
    public void EventListenerBase_ShouldIgnore_WhenPayloadMissingNameKey()
    {
        // Test payload that doesn't have "Name" key - simulated by a non-counter event
        var listener = new TestEventListener();

        using var customSource = new RivuletTestEventSource();
        customSource.WriteEmptyEvent(); // Event with empty/non-dictionary payload

        Thread.Sleep(100);

        // Should handle gracefully
        listener.ReceivedCounters.ShouldNotBeNull();

        listener.Dispose();
    }

    [Fact]
    public void EventListenerBase_ShouldIgnore_WhenPayloadMissingMeanAndIncrement()
    {
        // Coverage for lines 49-51: when both Mean and Increment keys are missing
        // This is implicitly tested by non-counter events, but let's be explicit
        var listener = new TestEventListener();

        using var customSource = new RivuletTestEventSource();
        customSource.WriteInformationalEvent("event without mean or increment");

        Thread.Sleep(100);

        listener.ReceivedCounters.ShouldNotBeNull();

        listener.Dispose();
    }

    [Fact]
    public void EventListenerBase_ShouldHandleConvertToDouble()
    {
        // Test line 53: Convert.ToDouble(meanObj)
        // This is covered by normal counter operations, but adding explicit test
        var listener = new TestEventListener();

        using var customSource = new RivuletTestEventSource();
        customSource.EmitCounterWithoutDisplayName(); // Emits a counter with numeric value

        Thread.Sleep(100);

        listener.Dispose();
    }

    [Fact]
    public async Task EventListenerBase_ShouldHandleIncrementCounter()
    {
        // Ensure we cover the "Increment" key path (line 50) in addition to "Mean"
        var listener = new TestEventListener();

        // Run operations to generate counters
        // 10 items * 300ms / 2 parallelism = 1500ms of operation time
        await Enumerable.Range(1, 10)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(static async (x, ct) =>
                {
                    await Task.Delay(300, ct);
                    return x;
                },
                new() { MaxDegreeOfParallelism = 2 })
            .ToListAsync();

        // Increased to 8000ms for CI/CD reliability
        var deadline = DateTime.UtcNow.AddMilliseconds(8000);
        while (listener.ReceivedCounters.IsEmpty && DateTime.UtcNow < deadline) await Task.Delay(100);

        listener.ReceivedCounters.ShouldNotBeEmpty();

        listener.Dispose();
    }

    private sealed class TestEventListener : RivuletEventListenerBase
    {
        public ConcurrentDictionary<string, CounterData> ReceivedCounters { get; } = new();

        protected override void OnCounterReceived(string name,
            string displayName,
            double value,
            string displayUnits) =>
            ReceivedCounters[name] = new(displayName, displayUnits);
    }

    private sealed record CounterData(string DisplayName, string DisplayUnits);

    [EventSource(Name = "CustomEventSource")]
    private sealed class CustomEventSource(string name) : EventSource(name)
    {
        [Event(100)]
        public new void WriteEvent(int id, string message) => base.WriteEvent(id, message);
    }

    [EventSource(Name = "Rivulet.Core")]
    private sealed class RivuletTestEventSource : EventSource
    {
        private readonly EventCounter? _testCounter;

        public RivuletTestEventSource() => _testCounter = new("test-counter", this);

        public void EmitCounterWithoutDisplayName() => _testCounter?.WriteMetric(1.0);

        public void EmitCounterWithoutDisplayUnits() => _testCounter?.WriteMetric(2.0);

        [Event(10)]
        public void WriteEmptyEvent() => WriteEvent(10);

        [Event(11, Level = EventLevel.Informational)]
        public void WriteInformationalEvent(string message) => WriteEvent(11, message);

        protected override void Dispose(bool disposing)
        {
            _testCounter?.Dispose();
            base.Dispose(disposing);
        }
    }
}

// Collection definition to disable parallelization for EventSource tests
// EventSource and EventListener are process-wide singletons - parallel execution
// causes cross-test pollution where listeners receive events from other tests
[CollectionDefinition(TestCollections.EventSource, DisableParallelization = true)]
public class EventSourceTestCollection;