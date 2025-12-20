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
    private static readonly Meter Meter = new(RivuletSharedConstants.RivuletCore, RivuletOpenTelemetryConstants.InstrumentationVersion);

    /// <summary>
    ///     Initializes a new instance of the <see cref="RivuletMetricsExporter" /> class.
    /// </summary>
    public RivuletMetricsExporter()
    {
        var eventSource = RivuletEventSource.Log;

        _itemsStartedGauge = CreateMetricGauge(
            RivuletOpenTelemetryConstants.MetricNames.ItemsStarted,
            () => eventSource.GetItemsStarted(),
            RivuletOpenTelemetryConstants.MetricUnits.Items,
            RivuletOpenTelemetryConstants.MetricDescriptions.ItemsStarted);

        _itemsCompletedGauge = CreateMetricGauge(
            RivuletOpenTelemetryConstants.MetricNames.ItemsCompleted,
            () => eventSource.GetItemsCompleted(),
            RivuletOpenTelemetryConstants.MetricUnits.Items,
            RivuletOpenTelemetryConstants.MetricDescriptions.ItemsCompleted);

        _totalRetriesGauge = CreateMetricGauge(
            RivuletOpenTelemetryConstants.MetricNames.RetriesTotal,
            () => eventSource.GetTotalRetries(),
            RivuletOpenTelemetryConstants.MetricUnits.Retries,
            RivuletOpenTelemetryConstants.MetricDescriptions.RetriesTotal);

        _totalFailuresGauge = CreateMetricGauge(
            RivuletOpenTelemetryConstants.MetricNames.FailuresTotal,
            () => eventSource.GetTotalFailures(),
            RivuletOpenTelemetryConstants.MetricUnits.Failures,
            RivuletOpenTelemetryConstants.MetricDescriptions.FailuresTotal);

        _throttleEventsGauge = CreateMetricGauge(
            RivuletOpenTelemetryConstants.MetricNames.ThrottleEvents,
            () => eventSource.GetThrottleEvents(),
            RivuletOpenTelemetryConstants.MetricUnits.Events,
            RivuletOpenTelemetryConstants.MetricDescriptions.ThrottleEvents);

        _drainEventsGauge = CreateMetricGauge(
            RivuletOpenTelemetryConstants.MetricNames.DrainEvents,
            () => eventSource.GetDrainEvents(),
            RivuletOpenTelemetryConstants.MetricUnits.Events,
            RivuletOpenTelemetryConstants.MetricDescriptions.DrainEvents);

        _errorRateGauge = CreateMetricGauge(
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
    ///     Creates an observable gauge with the specified configuration.
    /// </summary>
    /// <typeparam name="T">The type of the measurement value.</typeparam>
    /// <param name="name">The metric name.</param>
    /// <param name="measurement">Function to retrieve the measurement value.</param>
    /// <param name="unit">The measurement unit.</param>
    /// <param name="description">The metric description.</param>
    /// <returns>An observable gauge configured with the specified parameters.</returns>
    private static ObservableGauge<T> CreateMetricGauge<T>(
        string name,
        Func<T> measurement,
        string unit,
        string description)
        where T : struct =>
        Meter.CreateObservableGauge(name, measurement, unit, description);

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