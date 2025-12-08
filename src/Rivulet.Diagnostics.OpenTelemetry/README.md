# Rivulet.Diagnostics.OpenTelemetry

OpenTelemetry integration for Rivulet.Core providing distributed tracing, metrics export, and comprehensive observability.

## Installation

```bash
dotnet add package Rivulet.Diagnostics.OpenTelemetry
```

## Features

- **Distributed Tracing**: Automatic activity creation with parent-child relationships
- **Metrics Export**: Bridge EventCounters to OpenTelemetry Meters
- **Retry Tracking**: Record retry attempts as activity events
- **Circuit Breaker Events**: Track circuit state changes in traces
- **Adaptive Concurrency**: Monitor concurrency adjustments
- **Error Correlation**: Link errors with retry attempts and transient classification

## Quick Start

### 1. Configure OpenTelemetry

```csharp
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using Rivulet.Diagnostics.OpenTelemetry;

// At application startup
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("MyService"))
    .AddSource(RivuletActivitySource.SourceName)
    .AddJaegerExporter(options =>
    {
        options.AgentHost = "localhost";
        options.AgentPort = 6831;
    })
    .Build();

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("MyService"))
    .AddMeter(RivuletMetricsExporter.MeterName)
    .AddPrometheusExporter()
    .Build();

// Create metrics exporter
// IMPORTANT: Must remain alive for the duration of the application
// Bridges EventCounters from Rivulet.Core to OpenTelemetry Meters
// Disposing it stops the metrics export
using var metricsExporter = new RivuletMetricsExporter();
```

### 2. Use with Rivulet Operations

```csharp
using Rivulet.Core;
using Rivulet.Diagnostics.OpenTelemetry;

var urls = new[] { "https://api.example.com/1", "https://api.example.com/2", /* ... */ };

var options = new ParallelOptionsRivulet
{
    MaxDegreeOfParallelism = 32,
    MaxRetries = 3,
    IsTransient = ex => ex is HttpRequestException,
    ErrorMode = ErrorMode.CollectAndContinue
}.WithOpenTelemetryTracing("FetchUrls");

var results = await urls.SelectParallelAsync(
    async (url, ct) =>
    {
        using var response = await httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    },
    options);
```

## Distributed Tracing

### Activity Hierarchy

Each parallel operation creates activities with this structure:

```
Rivulet.FetchUrls                    [Root Activity]
├── Rivulet.FetchUrls.Item          [Item 0]
│   ├── Tags: rivulet.item_index=0
│   └── Status: Ok
├── Rivulet.FetchUrls.Item          [Item 1 - with retry]
│   ├── Tags: rivulet.item_index=1
│   ├── Events:
│   │   └── retry (attempt=1, exception.type=HttpRequestException)
│   └── Status: Ok
└── Rivulet.FetchUrls.Item          [Item 2 - failed]
    ├── Tags: rivulet.item_index=2, rivulet.error.transient=false
    ├── Exception: InvalidOperationException
    └── Status: Error
```

### Activity Tags

| Tag | Description |
|-----|-------------|
| `rivulet.item_index` | Index of the item being processed |
| `rivulet.total_items` | Total number of items (on root activity) |
| `rivulet.retries` | Number of retry attempts |
| `rivulet.error.transient` | Whether error is transient |
| `rivulet.items_processed` | Items successfully processed |
| `rivulet.concurrency.current` | Current concurrency level |
| `rivulet.circuit_breaker.state` | Circuit breaker state |

### Activity Events

| Event | Description | Tags |
|-------|-------------|------|
| `retry` | Retry attempt occurred | `rivulet.retry_attempt`, `exception.type`, `exception.message` |
| `circuit_breaker_state_change` | Circuit breaker changed state | `rivulet.circuit_breaker.state` |
| `adaptive_concurrency_change` | Concurrency level adjusted | `rivulet.concurrency.old`, `rivulet.concurrency.new` |

## Metrics Export

The `RivuletMetricsExporter` bridges Rivulet's EventCounters to OpenTelemetry Meters:

### Available Metrics

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `rivulet.items.started` | Gauge | {items} | Total items started |
| `rivulet.items.completed` | Gauge | {items} | Total items completed |
| `rivulet.retries.total` | Gauge | {retries} | Total retry attempts |
| `rivulet.failures.total` | Gauge | {failures} | Total failures after retries |
| `rivulet.throttle.events` | Gauge | {events} | Backpressure throttle events |
| `rivulet.drain.events` | Gauge | {events} | Channel drain events |
| `rivulet.error.rate` | Gauge | {ratio} | Error rate (failures/started) |

### Example with Prometheus

```csharp
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Exporter.Prometheus;

var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddMeter(RivuletMetricsExporter.MeterName)
    .AddPrometheusHttpListener(options =>
    {
        options.UriPrefixes = new[] { "http://localhost:9090/" };
    })
    .Build();

// Metrics available at http://localhost:9090/metrics

// Create exporter and keep it alive for the application lifetime
// It automatically bridges Rivulet EventCounters to OpenTelemetry
using var exporter = new RivuletMetricsExporter();

// Use Rivulet normally - metrics automatically exported
var results = await items.SelectParallelAsync(processAsync, options);
```

