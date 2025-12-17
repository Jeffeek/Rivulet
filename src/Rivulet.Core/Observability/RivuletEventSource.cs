using System.Diagnostics.Tracing;

namespace Rivulet.Core.Observability;

/// <summary>
///     EventSource for exposing Rivulet.Core runtime metrics via EventCounters.
///     Provides zero-cost monitoring when not actively listening.
/// </summary>
/// <remarks>
///     Metrics are exposed as EventCounters and can be monitored using:
///     - dotnet-counters: dotnet-counters monitor --process-id [PID] --counters Rivulet.Core
///     - Application Insights
///     - Prometheus exporters
///     - Custom EventListener implementations
/// </remarks>
[EventSource(Name = RivuletSharedConstants.RivuletCore)]
internal sealed class RivuletEventSource : EventSource
{
    /// <summary>
    ///     Singleton instance of the EventSource.
    /// </summary>
    public static readonly RivuletEventSource Log = new();
    private readonly PollingCounter _drainEventsCounter;
    private readonly PollingCounter _itemsCompletedCounter;

    private readonly PollingCounter _itemsStartedCounter;
    private readonly PollingCounter _throttleEventsCounter;
    private readonly PollingCounter _totalFailuresCounter;
    private readonly PollingCounter _totalRetriesCounter;
    private long _drainEvents;
    private long _itemsCompleted;

    private long _itemsStarted;
    private long _throttleEvents;
    private long _totalFailures;
    private long _totalRetries;

    private RivuletEventSource()
    {
        // Create counters in constructor so they exist before EnableEvents is called.
        // This ensures they are registered and will be polled when a listener enables the EventSource.
        _itemsStartedCounter = new(
            RivuletMetricsConstants.CounterNames.ItemsStarted,
            this,
            () => GetItemsStarted())
        {
            DisplayName = RivuletMetricsConstants.DisplayNames.ItemsStarted,
            DisplayUnits = RivuletMetricsConstants.DisplayUnits.Items
        };

        _itemsCompletedCounter = new(
            RivuletMetricsConstants.CounterNames.ItemsCompleted,
            this,
            () => GetItemsCompleted())
        {
            DisplayName = RivuletMetricsConstants.DisplayNames.ItemsCompleted,
            DisplayUnits = RivuletMetricsConstants.DisplayUnits.Items
        };

        _totalRetriesCounter = new(
            RivuletMetricsConstants.CounterNames.TotalRetries,
            this,
            () => GetTotalRetries())
        {
            DisplayName = RivuletMetricsConstants.DisplayNames.TotalRetries,
            DisplayUnits = RivuletMetricsConstants.DisplayUnits.Retries
        };

        _totalFailuresCounter = new(
            RivuletMetricsConstants.CounterNames.TotalFailures,
            this,
            () => GetTotalFailures())
        {
            DisplayName = RivuletMetricsConstants.DisplayNames.TotalFailures,
            DisplayUnits = RivuletMetricsConstants.DisplayUnits.Failures
        };

        _throttleEventsCounter = new(
            RivuletMetricsConstants.CounterNames.ThrottleEvents,
            this,
            () => GetThrottleEvents())
        {
            DisplayName = RivuletMetricsConstants.DisplayNames.ThrottleEvents,
            DisplayUnits = RivuletMetricsConstants.DisplayUnits.Events
        };

        _drainEventsCounter = new(
            RivuletMetricsConstants.CounterNames.DrainEvents,
            this,
            () => GetDrainEvents())
        {
            DisplayName = RivuletMetricsConstants.DisplayNames.DrainEvents,
            DisplayUnits = RivuletMetricsConstants.DisplayUnits.Events
        };
    }

    /// <summary>
    ///     Increments the count of items that have started processing.
    /// </summary>
    public void IncrementItemsStarted() => Interlocked.Increment(ref _itemsStarted);

    /// <summary>
    ///     Increments the count of items that have completed processing.
    /// </summary>
    public void IncrementItemsCompleted() => Interlocked.Increment(ref _itemsCompleted);

    /// <summary>
    ///     Increments the total retry count.
    /// </summary>
    public void IncrementRetries() => Interlocked.Increment(ref _totalRetries);

    /// <summary>
    ///     Increments the total failure count.
    /// </summary>
    public void IncrementFailures() => Interlocked.Increment(ref _totalFailures);

    /// <summary>
    ///     Increments the throttle event count.
    /// </summary>
    public void IncrementThrottleEvents() => Interlocked.Increment(ref _throttleEvents);

    /// <summary>
    ///     Increments the drain event count.
    /// </summary>
    public void IncrementDrainEvents() => Interlocked.Increment(ref _drainEvents);

    /// <summary>
    ///     Logs a callback failure for diagnostics.
    /// </summary>
    /// <param name="callbackName">Name of the callback that failed.</param>
    /// <param name="exceptionType">Type of the exception.</param>
    /// <param name="message">Exception message.</param>
    /// <remarks>
    ///     Event ID 100 is used to avoid conflicts with internal EventCounter event IDs (0-3).
    /// </remarks>
    [Event(100, Level = EventLevel.Warning, Message = "Callback '{0}' failed with {1}: {2}")]
    public void CallbackFailed(string callbackName, string exceptionType, string message) =>
        WriteEvent(100, callbackName, exceptionType, message);


    /// <summary>
    ///     Gets the current count of items started.
    /// </summary>
    public long GetItemsStarted() => Interlocked.Read(ref _itemsStarted);

    /// <summary>
    ///     Gets the current count of items completed.
    /// </summary>
    public long GetItemsCompleted() => Interlocked.Read(ref _itemsCompleted);

    /// <summary>
    ///     Gets the current total retry count.
    /// </summary>
    public long GetTotalRetries() => Interlocked.Read(ref _totalRetries);

    /// <summary>
    ///     Gets the current total failure count.
    /// </summary>
    public long GetTotalFailures() => Interlocked.Read(ref _totalFailures);

    /// <summary>
    ///     Gets the current throttle event count.
    /// </summary>
    public long GetThrottleEvents() => Interlocked.Read(ref _throttleEvents);

    /// <summary>
    ///     Gets the current drain event count.
    /// </summary>
    public long GetDrainEvents() => Interlocked.Read(ref _drainEvents);

    protected override void Dispose(bool disposing)
    {
        _itemsStartedCounter.Dispose();
        _itemsCompletedCounter.Dispose();
        _totalRetriesCounter.Dispose();
        _totalFailuresCounter.Dispose();
        _throttleEventsCounter.Dispose();
        _drainEventsCounter.Dispose();
        base.Dispose(disposing);
    }
}