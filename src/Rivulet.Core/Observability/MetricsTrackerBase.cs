namespace Rivulet.Core.Observability;

/// <summary>
/// Base class for metrics tracking implementations.
/// Provides a common interface for both active and no-op trackers.
/// </summary>
internal abstract class MetricsTrackerBase : IDisposable
{
    /// <summary>
    /// Increments the count of items started.
    /// </summary>
    public abstract void IncrementItemsStarted();

    /// <summary>
    /// Increments the count of items completed.
    /// </summary>
    public abstract void IncrementItemsCompleted();

    /// <summary>
    /// Increments the retry count.
    /// </summary>
    public abstract void IncrementRetries();

    /// <summary>
    /// Increments the failure count.
    /// </summary>
    public abstract void IncrementFailures();

    /// <summary>
    /// Increments the throttle events count.
    /// </summary>
    public abstract void IncrementThrottleEvents();

    /// <summary>
    /// Increments the drain events count.
    /// </summary>
    public abstract void IncrementDrainEvents();

    /// <summary>
    /// Sets the current number of active workers.
    /// </summary>
    public abstract void SetActiveWorkers(int count);

    /// <summary>
    /// Sets the current queue depth.
    /// </summary>
    public abstract void SetQueueDepth(int depth);

    /// <summary>
    /// Disposes resources used by the tracker.
    /// </summary>
    public abstract void Dispose();

    /// <summary>
    /// Factory method to create the appropriate tracker based on options.
    /// Returns a lightweight NoOpMetricsTracker if no custom metrics are needed.
    /// </summary>
    public static MetricsTrackerBase Create(MetricsOptions? options, CancellationToken cancellationToken)
    {
        return options?.OnMetricsSample is not null
            ? new MetricsTracker(options, cancellationToken)
            : NoOpMetricsTracker.Instance;
    }
}
