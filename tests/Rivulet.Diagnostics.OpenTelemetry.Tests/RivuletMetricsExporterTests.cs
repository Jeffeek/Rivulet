using FluentAssertions;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using Rivulet.Core;
using Rivulet.Core.Observability;

namespace Rivulet.Diagnostics.OpenTelemetry.Tests;

[Collection("Metrics Tests")]
public class RivuletMetricsExporterTests : IDisposable
{
    private readonly List<Metric> _exportedMetrics = new();
    private readonly MeterProvider _meterProvider;
    private readonly RivuletMetricsExporter _exporter;

    public RivuletMetricsExporterTests()
    {
        _meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(RivuletSharedConstants.RivuletCore)
            .AddInMemoryExporter(_exportedMetrics)
            .Build();

        _exporter = new RivuletMetricsExporter();
    }

    public void Dispose()
    {
        _exporter.Dispose();
        _meterProvider.Dispose();
    }

    [Fact]
    public void Exporter_ShouldExportItemsStartedMetric()
    {
        var initialValue = RivuletEventSource.Log.GetItemsStarted();
        
        for (var i = 0; i < 10; i++)
        {
            RivuletEventSource.Log.IncrementItemsStarted();
        }

        _meterProvider.ForceFlush();

        _exportedMetrics.Should().Contain(m => m.Name == "rivulet.items.started");
        var itemsStarted = _exportedMetrics.First(m => m.Name == "rivulet.items.started");
        var currentValue = GetMetricValue(itemsStarted);
        (currentValue - initialValue).Should().Be(10);
    }

    [Fact]
    public void Exporter_ShouldExportItemsCompletedMetric()
    {
        var initialValue = RivuletEventSource.Log.GetItemsCompleted();
        
        for (var i = 0; i < 5; i++)
        {
            RivuletEventSource.Log.IncrementItemsCompleted();
        }

        _meterProvider.ForceFlush();

        _exportedMetrics.Should().Contain(m => m.Name == "rivulet.items.completed");
        var itemsCompleted = _exportedMetrics.First(m => m.Name == "rivulet.items.completed");
        var currentValue = GetMetricValue(itemsCompleted);
        (currentValue - initialValue).Should().Be(5);
    }

    [Fact]
    public void Exporter_ShouldExportRetriesMetric()
    {
        var initialValue = RivuletEventSource.Log.GetTotalRetries();
        
        for (var i = 0; i < 3; i++)
        {
            RivuletEventSource.Log.IncrementRetries();
        }

        _meterProvider.ForceFlush();

        _exportedMetrics.Should().Contain(m => m.Name == "rivulet.retries.total");
        var retries = _exportedMetrics.First(m => m.Name == "rivulet.retries.total");
        var currentValue = GetMetricValue(retries);
        (currentValue - initialValue).Should().Be(3);
    }

    [Fact]
    public void Exporter_ShouldExportFailuresMetric()
    {
        var initialValue = RivuletEventSource.Log.GetTotalFailures();
        
        for (var i = 0; i < 2; i++)
        {
            RivuletEventSource.Log.IncrementFailures();
        }

        _meterProvider.ForceFlush();

        _exportedMetrics.Should().Contain(m => m.Name == "rivulet.failures.total");
        var failures = _exportedMetrics.First(m => m.Name == "rivulet.failures.total");
        var currentValue = GetMetricValue(failures);
        (currentValue - initialValue).Should().Be(2);
    }

    [Fact]
    public void Exporter_ShouldCalculateErrorRate()
    {
        var initialStarted = RivuletEventSource.Log.GetItemsStarted();
        var initialFailures = RivuletEventSource.Log.GetTotalFailures();
        
        for (var i = 0; i < 10; i++)
        {
            RivuletEventSource.Log.IncrementItemsStarted();
        }
        for (var i = 0; i < 2; i++)
        {
            RivuletEventSource.Log.IncrementFailures();
        }

        _meterProvider.ForceFlush();

        _exportedMetrics.Should().Contain(m => m.Name == "rivulet.error.rate");
        var errorRate = _exportedMetrics.First(m => m.Name == "rivulet.error.rate");
        var value = GetMetricValue(errorRate);
        
        var expectedErrorRate = initialStarted + 10 > 0 
            ? (double)(initialFailures + 2) / (initialStarted + 10) 
            : 0.0;
        value.Should().BeApproximately(expectedErrorRate, 0.001);
    }

