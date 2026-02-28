using Rivulet.Base.Tests;
using Rivulet.Core;
using Rivulet.Core.Observability;

namespace Rivulet.Diagnostics.Tests;

[Collection(TestCollections.SerialEventSource)]
public sealed class PrometheusExporterTests
{
    [Fact]
    public async Task PrometheusExporter_ShouldExportMetrics_InPrometheusFormat()
    {
        using var exporter = new PrometheusExporter();

        // Operations must run long enough for EventCounter polling (1 second interval)
        // 10 items * 200ms / 2 parallelism = 1000ms (1 second) minimum operation time
        await Enumerable.Range(1, 10)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(static async (x, ct) =>
                {
                    await Task.Delay(200, ct);
                    return x * 2;
                },
                new() { MaxDegreeOfParallelism = 2 },
                cancellationToken: TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Wait for EventCounters to fire
        // Increased from 2000ms → 5000ms for Windows CI/CD reliability (2/180 failures)
        // EventCounters have ~1s polling interval but can be delayed under load
        // Must wait for BOTH items_started AND items_completed to be present
        await DeadlineExtensions.ApplyDeadlineAsync(
            DateTime.UtcNow.AddMilliseconds(4000),
            static () => Task.Delay(100),
            () =>
            {
                var export = exporter.Export();
                return string.IsNullOrEmpty(export) ||
                       !export.Contains("rivulet_items_started") ||
                       !export.Contains("rivulet_items_completed");
            });

        var prometheusText = exporter.Export();
        prometheusText.ShouldContain("# Rivulet.Core Metrics");
        prometheusText.ShouldContain("# HELP rivulet_");
        prometheusText.ShouldContain("# TYPE rivulet_");
        prometheusText.ShouldContain("rivulet_items_started");
        prometheusText.ShouldContain("rivulet_items_completed");
    }

    [Fact]
    public async Task PrometheusExporter_ShouldExportDictionary_WithMetricValues()
    {
        using var exporter = new PrometheusExporter();

        // Operations must run long enough for EventCounter polling (1 second interval)
        // 10 items * 200ms / 2 parallelism = 1000ms (1 second) minimum operation time
        await Enumerable.Range(1, 10)
            .ToAsyncEnumerable()
            .ForEachParallelAsync(static async (_, ct) => { await Task.Delay(200, ct); },
                new() { MaxDegreeOfParallelism = 2 },
                TestContext.Current.CancellationToken);

        // Wait for EventCounters to fire
        // Increased from 2000ms → 5000ms for Windows CI/CD reliability (3/180 failures)
        // EventCounters have ~1s polling interval but can be delayed under load
        // Must wait for BOTH items_started AND items_completed keys to be present
        await DeadlineExtensions.ApplyDeadlineAsync(
            DateTime.UtcNow.AddMilliseconds(4000),
            static () => Task.Delay(100),
            () =>
            {
                var dict = exporter.ExportDictionary();
                return !dict.ContainsKey(RivuletMetricsConstants.CounterNames.ItemsStarted) ||
                       !dict.ContainsKey(RivuletMetricsConstants.CounterNames.ItemsCompleted);
            });

        var metrics = exporter.ExportDictionary();
        metrics.ShouldNotBeEmpty();
        metrics.TryGetValue(RivuletMetricsConstants.CounterNames.ItemsStarted, out var itemsStarted).ShouldBeTrue();
        metrics.TryGetValue(RivuletMetricsConstants.CounterNames.ItemsCompleted, out var itemsCompleted).ShouldBeTrue();
        itemsStarted.ShouldBeGreaterThanOrEqualTo(0);
        itemsCompleted.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void PrometheusExporter_ShouldReturnEmptyExport_WhenNoMetrics()
    {
        using var exporter = new PrometheusExporter();

        var prometheusText = exporter.Export();
        prometheusText.ShouldContain("# Rivulet.Core Metrics");

        var metrics = exporter.ExportDictionary();
        metrics.ShouldBeEmpty();
    }
}
