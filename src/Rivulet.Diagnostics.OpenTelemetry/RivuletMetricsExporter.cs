using System.Diagnostics.Metrics;
using Rivulet.Core.Observability;

namespace Rivulet.Diagnostics.OpenTelemetry;

/// <summary>
/// Exports Rivulet metrics to OpenTelemetry Meters for monitoring and visualization.
/// </summary>
/// <remarks>
/// This class bridges Rivulet's EventSource-based metrics to OpenTelemetry's Meter API,
/// allowing integration with observability platforms like Prometheus, Azure Monitor, DataDog, etc.
/// </remarks>
/// <example>
/// <code>
/// // Configure OpenTelemetry at startup
/// using var meterProvider = Sdk.CreateMeterProviderBuilder()
///     .AddMeter(RivuletMetricsExporter.MeterName)
///     .AddPrometheusExporter()
///     .Build();
///
/// // Create the exporter (automatically starts exporting)
/// using var metricsExporter = new RivuletMetricsExporter(TimeSpan.FromSeconds(5));
///
/// // Use Rivulet normally - metrics are automatically exported
/// var results = await urls.SelectParallelAsync(processAsync, options);
/// </code>
/// </example>
public sealed class RivuletMetricsExporter : IDisposable
{
    /// <summary>
    /// The name of the Meter for Rivulet metrics.
    /// </summary>
    public const string MeterName = "Rivulet.Core";

    /// <summary>
    /// The version of the Rivulet instrumentation.
    /// </summary>
    private const string Version = "1.2.0";

    private static readonly Meter Meter = new(MeterName, Version);

    private readonly ObservableGauge<long> _itemsStartedGauge;
    private readonly ObservableGauge<long> _itemsCompletedGauge;
    private readonly ObservableGauge<long> _totalRetriesGauge;
    private readonly ObservableGauge<long> _totalFailuresGauge;
    private readonly ObservableGauge<long> _throttleEventsGauge;
    private readonly ObservableGauge<long> _drainEventsGauge;
    private readonly ObservableGauge<double> _errorRateGauge;

    /// <summary>
    /// Initializes a new instance of the <see cref="RivuletMetricsExporter"/> class.
    /// </summary>
    public RivuletMetricsExporter()
    {
        var eventSource = RivuletEventSource.Log;

        _itemsStartedGauge = Meter.CreateObservableGauge(
            "rivulet.items.started",
            () => eventSource.GetItemsStarted(),
            unit: "{items}",
            description: "Total number of items that have started processing");

        _itemsCompletedGauge = Meter.CreateObservableGauge(
            "rivulet.items.completed",
            () => eventSource.GetItemsCompleted(),
            unit: "{items}",
            description: "Total number of items that have completed processing");

        _totalRetriesGauge = Meter.CreateObservableGauge(
            "rivulet.retries.total",
            () => eventSource.GetTotalRetries(),
            unit: "{retries}",
            description: "Total number of retry attempts across all operations");

        _totalFailuresGauge = Meter.CreateObservableGauge(
            "rivulet.failures.total",
            () => eventSource.GetTotalFailures(),
            unit: "{failures}",
            description: "Total number of failed items after all retries");

        _throttleEventsGauge = Meter.CreateObservableGauge(
            "rivulet.throttle.events",
            () => eventSource.GetThrottleEvents(),
            unit: "{events}",
            description: "Total number of backpressure throttle events");

        _drainEventsGauge = Meter.CreateObservableGauge(
            "rivulet.drain.events",
            () => eventSource.GetDrainEvents(),
            unit: "{events}",
            description: "Total number of channel drain events");

        _errorRateGauge = Meter.CreateObservableGauge(
            "rivulet.error.rate",
            () =>
            {
                var started = eventSource.GetItemsStarted();
                var failures = eventSource.GetTotalFailures();
                return started > 0 ? (double)failures / started : 0.0;
            },
            unit: "{ratio}",
            description: "Error rate (failures / items started)");
    }

    /// <summary>
    /// Disposes the metrics exporter.
    /// </summary>
    public void Dispose()
    {
        // ObservableGauges are automatically cleaned up when the meter is disposed
        // We keep the meter alive as a static singleton for the lifetime of the app
    }
}
