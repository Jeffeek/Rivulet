using FluentAssertions;
using Rivulet.Core;

namespace Rivulet.Diagnostics.Tests
{
    public class MetricsAggregatorTests
    {
        [Fact]
        public async Task MetricsAggregator_ShouldAggregateMetrics_OverTimeWindow()
        {
            var aggregatedMetrics = new List<IReadOnlyList<AggregatedMetrics>>();
            using var aggregator = new MetricsAggregator(TimeSpan.FromSeconds(2));
            aggregator.OnAggregation += metrics => aggregatedMetrics.Add(metrics);

            await Enumerable.Range(1, 20)
                .ToAsyncEnumerable()
                .SelectParallelStreamAsync(async (x, ct) =>
                {
                    await Task.Delay(50, ct);
                    return x * 2;
                }, new ParallelOptionsRivulet
                {
                    MaxDegreeOfParallelism = 5
                })
                .ToListAsync();

            await Task.Delay(3000);

            aggregatedMetrics.Should().NotBeEmpty();
            var lastAggregation = aggregatedMetrics.Last();
            lastAggregation.Should().NotBeEmpty();
        
            var itemsStartedMetric = lastAggregation.FirstOrDefault(m => m.Name == "items-started");
            itemsStartedMetric.Should().NotBeNull();
            itemsStartedMetric.Min.Should().BeGreaterThanOrEqualTo(0);
            itemsStartedMetric.Max.Should().BeGreaterThanOrEqualTo(itemsStartedMetric.Min);
            itemsStartedMetric.Average.Should().BeGreaterThanOrEqualTo(0);
            itemsStartedMetric.SampleCount.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task MetricsAggregator_ShouldCalculateCorrectStatistics()
        {
            var aggregatedMetrics = new List<IReadOnlyList<AggregatedMetrics>>();
            using var aggregator = new MetricsAggregator(TimeSpan.FromSeconds(2));
            aggregator.OnAggregation += metrics => aggregatedMetrics.Add(metrics);

            await Enumerable.Range(1, 10)
                .ToAsyncEnumerable()
                .SelectParallelStreamAsync(async (x, ct) =>
                {
                    await Task.Delay(10, ct);
                    return x;
                }, new ParallelOptionsRivulet
                {
                    MaxDegreeOfParallelism = 2
                })
                .ToListAsync();

            await Task.Delay(3000);

            aggregatedMetrics.Should().NotBeEmpty();
            var lastAggregation = aggregatedMetrics.Last();
        
            foreach (var metric in lastAggregation)
            {
                metric.Min.Should().BeLessThanOrEqualTo(metric.Max);
                metric.Average.Should().BeInRange(metric.Min, metric.Max);
                metric.Current.Should().BeInRange(metric.Min, metric.Max);
            }
        }

        [Fact]
        public async Task MetricsAggregator_ShouldHandleExpiredSamples()
        {
            var aggregatedMetrics = new List<IReadOnlyList<AggregatedMetrics>>();
            using var aggregator = new MetricsAggregator(TimeSpan.FromSeconds(1));
            aggregator.OnAggregation += metrics =>
            {
                if (metrics.Count > 0)
                    aggregatedMetrics.Add(metrics);
            };

            await Enumerable.Range(1, 5)
                .ToAsyncEnumerable()
                .SelectParallelStreamAsync(async (x, ct) =>
                {
                    await Task.Delay(10, ct);
                    return x;
                }, new ParallelOptionsRivulet
                {
                    MaxDegreeOfParallelism = 2
                })
                .ToListAsync();

            await Task.Delay(2500);

            aggregatedMetrics.Should().NotBeEmpty();
            var firstAggregation = aggregatedMetrics.First();
            firstAggregation.Should().NotBeEmpty();

            await Task.Delay(2000);

            var totalAggregations = aggregatedMetrics.Count;
            totalAggregations.Should().BeGreaterThanOrEqualTo(1);
        }

        [Fact]
        public void MetricsAggregator_ShouldNotThrow_WhenDisposed()
        {
            var aggregator = new MetricsAggregator();
            var act = () => aggregator.Dispose();
            act.Should().NotThrow();
        }
    }
}
