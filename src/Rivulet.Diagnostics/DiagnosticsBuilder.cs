namespace Rivulet.Diagnostics;

/// <summary>
/// Fluent builder for configuring Rivulet diagnostics.
/// </summary>
/// <example>
/// <code>
/// using var diagnostics = new DiagnosticsBuilder()
///     .AddConsoleListener()
///     .AddFileListener("metrics.log")
///     .AddStructuredLogListener("metrics.json")
///     .AddMetricsAggregator(TimeSpan.FromSeconds(10), metrics =>
///     {
///         foreach (var metric in metrics)
///         {
///             Console.WriteLine($"{metric.DisplayName}: Avg={metric.Average:F2}");
///         }
///     })
///     .Build();
/// 
/// await Enumerable.Range(1, 100)
///     .ToAsyncEnumerable()
///     .SelectParallelAsync(async x => await ProcessAsync(x), new ParallelOptionsRivulet
///     {
///         MaxDegreeOfParallelism = 10
///     });
/// </code>
/// </example>
public sealed class DiagnosticsBuilder : IDisposable
{
    private readonly List<IDisposable> _listeners = new();

    /// <summary>
    /// Adds a console listener to the diagnostics pipeline.
    /// </summary>
    /// <param name="useColors">Whether to use console colors. Default is true.</param>
    /// <returns>The builder for chaining.</returns>
    public DiagnosticsBuilder AddConsoleListener(bool useColors = true)
    {
        _listeners.Add(new RivuletConsoleListener(useColors));
        return this;
    }

    /// <summary>
    /// Adds a file listener to the diagnostics pipeline.
    /// </summary>
    /// <param name="filePath">The path to the log file.</param>
    /// <param name="maxFileSizeBytes">Maximum file size before rotation. Default is 10MB.</param>
    /// <returns>The builder for chaining.</returns>
    public DiagnosticsBuilder AddFileListener(string filePath, long maxFileSizeBytes = 10 * 1024 * 1024)
    {
        _listeners.Add(new RivuletFileListener(filePath, maxFileSizeBytes));
        return this;
    }

    /// <summary>
    /// Adds a structured log listener to the diagnostics pipeline.
    /// </summary>
    /// <param name="filePath">The path to the JSON lines log file.</param>
    /// <returns>The builder for chaining.</returns>
    public DiagnosticsBuilder AddStructuredLogListener(string filePath)
    {
        _listeners.Add(new RivuletStructuredLogListener(filePath));
        return this;
    }

    /// <summary>
    /// Adds a structured log listener with a custom log action.
    /// </summary>
    /// <param name="logAction">Action to invoke with each JSON log line.</param>
    /// <returns>The builder for chaining.</returns>
    public DiagnosticsBuilder AddStructuredLogListener(Action<string> logAction)
    {
        _listeners.Add(new RivuletStructuredLogListener(logAction));
        return this;
    }

    /// <summary>
    /// Adds a metrics aggregator to the diagnostics pipeline.
    /// </summary>
    /// <param name="aggregationWindow">The time window for aggregation.</param>
    /// <param name="onAggregation">Action to invoke when metrics are aggregated.</param>
    /// <returns>The builder for chaining.</returns>
    public DiagnosticsBuilder AddMetricsAggregator(TimeSpan aggregationWindow, Action<IReadOnlyList<AggregatedMetrics>> onAggregation)
    {
        var aggregator = new MetricsAggregator(aggregationWindow);
        aggregator.OnAggregation += onAggregation;
        _listeners.Add(aggregator);
        return this;
    }

    /// <summary>
    /// Adds a Prometheus exporter to the diagnostics pipeline.
    /// </summary>
    /// <param name="exporter">Output parameter that receives the exporter instance.</param>
    /// <returns>The builder for chaining.</returns>
    public DiagnosticsBuilder AddPrometheusExporter(out PrometheusExporter exporter)
    {
        exporter = new PrometheusExporter();
        _listeners.Add(exporter);
        return this;
    }

    /// <summary>
    /// Adds a Rivulet health check to the diagnostics pipeline.
    /// </summary>
    /// <param name="exporter">The PrometheusExporter instance to use for retrieving metrics.</param>
    /// <param name="options">Optional health check options.</param>
    /// <returns>The builder for chaining.</returns>
    public DiagnosticsBuilder AddHealthCheck(PrometheusExporter exporter, RivuletHealthCheckOptions? options = null)
    {
        _listeners.Add(new RivuletHealthCheck(exporter, options));
        return this;
    }

    /// <summary>
    /// Builds and returns the diagnostics configuration.
    /// </summary>
    /// <returns>The builder instance for disposal.</returns>
    public DiagnosticsBuilder Build()
    {
        return this;
    }

    /// <summary>
    /// Disposes all registered listeners.
    /// </summary>
    public void Dispose()
    {
        foreach (var listener in _listeners)
        {
            listener.Dispose();
        }

        _listeners.Clear();
    }
}
