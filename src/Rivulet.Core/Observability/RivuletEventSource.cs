using System.Diagnostics.Tracing;

namespace Rivulet.Core.Observability;

/// <summary>
/// EventSource for exposing Rivulet.Core runtime metrics via EventCounters.
/// Provides zero-cost monitoring when not actively listening.
/// </summary>
/// <remarks>
/// Metrics are exposed as EventCounters and can be monitored using:
/// - dotnet-counters: dotnet-counters monitor --process-id [PID] --counters Rivulet.Core
/// - Application Insights
/// - Prometheus exporters
/// - Custom EventListener implementations
/// </remarks>
[EventSource(Name = RivuletSharedConstants.RivuletCore)]
internal sealed class RivuletEventSource : EventSource
{
    /// <summary>
    /// Singleton instance of the EventSource.
    /// </summary>
    public static readonly RivuletEventSource Log = new();

    private PollingCounter? _itemsStartedCounter;
    private PollingCounter? _itemsCompletedCounter;
    private PollingCounter? _totalRetriesCounter;
    private PollingCounter? _totalFailuresCounter;
    private PollingCounter? _throttleEventsCounter;
    private PollingCounter? _drainEventsCounter;

    private long _itemsStarted;
    private long _itemsCompleted;
    private long _totalRetries;
    private long _totalFailures;
    private long _throttleEvents;
    private long _drainEvents;

    private RivuletEventSource()
    {
    }

    /// <summary>
    /// Increments the count of items that have started processing.
    /// </summary>
    public void IncrementItemsStarted() => Interlocked.Increment(ref _itemsStarted);

    /// <summary>
    /// Increments the count of items that have completed processing.
    /// </summary>
    public void IncrementItemsCompleted() => Interlocked.Increment(ref _itemsCompleted);

    /// <summary>
    /// Increments the total retry count.
    /// </summary>
    public void IncrementRetries() => Interlocked.Increment(ref _totalRetries);

    /// <summary>
    /// Increments the total failure count.
    /// </summary>
    public void IncrementFailures() => Interlocked.Increment(ref _totalFailures);

    /// <summary>
    /// Increments the throttle event count.
    /// </summary>
    public void IncrementThrottleEvents() => Interlocked.Increment(ref _throttleEvents);

    /// <summary>
    /// Increments the drain event count.
    /// </summary>
    public void IncrementDrainEvents() => Interlocked.Increment(ref _drainEvents);


    /// <summary>
    /// Gets the current count of items started.
    /// </summary>
    public long GetItemsStarted() => Interlocked.Read(ref _itemsStarted);

    /// <summary>
    /// Gets the current count of items completed.
    /// </summary>
    public long GetItemsCompleted() => Interlocked.Read(ref _itemsCompleted);

    /// <summary>
    /// Gets the current total retry count.
    /// </summary>
    public long GetTotalRetries() => Interlocked.Read(ref _totalRetries);

    /// <summary>
    /// Gets the current total failure count.
    /// </summary>
    public long GetTotalFailures() => Interlocked.Read(ref _totalFailures);

    /// <summary>
    /// Gets the current throttle event count.
    /// </summary>
    public long GetThrottleEvents() => Interlocked.Read(ref _throttleEvents);

    /// <summary>
    /// Gets the current drain event count.
    /// </summary>
    public long GetDrainEvents() => Interlocked.Read(ref _drainEvents);

    /// <summary>
    /// Called when EventSource is enabled. Creates EventCounters for monitoring.
    /// </summary>
    protected override void OnEventCommand(EventCommandEventArgs command)
    {
        if (command.Command != EventCommand.Enable) return;

        _itemsStartedCounter ??= new(
            RivuletMetricsConstants.CounterNames.ItemsStarted,
            this,
            () => GetItemsStarted())
        {
            DisplayName = RivuletMetricsConstants.DisplayNames.ItemsStarted,
            DisplayUnits = RivuletMetricsConstants.DisplayUnits.Items
        };

        _itemsCompletedCounter ??= new(
            RivuletMetricsConstants.CounterNames.ItemsCompleted,
            this,
            () => GetItemsCompleted())
        {
            DisplayName = RivuletMetricsConstants.DisplayNames.ItemsCompleted,
            DisplayUnits = RivuletMetricsConstants.DisplayUnits.Items
        };

        _totalRetriesCounter ??= new(
            RivuletMetricsConstants.CounterNames.TotalRetries,
            this,
            () => GetTotalRetries())
        {
            DisplayName = RivuletMetricsConstants.DisplayNames.TotalRetries,
            DisplayUnits = RivuletMetricsConstants.DisplayUnits.Retries
        };

        _totalFailuresCounter ??= new(
            RivuletMetricsConstants.CounterNames.TotalFailures,
            this,
            () => GetTotalFailures())
        {
            DisplayName = RivuletMetricsConstants.DisplayNames.TotalFailures,
            DisplayUnits = RivuletMetricsConstants.DisplayUnits.Failures
        };

        _throttleEventsCounter ??= new(
            RivuletMetricsConstants.CounterNames.ThrottleEvents,
            this,
            () => GetThrottleEvents())
        {
            DisplayName = RivuletMetricsConstants.DisplayNames.ThrottleEvents,
            DisplayUnits = RivuletMetricsConstants.DisplayUnits.Events
        };

        _drainEventsCounter ??= new(
            RivuletMetricsConstants.CounterNames.DrainEvents,
            this,
            () => GetDrainEvents())
        {
            DisplayName = RivuletMetricsConstants.DisplayNames.DrainEvents,
            DisplayUnits = RivuletMetricsConstants.DisplayUnits.Events
        };
    }

    protected override void Dispose(bool disposing)
    {
        _itemsStartedCounter?.Dispose();
        _itemsCompletedCounter?.Dispose();
        _totalRetriesCounter?.Dispose();
        _totalFailuresCounter?.Dispose();
        _throttleEventsCounter?.Dispose();
        _drainEventsCounter?.Dispose();
        base.Dispose(disposing);
    }
}
