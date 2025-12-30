namespace Rivulet.Core.Observability;

/// <summary>
///     Configuration options for runtime metrics collection during parallel operations.
/// </summary>
public sealed class MetricsOptions
{
    /// <summary>
    ///     Gets the interval at which metrics snapshots are sampled and reported.
    ///     Defaults to 10 seconds to balance observability with overhead.
    /// </summary>
    /// <remarks>
    ///     Shorter intervals provide more real-time visibility but increase overhead.
    ///     Longer intervals reduce overhead but may miss short-lived spikes.
    /// </remarks>
    public TimeSpan SampleInterval { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     Gets the callback function invoked when a metrics snapshot is sampled.
    ///     Receives a <see cref="MetricsSnapshot" /> containing current runtime metrics.
    /// </summary>
    /// <remarks>
    ///     This callback is executed asynchronously from a background task and should not block.
    ///     Common uses include:
    ///     - Exporting to monitoring systems (Prometheus, Application Insights, DataDog)
    ///     - Logging metrics for analysis
    ///     - Triggering alerts based on thresholds
    ///     - Updating dashboards or UI
    ///     The callback may be invoked from any thread and must be thread-safe.
    /// </remarks>
    public Func<MetricsSnapshot, ValueTask>? OnMetricsSample { get; init; }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MetricsOptions"/> class with default values.
    /// </summary>
    public MetricsOptions() { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MetricsOptions"/> class by copying values from another instance.
    /// </summary>
    /// <param name="original">The original instance to copy from. If null, default values are used.</param>
    public MetricsOptions(MetricsOptions? original)
    {
        if (original is null)
            return;

        SampleInterval = original.SampleInterval;
        OnMetricsSample = original.OnMetricsSample;
    }
}
