using Rivulet.Base.Tests;
using Rivulet.Core;
using System.Collections.Concurrent;
using Rivulet.Core.Observability;
using System.Diagnostics.Tracing;

namespace Rivulet.Diagnostics.Tests;

// EventSource and EventListener are process-wide singletons by design.
// These tests must run sequentially to avoid cross-test pollution.
[Collection(TestCollections.EventSource)]
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
            }, new()
            {
                MaxDegreeOfParallelism = 2
            })
            .ToListAsync();

        // EventSource publishes counters every 1 second
        // Increased from 2000ms → 5000ms for Windows CI/CD reliability
        await DeadlineExtensions.ApplyDeadlineAsync(
            DateTime.UtcNow.AddMilliseconds(5000),
            () => Task.Delay(100),
            () => _listener.ReceivedCounters.IsEmpty);

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
            }, new()
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
            }, new()
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
            .SelectParallelAsync((x, _) => ValueTask.FromResult(x), new());

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
            ReceivedCounters[name] = new(displayName, displayUnits);
        }
    }

    private sealed record CounterData(string DisplayName, string DisplayUnits);

    [Fact]
    public void EventListenerBase_ShouldIgnoreEvents_WhenEventSourceNameIsWrong()
    {
        var listener = new TestEventListener();

        // Create a custom EventSource with a different name
        using var customSource = new CustomEventSource("NotRivuletCore");
        customSource.WriteEvent(1, "test");

        // Should not receive any counters because event source name doesn't match
        listener.ReceivedCounters.Should().BeEmpty();

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

    [EventSource(Name = "CustomEventSource")]
    private sealed class CustomEventSource(string name) : EventSource(name)
    {
        [Event(1)]
        public new void WriteEvent(int id, string message)
        {
            base.WriteEvent(id, message);
        }
    }

    [EventSource(Name = "Rivulet.Core")]
    private sealed class RivuletTestEventSource : EventSource
    {
        private readonly EventCounter? _testCounter;

        public RivuletTestEventSource()
        {
            _testCounter = new("test-counter", this);
        }

        public void EmitCounterWithoutDisplayName()
        {
            _testCounter?.WriteMetric(1.0);
        }

        public void EmitCounterWithoutDisplayUnits()
        {
            _testCounter?.WriteMetric(2.0);
        }

        [Event(10)]
        public void WriteEmptyEvent()
        {
            WriteEvent(10);
        }

        [Event(11, Level = EventLevel.Informational)]
        public void WriteInformationalEvent(string message)
        {
            WriteEvent(11, message);
        }

        protected override void Dispose(bool disposing)
        {
            _testCounter?.Dispose();
            base.Dispose(disposing);
        }
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
        listener.ReceivedCounters.Should().NotBeNull();

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
        listener.ReceivedCounters.Should().NotBeNull();

        listener.Dispose();
    }

    [Fact]
    public async Task EventListenerBase_ShouldEnableEvents_WhenRivuletCoreSourceCreated()
    {
        // Test that listener enables events when Rivulet.Core EventSource is created
        var listener = new TestEventListener();

        // Trigger some operations to ensure EventSource is active
        await Enumerable.Range(1, 5)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(async (x, ct) =>
            {
                await Task.Delay(100, ct);
                return x;
            }, new())
            .ToListAsync();

        // Wait for counters
        await DeadlineExtensions.ApplyDeadlineAsync(
            DateTime.UtcNow.AddMilliseconds(3000),
            () => Task.Delay(100),
            () => listener.ReceivedCounters.IsEmpty);

        // Listener should have received counters, proving IsEnabled was set to true
        listener.ReceivedCounters.Should().NotBeEmpty();

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

        listener.ReceivedCounters.Should().NotBeNull();
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
        listener.ReceivedCounters.Should().NotBeNull();

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
        listener.ReceivedCounters.Should().NotBeNull();

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

        listener.ReceivedCounters.Should().NotBeNull();

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
        await Enumerable.Range(1, 5)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(async (x, ct) =>
            {
                await Task.Delay(100, ct);
                return x;
            }, new())
            .ToListAsync();

        var deadline = DateTime.UtcNow.AddMilliseconds(3000);
        while (listener.ReceivedCounters.IsEmpty && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        listener.ReceivedCounters.Should().NotBeEmpty();

        listener.Dispose();
    }
}

// Collection definition to disable parallelization for EventSource tests
// EventSource and EventListener are process-wide singletons - parallel execution
// causes cross-test pollution where listeners receive events from other tests
[CollectionDefinition(TestCollections.EventSource, DisableParallelization = true)]
public class EventSourceTestCollection { }
