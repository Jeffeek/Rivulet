using System.Collections.Concurrent;

namespace Rivulet.Diagnostics;

/// <summary>
/// Represents a snapshot of aggregated metrics over a specific time window.
/// </summary>
public sealed class AggregatedMetrics
{
    /// <summary>
    /// Gets the programmatic name of the metric.
    /// </summary>
    public required string Name { get; init; }
    /// <summary>
    /// Gets the human-readable display name of the metric.
    /// </summary>
    public required string DisplayName { get; init; }
    /// <summary>
    /// Gets the display units for the metric (e.g., "items", "ms").
    /// </summary>
    public required string DisplayUnits { get; init; }
    /// <summary>
    /// Gets the minimum value observed for the metric during the aggregation window.
    /// </summary>
    public double Min { get; init; }
    /// <summary>
    /// Gets the maximum value observed for the metric during the aggregation window.
    /// </summary>
    public double Max { get; init; }
    /// <summary>
    /// Gets the average value of the metric during the aggregation window.
    /// </summary>
    public double Average { get; init; }
    /// <summary>
    /// Gets the most recent value of the metric.
    /// </summary>
    public double Current { get; init; }
    /// <summary>
    /// Gets the number of samples collected for the metric during the aggregation window.
    /// </summary>
    public int SampleCount { get; init; }
    /// <summary>
    /// Gets the timestamp when the metrics were aggregated.
    /// </summary>
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// Aggregates Rivulet metrics over a specified time window and provides aggregated statistics.
/// </summary>
public sealed class MetricsAggregator : RivuletEventListenerBase
{
    private readonly TimeSpan _aggregationWindow;
    private readonly ConcurrentDictionary<string, List<(double Value, DateTime Timestamp)>> _samples = new();
    private readonly ConcurrentDictionary<string, (string DisplayName, string DisplayUnits)> _metricMetadata = new();
    private readonly Timer _aggregationTimer;

    /// <summary>
    /// Occurs when metrics are aggregated after each aggregation window.
    /// </summary>
    public event Action<IReadOnlyList<AggregatedMetrics>>? OnAggregation;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetricsAggregator"/> class.
    /// </summary>
    /// <param name="aggregationWindow">The time window for aggregation. Defaults to 10 seconds if not specified.</param>
    public MetricsAggregator(TimeSpan? aggregationWindow = null)
    {
        _aggregationWindow = aggregationWindow ?? TimeSpan.FromSeconds(10);
        _aggregationTimer = new Timer(AggregateMetrics, null, _aggregationWindow, _aggregationWindow);
    }

    /// <summary>
    /// Called when a counter value is received from Rivulet.Core.
    /// </summary>
    /// <param name="name">The programmatic name of the counter.</param>
    /// <param name="displayName">The human-readable display name of the counter.</param>
    /// <param name="value">The current value of the counter.</param>
    /// <param name="displayUnits">The display units of the counter.</param>
    protected override void OnCounterReceived(string name, string displayName, double value, string displayUnits)
    {
        var samples = _samples.GetOrAdd(name, _ => new List<(double, DateTime)>());

        lock (samples)
        {
            _metricMetadata.TryAdd(name, (displayName, displayUnits));
            samples.Add((value, DateTime.UtcNow));
        }
    }

    private void AggregateMetrics(object? state)
    {
        var cutoffTime = DateTime.UtcNow - _aggregationWindow;
        var aggregatedMetrics = new List<AggregatedMetrics>();

        foreach (var (name, samples) in _samples)
        {
            lock (samples)
            {
                samples.RemoveAll(s => s.Timestamp < cutoffTime);
                if (samples.Count == 0)
                    continue;

                var values = samples.Select(s => s.Value).ToList();
                var metadata = _metricMetadata.GetValueOrDefault(name, (name, string.Empty));

                aggregatedMetrics.Add(new AggregatedMetrics
                {
                    Name = name,
                    DisplayName = metadata.Item1,
                    DisplayUnits = metadata.Item2,
                    Min = values.Min(),
                    Max = values.Max(),
                    Average = values.Average(),
                    Current = values.Last(),
                    SampleCount = values.Count,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        if (aggregatedMetrics.Count > 0)
        {
            OnAggregation?.Invoke(aggregatedMetrics);
        }
    }

    /// <summary>
    /// Disposes the metrics aggregator and its internal timer.
    /// </summary>
    public override void Dispose()
    {
        // Wait for any pending timer callbacks to complete before final aggregation
        using var waitHandle = new ManualResetEvent(false);
        _aggregationTimer.Dispose(waitHandle);
        waitHandle.WaitOne();

        AggregateMetrics(null);
        base.Dispose();
    }
}
