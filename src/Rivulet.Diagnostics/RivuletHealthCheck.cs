using Microsoft.Extensions.Diagnostics.HealthChecks;
using Rivulet.Core.Observability;

namespace Rivulet.Diagnostics;

/// <summary>
/// Health check for monitoring Rivulet.Core metrics.
/// Integrates with Microsoft.Extensions.Diagnostics.HealthChecks.
/// </summary>
/// <example>
/// <code>
/// // In Startup.cs or Program.cs
/// builder.Services.AddHealthChecks()
///     .AddCheck&lt;RivuletHealthCheck&gt;("rivulet", tags: new[] { "ready" });
/// 
/// // Configure thresholds
/// builder.Services.Configure&lt;RivuletHealthCheckOptions&gt;(options =>
/// {
///     options.ErrorRateThreshold = 0.1; // 10% error rate
///     options.FailureCountThreshold = 100;
/// });
/// </code>
/// </example>
public sealed class RivuletHealthCheck : IHealthCheck, IDisposable
{
    private readonly RivuletHealthCheckOptions _options;
    private readonly PrometheusExporter _exporter;

    /// <summary>
    /// Initializes a new instance of the <see cref="RivuletHealthCheck"/> class.
    /// </summary>
    /// <param name="exporter">The PrometheusExporter instance to use for retrieving metrics.</param>
    /// <param name="options">Health check options.</param>
    public RivuletHealthCheck(PrometheusExporter exporter, RivuletHealthCheckOptions? options = null)
    {
        _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
        _options = options ?? new RivuletHealthCheckOptions();
    }

    /// <summary>
    /// Checks the health of Rivulet operations.
    /// </summary>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var metrics = _exporter.ExportDictionary();

            if (metrics.Count == 0)
            {
                return Task.FromResult(HealthCheckResult.Healthy("No Rivulet operations currently running"));
            }

            var itemsStarted = metrics.GetValueOrDefault(RivuletMetricsConstants.CounterNames.ItemsStarted, 0);
            var itemsCompleted = metrics.GetValueOrDefault(RivuletMetricsConstants.CounterNames.ItemsCompleted, 0);
            var totalFailures = metrics.GetValueOrDefault(RivuletMetricsConstants.CounterNames.TotalFailures, 0);
            var totalRetries = metrics.GetValueOrDefault(RivuletMetricsConstants.CounterNames.TotalRetries, 0);

            var errorRate = itemsStarted > 0 ? totalFailures / itemsStarted : 0;

            var data = new Dictionary<string, object>
            {
                ["items_started"] = itemsStarted,
                ["items_completed"] = itemsCompleted,
                ["total_failures"] = totalFailures,
                ["total_retries"] = totalRetries,
                ["error_rate"] = errorRate
            };

            if (totalFailures >= _options.FailureCountThreshold)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"Failure count ({totalFailures}) exceeds threshold ({_options.FailureCountThreshold})",
                    data: data
                ));
            }

            if (errorRate >= _options.ErrorRateThreshold)
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"Error rate ({errorRate:P2}) exceeds threshold ({_options.ErrorRateThreshold:P2})",
                    data: data
                ));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                $"Rivulet operations healthy: {itemsCompleted}/{itemsStarted} completed, {totalFailures} failures",
                data: data
            ));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Failed to collect Rivulet metrics",
                exception: ex
            ));
        }
    }

    /// <summary>
    /// Disposes the health check.
    /// </summary>
    public void Dispose()
    {
    }
}

/// <summary>
/// Options for configuring <see cref="RivuletHealthCheck"/>.
/// </summary>
public sealed class RivuletHealthCheckOptions
{
    /// <summary>
    /// Gets or sets the error rate threshold (0.0 to 1.0) above which the health check reports degraded status.
    /// Default is 0.1 (10%).
    /// </summary>
    public double ErrorRateThreshold { get; set; } = 0.1;

    /// <summary>
    /// Gets or sets the failure count threshold above which the health check reports unhealthy status.
    /// Default is 1000.
    /// </summary>
    public long FailureCountThreshold { get; set; } = 1000;
}