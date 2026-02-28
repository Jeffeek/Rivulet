using Rivulet.Core;
using Rivulet.Core.Observability;

namespace Rivulet.Diagnostics.Tests;

[Collection(TestCollections.SerialEventSource)]
public sealed class MetricsAggregatorTests
{
    [Fact]
    public async Task MetricsAggregator_ShouldAggregateMetrics_OverTimeWindow()
    {
        var aggregatedMetrics = new List<IReadOnlyList<AggregatedMetrics>>();
        var lockObj = new object();

        await using var aggregator = new MetricsAggregator(TimeSpan.FromMilliseconds(500));
        aggregator.OnAggregation += metrics =>
        {
            lock (lockObj) aggregatedMetrics.Add(metrics);
        };

        // Use longer operation (200ms per item) to ensure EventCounters poll DURING execution
        // EventCounters have ~1 second polling interval, so operation needs to run for 1-2+ seconds
        // 5 items * 200ms / 2 parallelism = 500ms (0.5 second) of operation time
        await Enumerable.Range(1, 5)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(static async (x, ct) =>
                {
                    await Task.Delay(200, ct);
                    return x * 2;
                },
                new() { MaxDegreeOfParallelism = 2 })
            .ToListAsync(TestContext.Current.CancellationToken);

        // Wait for EventCounters to poll and write metrics, then for aggregation window to fire
        await Task.Delay(5000, CancellationToken.None);

        // Take a thread-safe snapshot to avoid race conditions
        IReadOnlyList<AggregatedMetrics> lastAggregation;
        lock (lockObj)
        {
            aggregatedMetrics.ShouldNotBeEmpty();
            lastAggregation = aggregatedMetrics.Last();
        }

        lastAggregation.ShouldNotBeEmpty();

        var itemsStartedMetric =
            lastAggregation.FirstOrDefault(static m => m.Name == RivuletMetricsConstants.CounterNames.ItemsStarted);
        itemsStartedMetric.ShouldNotBeNull();
        itemsStartedMetric.Min.ShouldBeGreaterThanOrEqualTo(0);
        itemsStartedMetric.Max.ShouldBeGreaterThanOrEqualTo(itemsStartedMetric.Min);
        itemsStartedMetric.Average.ShouldBeGreaterThanOrEqualTo(0);
        itemsStartedMetric.SampleCount.ShouldBeGreaterThan(0);
        itemsStartedMetric.Timestamp.ShouldBe(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task MetricsAggregator_ShouldCalculateCorrectStatistics()
    {
        var aggregatedMetrics = new List<IReadOnlyList<AggregatedMetrics>>();
        var lockObj = new object();

        await using var aggregator = new MetricsAggregator(TimeSpan.FromSeconds(2));
        aggregator.OnAggregation += metrics =>
        {
            if (metrics.Count <= 0) return;

            lock (lockObj) aggregatedMetrics.Add(metrics);
        };

        // Operations must run long enough for EventCounter polling (1 second interval)
        // 5 items * 200ms / 2 parallelism = 500ms minimum operation time
        await Enumerable.Range(1, 5)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(static async (x, ct) =>
                {
                    await Task.Delay(200, ct);
                    return x;
                },
                new() { MaxDegreeOfParallelism = 2 })
            .ToListAsync(TestContext.Current.CancellationToken);

        // Wait for at least 2x the aggregation window to ensure timer fires reliably
        await Task.Delay(3000, CancellationToken.None); // Fixed delay for timer-based aggregation

        // Take a thread-safe snapshot of the list to avoid race conditions
        List<IReadOnlyList<AggregatedMetrics>> snapshot;
        lock (lockObj) snapshot = aggregatedMetrics.ToList();

        snapshot.ShouldNotBeEmpty();

        // Check all aggregations, not just the last one, to avoid timing issues
        foreach (var aggregation in snapshot)
        {
            aggregation.ShouldNotBeEmpty();

            foreach (var metric in aggregation)
            {
                metric.Min.ShouldBeLessThanOrEqualTo(metric.Max);
                metric.Average.ShouldBeInRange(metric.Min, metric.Max);
                metric.Current.ShouldBeInRange(metric.Min, metric.Max);
                metric.SampleCount.ShouldBeGreaterThan(0);
            }
        }
    }

    [Fact]
    public async Task MetricsAggregator_ShouldHandleExpiredSamples()
    {
        var aggregatedMetrics = new List<IReadOnlyList<AggregatedMetrics>>();
        var lockObj = new object();

        await using var aggregator = new MetricsAggregator(TimeSpan.FromSeconds(1)); // 1s aggregation window
        aggregator.OnAggregation += metrics =>
        {
            if (metrics.Count <= 0) return;

            lock (lockObj) aggregatedMetrics.Add(metrics);
        };

        // Use longer operation to ensure EventCounters have time to poll and emit metrics
        await Enumerable.Range(1, 20)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(static async (x, ct) =>
                {
                    await Task.Delay(50, ct);
                    return x;
                },
                new() { MaxDegreeOfParallelism = 2 })
            .ToListAsync(TestContext.Current.CancellationToken);

        // Wait for EventCounters to poll (~1s interval) + aggregation timer to fire
        await Task.Delay(2000, CancellationToken.None); // Fixed delay for timer-based aggregation

        // Take a thread-safe snapshot for initial checks
        IReadOnlyList<AggregatedMetrics> firstAggregation;
        lock (lockObj)
        {
            aggregatedMetrics.ShouldNotBeEmpty("aggregation should have captured metrics after sufficient wait time");
            firstAggregation = aggregatedMetrics.First();
        }

        firstAggregation.ShouldNotBeEmpty();

        // Wait for another aggregation window to potentially expire samples
        // This verifies that the aggregator handles sample expiration gracefully
        await Task.Delay(2000, CancellationToken.None);

        // Take a thread-safe snapshot for final checks
        List<IReadOnlyList<AggregatedMetrics>> snapshot;
        int totalAggregations;
        lock (lockObj)
        {
            totalAggregations = aggregatedMetrics.Count;
            snapshot = aggregatedMetrics.ToList();
        }

        totalAggregations.ShouldBeGreaterThanOrEqualTo(1);

        // Verify all captured aggregations have valid data
        foreach (var aggregation in snapshot)
        {
            aggregation.ShouldNotBeEmpty();
            foreach (var metric in aggregation)
            {
                metric.SampleCount.ShouldBeGreaterThan(0);
                metric.Min.ShouldBeLessThanOrEqualTo(metric.Max);
            }
        }
    }

    [Fact]
    public void MetricsAggregator_ShouldNotThrow_WhenDisposed()
    {
        var aggregator = new MetricsAggregator();
        var act = () => aggregator.Dispose();
        act.ShouldNotThrow();
    }

    [Fact]
    public void MetricsAggregator_ShouldNotThrow_WhenDoubleDisposed()
    {
        var aggregator = new MetricsAggregator();
        aggregator.Dispose();

        // Second dispose should not throw (tests disposal guard at line 137)
        var act = () => aggregator.Dispose();
        act.ShouldNotThrow();
    }

    [Fact]
    public async Task MetricsAggregator_ShouldNotThrow_WhenDoubleAsyncDisposed()
    {
        var aggregator = new MetricsAggregator();
        await aggregator.DisposeAsync();

        // Second dispose should not throw (tests disposal guard at line 155)
        var act = async () => await aggregator.DisposeAsync();
        await act.ShouldNotThrowAsync();
    }
}
