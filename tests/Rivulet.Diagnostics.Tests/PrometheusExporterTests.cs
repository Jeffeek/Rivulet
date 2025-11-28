using Rivulet.Base.Tests;
using Rivulet.Core;
using Rivulet.Core.Observability;

namespace Rivulet.Diagnostics.Tests;

[Collection(TestCollections.SerialEventSource)]
public class PrometheusExporterTests
{
    [Fact]
    public async Task PrometheusExporter_ShouldExportMetrics_InPrometheusFormat()
    {
        using var exporter = new PrometheusExporter();

        await Enumerable.Range(1, 10)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x * 2;
            }, new()
            {
                MaxDegreeOfParallelism = 2
            })
            .ToListAsync();

        // Wait for EventCounters to fire
        // Increased from 2000ms → 5000ms for Windows CI/CD reliability (2/180 failures)
        // EventCounters have ~1s polling interval but can be delayed under load
        // Must wait for BOTH items_started AND items_completed to be present
        await Extensions.ApplyDeadlineAsync(
            DateTime.UtcNow.AddMilliseconds(5000),
            () => Task.Delay(100),
            () =>
            {
                var export = exporter.Export();
                return string.IsNullOrEmpty(export) ||
                       !export.Contains("rivulet_items_started") ||
                       !export.Contains("rivulet_items_completed");
            });

        var prometheusText = exporter.Export();
        prometheusText.Should().Contain("# Rivulet.Core Metrics");
        prometheusText.Should().Contain("# HELP rivulet_");
        prometheusText.Should().Contain("# TYPE rivulet_");
        prometheusText.Should().Contain("rivulet_items_started");
        prometheusText.Should().Contain("rivulet_items_completed");
    }

    [Fact]
    public async Task PrometheusExporter_ShouldExportDictionary_WithMetricValues()
    {
        using var exporter = new PrometheusExporter();

        await Enumerable.Range(1, 10)
            .ToAsyncEnumerable()
            .ForEachParallelAsync(async (_, ct) =>
            {
                await Task.Delay(10, ct);
            }, new()
            {
                MaxDegreeOfParallelism = 2
            });

        // Wait for EventCounters to fire
        // Increased from 2000ms → 5000ms for Windows CI/CD reliability (3/180 failures)
        // EventCounters have ~1s polling interval but can be delayed under load
        // Must wait for BOTH items_started AND items_completed keys to be present
        await Extensions.ApplyDeadlineAsync(
            DateTime.UtcNow.AddMilliseconds(5000),
            () => Task.Delay(100),
            () =>
            {
                var dict = exporter.ExportDictionary();
                return !dict.ContainsKey(RivuletMetricsConstants.CounterNames.ItemsStarted) ||
                       !dict.ContainsKey(RivuletMetricsConstants.CounterNames.ItemsCompleted);
            });

        var metrics = exporter.ExportDictionary();
        metrics.Should().NotBeEmpty();
        metrics.Should().ContainKey(RivuletMetricsConstants.CounterNames.ItemsStarted);
        metrics.Should().ContainKey(RivuletMetricsConstants.CounterNames.ItemsCompleted);
        metrics[RivuletMetricsConstants.CounterNames.ItemsStarted].Should().BeGreaterThanOrEqualTo(0);
        metrics[RivuletMetricsConstants.CounterNames.ItemsCompleted].Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void PrometheusExporter_ShouldReturnEmptyExport_WhenNoMetrics()
    {
        using var exporter = new PrometheusExporter();

        var prometheusText = exporter.Export();
        prometheusText.Should().Contain("# Rivulet.Core Metrics");
        
        var metrics = exporter.ExportDictionary();
        metrics.Should().BeEmpty();
    }
}
