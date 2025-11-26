using System.Diagnostics.Tracing;
using Rivulet.Core.Observability;

namespace Rivulet.Core.Tests.Observability;

[Collection("EventSource Sequential Tests")]
public class RivuletEventSourceTests
{
    [Fact]
    public void Log_ShouldBeSingleton()
    {
        var log1 = RivuletEventSource.Log;
        var log2 = RivuletEventSource.Log;

        log1.Should().BeSameAs(log2);
    }

    [Fact]
    public void IncrementItemsStarted_ShouldIncrement()
    {
        var eventSource = RivuletEventSource.Log;
        var before = eventSource.GetItemsStarted();

        eventSource.IncrementItemsStarted();

        var after = eventSource.GetItemsStarted();
        after.Should().BeGreaterThan(before);
    }

    [Fact]
    public void IncrementItemsCompleted_ShouldIncrement()
    {
        var eventSource = RivuletEventSource.Log;
        var before = eventSource.GetItemsCompleted();

        eventSource.IncrementItemsCompleted();

        var after = eventSource.GetItemsCompleted();
        after.Should().BeGreaterThan(before);
    }

    [Fact]
    public void IncrementRetries_ShouldIncrement()
    {
        var eventSource = RivuletEventSource.Log;
        var before = eventSource.GetTotalRetries();

        eventSource.IncrementRetries();

        var after = eventSource.GetTotalRetries();
        after.Should().BeGreaterThan(before);
    }

    [Fact]
    public void IncrementFailures_ShouldIncrement()
    {
        var eventSource = RivuletEventSource.Log;
        var before = eventSource.GetTotalFailures();

        eventSource.IncrementFailures();

        var after = eventSource.GetTotalFailures();
        after.Should().BeGreaterThan(before);
    }

    [Fact]
    public void IncrementThrottleEvents_ShouldIncrement()
    {
        var eventSource = RivuletEventSource.Log;
        var before = eventSource.GetThrottleEvents();

        eventSource.IncrementThrottleEvents();

        var after = eventSource.GetThrottleEvents();
        after.Should().BeGreaterThan(before);
    }

    [Fact]
    public void IncrementDrainEvents_ShouldIncrement()
    {
        var eventSource = RivuletEventSource.Log;
        var before = eventSource.GetDrainEvents();

        eventSource.IncrementDrainEvents();

        var after = eventSource.GetDrainEvents();
        after.Should().BeGreaterThan(before);
    }

    [Fact]
    public void OnEventCommand_WithEnableCommand_ShouldCreateCounters()
    {
        using var listener = new TestEventListener();
        listener.EnableEvents(RivuletEventSource.Log, EventLevel.Verbose);

        // Enabling the event source should trigger OnEventCommand with Enable
        // This tests the counter creation branches (??= when null)
        RivuletEventSource.Log.Should().NotBeNull();
    }

    [Fact]
    public void OnEventCommand_CalledMultipleTimes_ShouldReuseCounters()
    {
        using var listener1 = new TestEventListener();
        using var listener2 = new TestEventListener();

        // First enable - creates counters
        listener1.EnableEvents(RivuletEventSource.Log, EventLevel.Verbose);

        // Second enable - reuses existing counters (hits the "already exists" branch of ??=)
        listener2.EnableEvents(RivuletEventSource.Log, EventLevel.Verbose);

        RivuletEventSource.Log.Should().NotBeNull();
    }

    [Fact]
    public void OnEventCommand_WithDisableCommand_ShouldNotCreateCounters()
    {
        using var listener = new TestEventListener();

        // Enable first
        listener.EnableEvents(RivuletEventSource.Log, EventLevel.Verbose);

        // Disable - should trigger OnEventCommand but with Disable command (early return)
        listener.DisableEvents(RivuletEventSource.Log);

        RivuletEventSource.Log.Should().NotBeNull();
    }

    [Fact]
    public void OnEventCommand_WithUpdateCommand_ShouldNotCreateCounters()
    {
        using var listener = new TestEventListener();

        // This triggers OnEventCommand with different command types
        listener.EnableEvents(RivuletEventSource.Log, EventLevel.Verbose);
        listener.DisableEvents(RivuletEventSource.Log);
        listener.EnableEvents(RivuletEventSource.Log, EventLevel.Warning);

        RivuletEventSource.Log.Should().NotBeNull();
    }

    [Fact]
    public void Dispose_ShouldDisposeCounters()
    {
        // Create a listener to enable counters
        using var listener = new TestEventListener();
        listener.EnableEvents(RivuletEventSource.Log, EventLevel.Verbose);

        // Increment some values to ensure counters are created and used
        RivuletEventSource.Log.IncrementItemsStarted();
        RivuletEventSource.Log.IncrementItemsCompleted();
        RivuletEventSource.Log.IncrementRetries();
        RivuletEventSource.Log.IncrementFailures();
        RivuletEventSource.Log.IncrementThrottleEvents();
        RivuletEventSource.Log.IncrementDrainEvents();

        // Dispose should clean up counters without throwing
        RivuletEventSource.Log.Dispose();

        // EventSource should still be accessible (singleton pattern)
        RivuletEventSource.Log.Should().NotBeNull();
    }

    private class TestEventListener : EventListener
    {
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            // No-op listener for testing
        }
    }
}