## Advanced Usage

### Retry Tracking

Track individual retry attempts in trace spans:

```csharp
var options = new ParallelOptionsRivulet
{
    MaxRetries = 5,
    BaseDelay = TimeSpan.FromMilliseconds(100),
    BackoffStrategy = BackoffStrategy.ExponentialJitter,
    IsTransient = ex => ex is HttpRequestException or TimeoutException
}.WithOpenTelemetryTracingAndRetries("ProcessWithRetries", trackRetries: true);

// Each retry creates an activity event with exception details
var results = await urls.SelectParallelAsync(processAsync, options);
```

### Circuit Breaker Monitoring

Monitor circuit breaker state changes in traces:

```csharp
var options = new ParallelOptionsRivulet
{
    MaxDegreeOfParallelism = 32,
    CircuitBreaker = new CircuitBreakerOptions
    {
        FailureThreshold = 5,
        OpenTimeout = TimeSpan.FromSeconds(30),
        OnStateChange = async (oldState, newState) =>
        {
            // State changes are automatically recorded in current activity
            logger.LogWarning($"Circuit breaker: {oldState} → {newState}");
        }
    }
}.WithOpenTelemetryTracing("ResilientOperation");

var results = await items.SelectParallelAsync(processAsync, options);
```

### Adaptive Concurrency Tracking

Track concurrency adjustments:

```csharp
var options = new ParallelOptionsRivulet
{
    AdaptiveConcurrency = new AdaptiveConcurrencyOptions
    {
        MinConcurrency = 1,
        MaxConcurrency = 64,
        TargetLatency = TimeSpan.FromMilliseconds(100),
        OnConcurrencyChange = async (oldValue, newValue) =>
        {
            // Changes automatically recorded in current activity
            logger.LogInformation($"Concurrency adjusted: {oldValue} → {newValue}");
        }
    }
}.WithOpenTelemetryTracing("AdaptiveOperation");

var results = await items.SelectParallelAsync(processAsync, options);
```

## Integration with Observability Platforms

### Jaeger

```csharp
using OpenTelemetry.Trace;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource(RivuletActivitySource.SourceName)
    .AddJaegerExporter(options =>
    {
        options.AgentHost = "jaeger-host";
        options.AgentPort = 6831;
    })
    .Build();
```

### Azure Monitor (Application Insights)

```csharp
using Azure.Monitor.OpenTelemetry.Exporter;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource(RivuletActivitySource.SourceName)
    .AddAzureMonitorTraceExporter(options =>
    {
        options.ConnectionString = "InstrumentationKey=...";
    })
    .Build();
```

### DataDog

```csharp
using OpenTelemetry.Exporter;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource(RivuletActivitySource.SourceName)
    .AddOtlpExporter(options =>
    {
        options.Endpoint = new Uri("https://trace.agent.datadoghq.com:4318");
    })
    .Build();
```

### Zipkin

```csharp
using OpenTelemetry.Trace;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource(RivuletActivitySource.SourceName)
    .AddZipkinExporter(options =>
    {
        options.Endpoint = new Uri("http://zipkin-host:9411/api/v2/spans");
    })
    .Build();
```

## Best Practices

1. **Configure Once**: Set up OpenTelemetry at application startup
2. **Use Operation Names**: Provide meaningful operation names for tracing
3. **Sample Appropriately**: Use sampling for high-throughput scenarios
4. **Monitor Error Rate**: Alert on `rivulet.error.rate` metric
5. **Track Retries**: Enable retry tracking for transient error analysis
6. **Correlate Traces**: Use W3C TraceContext for cross-service correlation
7. **Keep Exporter Alive**: RivuletMetricsExporter must remain alive for metrics export
   - In web apps: Register as singleton service
   - In console apps: Keep reference until application exits
   - Disposing stops the EventCounter listener and metrics collection

## Performance

- **Minimal Overhead**: Activities only created when tracing is enabled
- **Async-Safe**: All operations use `AsyncLocal<T>` for proper async context flow
- **Zero Allocations**: When tracing is disabled, no activities are created
- **Sampling Friendly**: Respects OpenTelemetry sampling decisions

## Framework Support

- .NET 8.0
- .NET 9.0

## Dependencies

- **Rivulet.Core** (≥ 1.2.0)
- **OpenTelemetry.Api** (≥ 1.13.1)
- **System.Diagnostics.DiagnosticSource** (≥ 9.0.0)

## License

MIT License - see LICENSE file for details

---

**Made with ❤️ by Jeffeek** | [NuGet](https://www.nuget.org/packages/Rivulet.Diagnostics.OpenTelemetry/) | [GitHub](https://github.com/Jeffeek/Rivulet)
