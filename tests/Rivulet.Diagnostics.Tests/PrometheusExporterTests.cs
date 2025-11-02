using FluentAssertions;
using Rivulet.Core;

namespace Rivulet.Diagnostics.Tests;

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
            }, new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 2
            })
            .ToListAsync();

        // Wait for EventCounters to fire - increased for CI/CD reliability
        await Task.Delay(2000);

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
            }, new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 2
            });

        // Wait for EventCounters to fire - increased for CI/CD reliability
        await Task.Delay(2000);

        var metrics = exporter.ExportDictionary();
        metrics.Should().NotBeEmpty();
        metrics.Should().ContainKey("items-started");
        metrics.Should().ContainKey("items-completed");
        metrics["items-started"].Should().BeGreaterThanOrEqualTo(0);
        metrics["items-completed"].Should().BeGreaterThanOrEqualTo(0);
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
