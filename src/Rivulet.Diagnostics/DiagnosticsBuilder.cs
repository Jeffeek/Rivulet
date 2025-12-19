using Rivulet.Core.Internal;
using System.Diagnostics.CodeAnalysis;

namespace Rivulet.Diagnostics;

/// <summary>
///     Fluent builder for configuring Rivulet diagnostics.
/// </summary>
/// <example>
///     <code>
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
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
public sealed class DiagnosticsBuilder : IDisposable, IAsyncDisposable
{
    private readonly object _disposeLock = LockFactory.CreateLock();
    private bool _disposed;

    private readonly List<IDisposable> _listeners = new();
    private readonly List<IAsyncDisposable> _asyncListeners = new();

    /// <summary>
    ///     Adds a console listener to the diagnostics pipeline.
    /// </summary>
    /// <param name="useColors">Whether to use console colors. Default is true.</param>
    /// <returns>The builder for chaining.</returns>
    public DiagnosticsBuilder AddConsoleListener(bool useColors = true)
    {
        _listeners.Add(new RivuletConsoleListener(useColors));
        return this;
    }

    /// <summary>
    ///     Adds a file listener to the diagnostics pipeline.
    /// </summary>
    /// <param name="filePath">The path to the log file.</param>
    /// <param name="maxFileSizeBytes">Maximum file size before rotation. Default is 10MB.</param>
    /// <returns>The builder for chaining.</returns>
    public DiagnosticsBuilder AddFileListener(string filePath, long maxFileSizeBytes = 10 * 1024 * 1024)
    {
        _asyncListeners.Add(new RivuletFileListener(filePath, maxFileSizeBytes));
        return this;
    }

    /// <summary>
    ///     Adds a structured log listener to the diagnostics pipeline.
    /// </summary>
    /// <param name="filePath">The path to the JSON lines log file.</param>
    /// <returns>The builder for chaining.</returns>
    public DiagnosticsBuilder AddStructuredLogListener(string filePath)
    {
        _asyncListeners.Add(new RivuletStructuredLogListener(filePath));
        return this;
    }

    /// <summary>
    ///     Adds a structured log listener with a custom log action.
    /// </summary>
    /// <param name="logAction">Action to invoke with each JSON log line.</param>
    /// <returns>The builder for chaining.</returns>
    public DiagnosticsBuilder AddStructuredLogListener(Action<string> logAction)
    {
        _asyncListeners.Add(new RivuletStructuredLogListener(logAction));
        return this;
    }

    /// <summary>
    ///     Adds a metrics aggregator to the diagnostics pipeline.
    /// </summary>
    /// <param name="aggregationWindow">The time window for aggregation.</param>
    /// <param name="onAggregation">Action to invoke when metrics are aggregated.</param>
    /// <returns>The builder for chaining.</returns>
    public DiagnosticsBuilder AddMetricsAggregator(TimeSpan aggregationWindow,
        Action<IReadOnlyList<AggregatedMetrics>> onAggregation)
    {
        var aggregator = new MetricsAggregator(aggregationWindow);
        aggregator.OnAggregation += onAggregation;
        _asyncListeners.Add(aggregator);
        return this;
    }

    /// <summary>
    ///     Adds a Prometheus exporter to the diagnostics pipeline.
    /// </summary>
    /// <param name="exporter">Output parameter that receives the exporter instance.</param>
    /// <returns>The builder for chaining.</returns>
    public DiagnosticsBuilder AddPrometheusExporter(out PrometheusExporter exporter)
    {
        exporter = new();
        _listeners.Add(exporter);
        return this;
    }

    /// <summary>
    ///     Adds a Rivulet health check to the diagnostics pipeline.
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
    ///     Builds and returns the diagnostics configuration.
    /// </summary>
    /// <returns>The builder instance for disposal.</returns>
    public DiagnosticsBuilder Build() => this;

    /// <summary>
    ///     Disposes all registered listeners synchronously.
    ///     Only disposes synchronous listeners. Use DisposeAsync for proper async disposal.
    /// </summary>
    public void Dispose() =>
        LockHelper.Execute(_disposeLock,
            () =>
            {
                if (_disposed) return;

                _disposed = true;

                // Dispose synchronous listeners
                foreach (var listener in _listeners)
                {
                    try
                    {
                        listener.Dispose();
                    }
                    catch
                    {
                        // Swallow exceptions during disposal to ensure all listeners are disposed
                    }
                }

                _listeners.Clear();

                // For async listeners, we can only dispose them synchronously (not ideal but necessary for IDisposable)
                // Users should prefer DisposeAsync for proper async disposal
                foreach (var listener in _asyncListeners)
                {
                    try
                    {
                        var valueTask = listener.DisposeAsync();
                        if (!valueTask.IsCompleted) valueTask.AsTask().GetAwaiter().GetResult();
                    }
                    catch
                    {
                        // Swallow exceptions during disposal to ensure all listeners are disposed
                    }
                }

                _asyncListeners.Clear();
            });

    /// <summary>
    ///     Disposes all registered listeners asynchronously.
    ///     This is the preferred disposal method for proper async cleanup.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        var shouldDispose = false;

        LockHelper.Execute(_disposeLock,
            () =>
            {
                if (_disposed) return;

                _disposed = true;
                shouldDispose = true;
            });

        if (!shouldDispose) return;

        // Dispose async listeners first (preferred)
        foreach (var listener in _asyncListeners)
        {
            try
            {
                await listener.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // Swallow exceptions during disposal to ensure all listeners are disposed
            }
        }

        _asyncListeners.Clear();

        // Then dispose synchronous listeners
        foreach (var listener in _listeners)
        {
            try
            {
                listener.Dispose();
            }
            catch
            {
                // Swallow exceptions during disposal to ensure all listeners are disposed
            }
        }

        _listeners.Clear();
    }
}