    [Fact]
    public void Exporter_ShouldExportThrottleEventsMetric()
    {
        var initialValue = RivuletEventSource.Log.GetThrottleEvents();
        
        for (var i = 0; i < 4; i++)
        {
            RivuletEventSource.Log.IncrementThrottleEvents();
        }

        _meterProvider.ForceFlush();

        _exportedMetrics.Should().Contain(m => m.Name == "rivulet.throttle.events");
        var throttleEvents = _exportedMetrics.First(m => m.Name == "rivulet.throttle.events");
        var currentValue = GetMetricValue(throttleEvents);
        (currentValue - initialValue).Should().Be(4);
    }

    [Fact]
    public void Exporter_ShouldExportDrainEventsMetric()
    {
        var initialValue = RivuletEventSource.Log.GetDrainEvents();
        
        for (var i = 0; i < 3; i++)
        {
            RivuletEventSource.Log.IncrementDrainEvents();
        }

        _meterProvider.ForceFlush();

        _exportedMetrics.Should().Contain(m => m.Name == "rivulet.drain.events");
        var drainEvents = _exportedMetrics.First(m => m.Name == "rivulet.drain.events");
        var currentValue = GetMetricValue(drainEvents);
        (currentValue - initialValue).Should().Be(3);
    }

    [Fact]
    public async Task Exporter_ShouldWorkWithActualParallelOperations()
    {
        // Clear any previous metrics to ensure clean state
        _exportedMetrics.Clear();
        _meterProvider.ForceFlush();

        var initialStarted = RivuletEventSource.Log.GetItemsStarted();
        var initialCompleted = RivuletEventSource.Log.GetItemsCompleted();

        var items = Enumerable.Range(1, 50);

        await items.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x * 2;
            },
            new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 4
            });

        // Force flush to get latest metrics
        _meterProvider.ForceFlush();
        await Task.Delay(50); // Give time for metrics to be exported

        _exportedMetrics.Should().Contain(m => m.Name == "rivulet.items.started");
        _exportedMetrics.Should().Contain(m => m.Name == "rivulet.items.completed");

        // Get the LATEST metrics (Last instead of First)
        var itemsStarted = _exportedMetrics.Last(m => m.Name == "rivulet.items.started");
        var itemsCompleted = _exportedMetrics.Last(m => m.Name == "rivulet.items.completed");

        // Read final values from EventSource for accurate comparison
        var finalStarted = RivuletEventSource.Log.GetItemsStarted();
        var finalCompleted = RivuletEventSource.Log.GetItemsCompleted();

        // Verify at least 50 items were processed (perhaps more due to parallel tests)
        // Since RivuletEventSource is a static singleton, other tests may increment counters
        (finalStarted - initialStarted).Should().BeGreaterThanOrEqualTo(50);
        (finalCompleted - initialCompleted).Should().BeGreaterThanOrEqualTo(50);

        // Verify metrics were exported and reflect the current state
        GetMetricValue(itemsStarted).Should().BeGreaterThanOrEqualTo(finalStarted);
        GetMetricValue(itemsCompleted).Should().BeGreaterThanOrEqualTo(finalCompleted);
    }

    private static double GetMetricValue(Metric metric)
    {
        foreach (ref readonly var metricPoint in metric.GetMetricPoints())
        {
            try
            {
                return metricPoint.GetGaugeLastValueDouble();
            }
            catch (NotSupportedException)
            {
                return metricPoint.GetGaugeLastValueLong();
            }
        }

        return 0;
    }
}
