using System.Diagnostics.Metrics;
using Rivulet.Core;
using Rivulet.Core.Observability;

namespace Rivulet.Diagnostics.OpenTelemetry;

/// <summary>
///     Exports Rivulet metrics to OpenTelemetry Meters for monitoring and visualization.
/// </summary>
/// <remarks>
///     This class bridges Rivulet's EventSource-based metrics to OpenTelemetry's Meter API,
///     allowing integration with observability platforms like Prometheus, Azure Monitor, DataDog, etc.
/// </remarks>
/// <example>
///     <code>
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
    private static readonly Meter _meter = new(RivuletSharedConstants.RivuletCore, RivuletOpenTelemetryConstants.InstrumentationVersion);

    /// <summary>
    ///     Initializes a new instance of the <see cref="RivuletMetricsExporter" /> class.
    /// </summary>
    public RivuletMetricsExporter()
    {
        var eventSource = RivuletEventSource.Log;

        _itemsStartedGauge = _meter.CreateObservableGauge(
            RivuletOpenTelemetryConstants.MetricNames.ItemsStarted,
            () => eventSource.GetItemsStarted(),
            RivuletOpenTelemetryConstants.MetricUnits.Items,
            RivuletOpenTelemetryConstants.MetricDescriptions.ItemsStarted);

        _itemsCompletedGauge = _meter.CreateObservableGauge(
            RivuletOpenTelemetryConstants.MetricNames.ItemsCompleted,
            () => eventSource.GetItemsCompleted(),
            RivuletOpenTelemetryConstants.MetricUnits.Items,
            RivuletOpenTelemetryConstants.MetricDescriptions.ItemsCompleted);

        _totalRetriesGauge = _meter.CreateObservableGauge(
            RivuletOpenTelemetryConstants.MetricNames.RetriesTotal,
            () => eventSource.GetTotalRetries(),
            RivuletOpenTelemetryConstants.MetricUnits.Retries,
            RivuletOpenTelemetryConstants.MetricDescriptions.RetriesTotal);

        _totalFailuresGauge = _meter.CreateObservableGauge(
            RivuletOpenTelemetryConstants.MetricNames.FailuresTotal,
            () => eventSource.GetTotalFailures(),
            RivuletOpenTelemetryConstants.MetricUnits.Failures,
            RivuletOpenTelemetryConstants.MetricDescriptions.FailuresTotal);

        _throttleEventsGauge = _meter.CreateObservableGauge(
            RivuletOpenTelemetryConstants.MetricNames.ThrottleEvents,
            () => eventSource.GetThrottleEvents(),
            RivuletOpenTelemetryConstants.MetricUnits.Events,
            RivuletOpenTelemetryConstants.MetricDescriptions.ThrottleEvents);

        _drainEventsGauge = _meter.CreateObservableGauge(
            RivuletOpenTelemetryConstants.MetricNames.DrainEvents,
            () => eventSource.GetDrainEvents(),
            RivuletOpenTelemetryConstants.MetricUnits.Events,
            RivuletOpenTelemetryConstants.MetricDescriptions.DrainEvents);

        _errorRateGauge = _meter.CreateObservableGauge(
            RivuletOpenTelemetryConstants.MetricNames.ErrorRate,
            () =>
            {
                var started = eventSource.GetItemsStarted();
                var failures = eventSource.GetTotalFailures();
                return started > 0 ? (double)failures / started : 0.0;
            },
            RivuletOpenTelemetryConstants.MetricUnits.Ratio,
            RivuletOpenTelemetryConstants.MetricDescriptions.ErrorRate);
    }

    /// <summary>
    ///     Disposes the metrics exporter.
    /// </summary>
    public void Dispose()
    {
        // ObservableGauges are automatically cleaned up when the meter is disposed
        // We keep the meter alive as a static singleton for the lifetime of the app
    }

    /// <summary>
    ///     <see cref="Dispose" />
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
}