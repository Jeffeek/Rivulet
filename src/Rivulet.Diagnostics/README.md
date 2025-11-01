# Rivulet.Diagnostics

Enterprise observability for Rivulet.Core with EventListener wrappers, metric aggregators, and health check integration.

## Features

- **EventListener Wrappers**: Console, File, and Structured JSON logging
- **Metrics Aggregation**: Time-window based metric aggregation with statistics
- **Prometheus Export**: Export metrics in Prometheus text format
- **Health Check Integration**: Microsoft.Extensions.Diagnostics.HealthChecks support
- **Fluent Builder API**: Easy configuration with DiagnosticsBuilder

## Installation

```bash
dotnet add package Rivulet.Diagnostics
```

## Quick Start

### Console Listener

```csharp
using Rivulet.Diagnostics;

using var listener = new RivuletConsoleListener();

await Enumerable.Range(1, 100)
    .ToAsyncEnumerable()
    .SelectParallelAsync(async x =>
    {
        await ProcessAsync(x);
        return x;
    }, new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 10
    });
```

### File Listener with Rotation

```csharp
using var listener = new RivuletFileListener(
    "metrics.log", 
    maxFileSizeBytes: 10 * 1024 * 1024 // 10MB
);

// Your parallel operations...
```

### Structured JSON Logging

```csharp
using var listener = new RivuletStructuredLogListener("metrics.json");

// Or with custom action
using var listener = new RivuletStructuredLogListener(json =>
{
    // Send to your logging system
    logger.LogInformation(json);
});
```

### Metrics Aggregation

```csharp
using var aggregator = new MetricsAggregator(TimeSpan.FromSeconds(10));
aggregator.OnAggregation += metrics =>
{
    foreach (var metric in metrics)
    {
        Console.WriteLine($"{metric.DisplayName}:");
        Console.WriteLine($"  Min: {metric.Min:F2}");
        Console.WriteLine($"  Max: {metric.Max:F2}");
        Console.WriteLine($"  Avg: {metric.Average:F2}");
        Console.WriteLine($"  Current: {metric.Current:F2}");
    }
};

// Your parallel operations...
```

### Prometheus Export

```csharp
using var exporter = new PrometheusExporter();

// Your parallel operations...

// Export to Prometheus format
var prometheusText = exporter.Export();
await File.WriteAllTextAsync("metrics.prom", prometheusText);

// Or get as dictionary
var metrics = exporter.ExportDictionary();
```

### Health Check Integration

```csharp
// In Program.cs or Startup.cs
builder.Services.AddHealthChecks()
    .AddCheck<RivuletHealthCheck>("rivulet", tags: new[] { "ready" });

// Configure thresholds
builder.Services.Configure<RivuletHealthCheckOptions>(options =>
{
    options.ErrorRateThreshold = 0.1; // 10% error rate
    options.FailureCountThreshold = 100;
});

// Add health check endpoint
app.MapHealthChecks("/health");
```

### Fluent Builder API

```csharp
using var diagnostics = new DiagnosticsBuilder()
    .AddConsoleListener()
    .AddFileListener("metrics.log")
    .AddStructuredLogListener("metrics.json")
    .AddMetricsAggregator(TimeSpan.FromSeconds(10), metrics =>
    {
        // Handle aggregated metrics
    })
    .AddPrometheusExporter(out var exporter)
    .Build();

// Your parallel operations...

// Export Prometheus metrics
var prometheusText = exporter.Export();
```

## Available Metrics

Rivulet.Diagnostics exposes the following metrics from Rivulet.Core:

- **items-started**: Total number of items that have started processing
- **items-completed**: Total number of items that have completed processing
- **total-retries**: Total number of retry attempts
- **total-failures**: Total number of failed items
- **throttle-events**: Number of throttle events (backpressure)
- **drain-events**: Number of drain events

## Advanced Usage

### Custom Metric Thresholds

```csharp
var healthCheck = new RivuletHealthCheck(new RivuletHealthCheckOptions
{
    ErrorRateThreshold = 0.05, // 5% error rate
    FailureCountThreshold = 50
});
```

### Multiple Listeners

```csharp
using var console = new RivuletConsoleListener();
using var file = new RivuletFileListener("metrics.log");
using var structured = new RivuletStructuredLogListener("metrics.json");
using var aggregator = new MetricsAggregator(TimeSpan.FromSeconds(5));

// All listeners will receive metrics simultaneously
```

## Integration with Monitoring Systems

### Application Insights

```csharp
using var listener = new RivuletStructuredLogListener(json =>
{
    telemetryClient.TrackEvent("RivuletMetrics", new Dictionary<string, string>
    {
        ["metrics"] = json
    });
});
```

### ELK Stack

```csharp
using var listener = new RivuletStructuredLogListener("metrics.json");
// Configure Filebeat to ship metrics.json to Elasticsearch
```

### Prometheus

```csharp
using var exporter = new PrometheusExporter();

// Expose metrics endpoint
app.MapGet("/metrics", () => exporter.Export());
```

## Performance

- **Zero-cost when not listening**: EventCounters have zero overhead when no listeners are attached
- **Minimal allocation**: Uses polling counters to avoid allocations
- **Thread-safe**: All listeners are thread-safe and can be used concurrently

## Requirements

- .NET 8.0 or .NET 9.0
- Rivulet.Core 1.2.0+
- Microsoft.Extensions.Diagnostics.HealthChecks 9.0.0+ (for health checks)

## License

MIT License - see LICENSE file for details

## Related Packages

- **Rivulet.Core**: Core parallel operators - [NuGet](https://www.nuget.org/packages/Rivulet.Core/)
- **Rivulet.Diagnostics.OpenTelemetry**: OpenTelemetry integration for distributed tracing - [NuGet](https://www.nuget.org/packages/Rivulet.Diagnostics.OpenTelemetry/) | [Documentation](../Rivulet.Diagnostics.OpenTelemetry/README.md)
