namespace Rivulet.Core.Observability;

/// <summary>
/// Lightweight no-op implementation of metrics tracker.
/// Used when user doesn't need custom metrics collection (MetricsOptions is null).
/// Still fires EventSource events for diagnostics/telemetry but avoids heavy allocations.
/// </summary>
internal sealed class NoOpMetricsTracker : MetricsTrackerBase
{
    /// <summary>
    /// Singleton instance to avoid any allocations.
    /// </summary>
    public static readonly NoOpMetricsTracker Instance = new();

    // Private constructor to enforce singleton pattern
    private NoOpMetricsTracker() { }

    /// <summary>
    /// Fires EventSource event only (no tracking).
    /// </summary>
    public override void IncrementItemsStarted()
    {
        RivuletEventSource.Log.IncrementItemsStarted();
    }

    /// <summary>
    /// Fires EventSource event only (no tracking).
    /// </summary>
    public override void IncrementItemsCompleted()
    {
        RivuletEventSource.Log.IncrementItemsCompleted();
    }

    /// <summary>
    /// Fires EventSource event only (no tracking).
    /// </summary>
    public override void IncrementRetries()
    {
        RivuletEventSource.Log.IncrementRetries();
    }

    /// <summary>
    /// Fires EventSource event only (no tracking).
    /// </summary>
    public override void IncrementFailures()
    {
        RivuletEventSource.Log.IncrementFailures();
    }

    /// <summary>
    /// Fires EventSource event only (no tracking).
    /// </summary>
    public override void IncrementThrottleEvents()
    {
        RivuletEventSource.Log.IncrementThrottleEvents();
    }

    /// <summary>
    /// Fires EventSource event only (no tracking).
    /// </summary>
    public override void IncrementDrainEvents()
    {
        RivuletEventSource.Log.IncrementDrainEvents();
    }

    /// <summary>
    /// No-op (no tracking needed).
    /// </summary>
    public override void SetActiveWorkers(int count)
    {
        // No-op: no metrics to track
    }

    /// <summary>
    /// No-op (no tracking needed).
    /// </summary>
    public override void SetQueueDepth(int depth)
    {
        // No-op: no metrics to track
    }

    /// <summary>
    /// No-op (nothing to dispose).
    /// </summary>
    public override ValueTask DisposeAsync()
    {
        // No-op: no resources to dispose
        return ValueTask.CompletedTask;
    }
}
