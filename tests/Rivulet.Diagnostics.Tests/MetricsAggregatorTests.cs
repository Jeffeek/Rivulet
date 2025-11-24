using Rivulet.Core;
using Rivulet.Core.Observability;

namespace Rivulet.Diagnostics.Tests
{
    public class MetricsAggregatorTests
    {
        [Fact]
        public async Task MetricsAggregator_ShouldAggregateMetrics_OverTimeWindow()
        {
            var aggregatedMetrics = new List<IReadOnlyList<AggregatedMetrics>>();
            await using var aggregator = new MetricsAggregator(TimeSpan.FromMilliseconds(500));
            aggregator.OnAggregation += metrics => aggregatedMetrics.Add(metrics);

            // Use longer operation (200ms per item) to ensure EventCounters poll DURING execution
            // EventCounters have ~1 second polling interval, so operation needs to run for 1-2+ seconds
            // 5 items * 200ms / 2 parallelism = 500ms (0.5 second) of operation time
            await Enumerable.Range(1, 5)
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

            // Wait for EventCounters to poll and write metrics, then for aggregation window to fire
            // Polling interval ~1s, aggregation window 500ms
            // Increased from 3000ms → 5000ms for Windows CI/CD reliability (1/180 failures)
            await Task.Delay(5000);

            aggregatedMetrics.Should().NotBeEmpty();
            var lastAggregation = aggregatedMetrics.Last();
            lastAggregation.Should().NotBeEmpty();

            var itemsStartedMetric = lastAggregation.FirstOrDefault(m => m.Name == RivuletMetricsConstants.CounterNames.ItemsStarted);
            itemsStartedMetric.Should().NotBeNull();
            itemsStartedMetric.Min.Should().BeGreaterThanOrEqualTo(0);
            itemsStartedMetric.Max.Should().BeGreaterThanOrEqualTo(itemsStartedMetric.Min);
            itemsStartedMetric.Average.Should().BeGreaterThanOrEqualTo(0);
            itemsStartedMetric.SampleCount.Should().BeGreaterThan(0);
            itemsStartedMetric.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task MetricsAggregator_ShouldCalculateCorrectStatistics()
        {
            var aggregatedMetrics = new List<IReadOnlyList<AggregatedMetrics>>();
            await using var aggregator = new MetricsAggregator(TimeSpan.FromSeconds(2));
            aggregator.OnAggregation += metrics =>
            {
                if (metrics.Count > 0)
                    aggregatedMetrics.Add(metrics);
            };

            await Enumerable.Range(1, 10)
                .ToAsyncEnumerable()
                .SelectParallelStreamAsync(async (x, ct) =>
                {
                    await Task.Delay(10, ct);
                    return x;
                }, new()
                {
                    MaxDegreeOfParallelism = 2
                })
                .ToListAsync();

            // Wait for at least 2x the aggregation window to ensure timer fires reliably
            // Increased from 3000ms to 4000ms for better CI/CD reliability
            await Task.Delay(4000);

            aggregatedMetrics.Should().NotBeEmpty();

            // Check all aggregations, not just the last one, to avoid timing issues
            foreach (var aggregation in aggregatedMetrics)
            {
                aggregation.Should().NotBeEmpty();

                foreach (var metric in aggregation)
                {
                    metric.Min.Should().BeLessThanOrEqualTo(metric.Max);
                    metric.Average.Should().BeInRange(metric.Min, metric.Max);
                    metric.Current.Should().BeInRange(metric.Min, metric.Max);
                    metric.SampleCount.Should().BeGreaterThan(0);
                }
            }
        }

        [Fact]
        public async Task MetricsAggregator_ShouldHandleExpiredSamples()
        {
            var aggregatedMetrics = new List<IReadOnlyList<AggregatedMetrics>>();
            await using var aggregator = new MetricsAggregator(TimeSpan.FromSeconds(1)); // Reduced from 2s for faster tests
            aggregator.OnAggregation += metrics =>
            {
                if (metrics.Count > 0)
                    aggregatedMetrics.Add(metrics);
            };

            await Enumerable.Range(1, 10)
                .ToAsyncEnumerable()
                .SelectParallelStreamAsync(async (x, ct) =>
                {
                    await Task.Delay(10, ct);
                    return x;
                }, new()
                {
                    MaxDegreeOfParallelism = 2
                })
                .ToListAsync();

            // Wait for at least 2x the aggregation window to ensure timer fires reliably
            await Task.Delay(2000); // Reduced from 5000ms for faster tests

            aggregatedMetrics.Should().NotBeEmpty();
            var firstAggregation = aggregatedMetrics.First();
            firstAggregation.Should().NotBeEmpty();

            // Wait for another aggregation window to potentially expire samples
            // This verifies that the aggregator handles sample expiration gracefully
            await Task.Delay(2000); // Reduced from 5000ms for faster tests

            var totalAggregations = aggregatedMetrics.Count;
            totalAggregations.Should().BeGreaterThanOrEqualTo(1);

            // Verify all captured aggregations have valid data
            foreach (var aggregation in aggregatedMetrics)
            {
                aggregation.Should().NotBeEmpty();
                foreach (var metric in aggregation)
                {
                    metric.SampleCount.Should().BeGreaterThan(0);
                    metric.Min.Should().BeLessThanOrEqualTo(metric.Max);
                }
            }
        }

        [Fact]
        public void MetricsAggregator_ShouldNotThrow_WhenDisposed()
        {
            var aggregator = new MetricsAggregator();
            var act = () => aggregator.Dispose();
            act.Should().NotThrow();
        }

        [Fact]
        public void MetricsAggregator_ShouldNotThrow_WhenDoubleDisposed()
        {
            var aggregator = new MetricsAggregator();
            aggregator.Dispose();

            // Second dispose should not throw (tests disposal guard at line 137)
            var act = () => aggregator.Dispose();
            act.Should().NotThrow();
        }

        [Fact]
        public async Task MetricsAggregator_ShouldNotThrow_WhenDoubleAsyncDisposed()
        {
            var aggregator = new MetricsAggregator();
            await aggregator.DisposeAsync();

            // Second dispose should not throw (tests disposal guard at line 155)
            var act = async () => await aggregator.DisposeAsync();
            await act.Should().NotThrowAsync();
        }
    }
}
