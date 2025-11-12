using System.Diagnostics.Metrics;
using Rivulet.Core;
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
    private static readonly Meter Meter = new(RivuletSharedConstants.RivuletCore, RivuletOpenTelemetryConstants.InstrumentationVersion);

    /// <summary>
    /// <see cref="Dispose"/>
    /// </summary>
    // ReSharper disable NotAccessedField.Local
    private readonly ObservableGauge<long> _itemsStartedGauge;
    private readonly ObservableGauge<long> _itemsCompletedGauge;
    private readonly ObservableGauge<long> _totalRetriesGauge;
    private readonly ObservableGauge<long> _totalFailuresGauge;
    private readonly ObservableGauge<long> _throttleEventsGauge;
    private readonly ObservableGauge<long> _drainEventsGauge;
    private readonly ObservableGauge<double> _errorRateGauge;
    // ReSharper restore NotAccessedField.Local

    /// <summary>
    /// Initializes a new instance of the <see cref="RivuletMetricsExporter"/> class.
    /// </summary>
    public RivuletMetricsExporter()
    {
        var eventSource = RivuletEventSource.Log;

        _itemsStartedGauge = Meter.CreateObservableGauge(
            RivuletOpenTelemetryConstants.MetricNames.ItemsStarted,
            () => eventSource.GetItemsStarted(),
            unit: RivuletOpenTelemetryConstants.MetricUnits.Items,
            description: RivuletOpenTelemetryConstants.MetricDescriptions.ItemsStarted);

        _itemsCompletedGauge = Meter.CreateObservableGauge(
            RivuletOpenTelemetryConstants.MetricNames.ItemsCompleted,
            () => eventSource.GetItemsCompleted(),
            unit: RivuletOpenTelemetryConstants.MetricUnits.Items,
            description: RivuletOpenTelemetryConstants.MetricDescriptions.ItemsCompleted);

        _totalRetriesGauge = Meter.CreateObservableGauge(
            RivuletOpenTelemetryConstants.MetricNames.RetriesTotal,
            () => eventSource.GetTotalRetries(),
            unit: RivuletOpenTelemetryConstants.MetricUnits.Retries,
            description: RivuletOpenTelemetryConstants.MetricDescriptions.RetriesTotal);

        _totalFailuresGauge = Meter.CreateObservableGauge(
            RivuletOpenTelemetryConstants.MetricNames.FailuresTotal,
            () => eventSource.GetTotalFailures(),
            unit: RivuletOpenTelemetryConstants.MetricUnits.Failures,
            description: RivuletOpenTelemetryConstants.MetricDescriptions.FailuresTotal);

        _throttleEventsGauge = Meter.CreateObservableGauge(
            RivuletOpenTelemetryConstants.MetricNames.ThrottleEvents,
            () => eventSource.GetThrottleEvents(),
            unit: RivuletOpenTelemetryConstants.MetricUnits.Events,
            description: RivuletOpenTelemetryConstants.MetricDescriptions.ThrottleEvents);

        _drainEventsGauge = Meter.CreateObservableGauge(
            RivuletOpenTelemetryConstants.MetricNames.DrainEvents,
            () => eventSource.GetDrainEvents(),
            unit: RivuletOpenTelemetryConstants.MetricUnits.Events,
            description: RivuletOpenTelemetryConstants.MetricDescriptions.DrainEvents);

        _errorRateGauge = Meter.CreateObservableGauge(
            RivuletOpenTelemetryConstants.MetricNames.ErrorRate,
            () =>
            {
                var started = eventSource.GetItemsStarted();
                var failures = eventSource.GetTotalFailures();
                return started > 0 ? (double)failures / started : 0.0;
            },
            unit: RivuletOpenTelemetryConstants.MetricUnits.Ratio,
            description: RivuletOpenTelemetryConstants.MetricDescriptions.ErrorRate);
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
