using System.Text;
using Rivulet.Core.Internal;

namespace Rivulet.Diagnostics;

/// <summary>
///     Exports Rivulet metrics in Prometheus text format.
///     Can be used with Prometheus scraping or push gateway.
/// </summary>
/// <example>
///     <code>
/// using var exporter = new PrometheusExporter();
/// 
/// await Enumerable.Range(1, 100)
///     .ToAsyncEnumerable()
///     .SelectParallelAsync(async x => await ProcessAsync(x), new ParallelOptionsRivulet
///     {
///         MaxDegreeOfParallelism = 10
///     });
/// 
/// var prometheusText = exporter.Export();
/// await File.WriteAllTextAsync("metrics.prom", prometheusText);
/// </code>
/// </example>
public sealed class PrometheusExporter : RivuletEventListenerBase
{
    private readonly Dictionary<string, (string DisplayName, double Value, string DisplayUnits)> _latestValues = [];
    private readonly object _lock = LockFactory.CreateLock();

    /// <summary>
    ///     Called when a counter value is received.
    /// </summary>
    protected override void OnCounterReceived(
        string name,
        string displayName,
        double value,
        string displayUnits
    ) =>
        LockHelper.Execute(_lock, () => _latestValues[name] = (displayName, value, displayUnits));

    /// <summary>
    ///     Exports current metrics in Prometheus text format.
    /// </summary>
    /// <returns>Prometheus-formatted metrics text.</returns>
    public string Export() =>
        LockHelper.Execute(_lock,
            () =>
            {
                var sb = new StringBuilder();
                sb.AppendLine(RivuletDiagnosticsConstants.PrometheusFormats.HeaderComment);
                sb.AppendLine(string.Format(RivuletDiagnosticsConstants.PrometheusFormats.GeneratedAtCommentFormat,
                    DateTime.UtcNow.ToString(RivuletDiagnosticsConstants.DateTimeFormats.Prometheus)));
                sb.AppendLine();

                foreach (var kvp in _latestValues)
                {
                    var metricName = SanitizeMetricName(kvp.Key);
                    var (displayName, value, _) = kvp.Value;

                    sb.AppendLine(string.Format(RivuletDiagnosticsConstants.PrometheusFormats.HelpFormat,
                        metricName,
                        displayName));
                    sb.AppendLine(string.Format(RivuletDiagnosticsConstants.PrometheusFormats.TypeFormat, metricName));
                    sb.AppendLine(string.Format(RivuletDiagnosticsConstants.PrometheusFormats.MetricFormat,
                        metricName,
                        value));
                    sb.AppendLine();
                }

                return sb.ToString();
            });

    /// <summary>
    ///     Exports current metrics as a dictionary.
    /// </summary>
    /// <returns>Dictionary of metric names to values.</returns>
    public IReadOnlyDictionary<string, double> ExportDictionary() =>
        LockHelper.Execute(_lock,
            () => _latestValues.ToDictionary(
                static kvp => kvp.Key,
                static kvp => kvp.Value.Value
            ));

    private static string SanitizeMetricName(string name) => name.Replace("-", "_").Replace(".", "_");
}
