# Rivulet Samples

This directory contains complete working examples demonstrating how to use all Rivulet packages in real-world scenarios.

## Available Samples

### 1. Rivulet.ConsoleSample
**Package:** `Rivulet.Core`

A comprehensive console application demonstrating all core parallel operators:

- **SelectParallelAsync**: Process items and collect results with retry logic
- **SelectParallelStreamAsync**: Stream results as they complete
- **ForEachParallelAsync**: Fire-and-forget parallel processing
- **BatchParallelAsync**: Process items in batches
- **Error Handling**: Different error modes (StopOnFirstError, CollectAndContinue)

**Run:**
```bash
cd Rivulet.ConsoleSample
dotnet run
```

**Key Features:**
- Bounded concurrency control
- Retry policies with exponential backoff
- Ordered and unordered output
- Error handling strategies

---

### 2. Rivulet.Diagnostics.Sample
**Package:** `Rivulet.Diagnostics`

Demonstrates observability features for production monitoring:

- **RivuletConsoleListener**: Real-time metrics in console
- **RivuletFileListener**: File-based metrics logging with rotation
- **RivuletStructuredLogListener**: JSON structured logging for log aggregation
- **PrometheusExporter**: Prometheus text format export
- **RivuletHealthCheck**: Health check integration
- **MetricsAggregator**: Statistical analysis of operations

**Run:**
```bash
cd Rivulet.Diagnostics.Sample
dotnet run
```

**Key Features:**
- EventSource-based metrics
- Multiple export formats
- Health monitoring
- Throughput and error rate tracking

---

### 3. Rivulet.OpenTelemetry.Sample
**Package:** `Rivulet.Diagnostics.OpenTelemetry`

Shows how to integrate Rivulet with OpenTelemetry for distributed tracing:

- **Activity/Span Creation**: Automatic distributed tracing
- **Retry Tracking**: Record retry attempts with context
- **Error Recording**: Detailed error tracking with transient classification
- **Custom Attributes**: Attach business context to spans
- **Circuit Breaker Events**: Track state changes
- **Adaptive Concurrency**: Monitor concurrency adjustments

**Run:**
```bash
cd Rivulet.OpenTelemetry.Sample
dotnet run
```

**Key Features:**
- W3C Trace Context propagation
- OpenTelemetry Metrics and Traces
- Console exporter (replace with Jaeger/Zipkin/OTLP in production)
- Correlation across distributed systems

---

### 4. Rivulet.Testing.Sample
**Package:** `Rivulet.Testing`

Demonstrates testing utilities for writing reliable tests:

- **VirtualTimeProvider**: Control time for deterministic tests
- **ChaosInjector**: Inject failures and latency for resilience testing
- **ConcurrencyAsserter**: Verify concurrency limits
- **FakeChannel**: Deterministic channel behavior
- **DeterministicScheduler**: Predictable task execution

**Run:**
```bash
cd Rivulet.Testing.Sample
dotnet run
```

**Key Features:**
- Fast, deterministic tests
- Fault injection testing
- Concurrency verification
- No actual delays needed

---

### 5. Rivulet.Hosting.Sample
**Package:** `Rivulet.Hosting`

Complete ASP.NET Core application with background services:

**Components:**
- **DataProcessingWorker**: Background worker using `ParallelWorkerService`
- **QueueProcessingWorker**: Queue processor using `ParallelBackgroundService`
- **BatchController**: REST API endpoints using Rivulet
- **Health Checks**: Readiness and liveness probes
- **Configuration**: Load options from appsettings.json

**Run:**
```bash
cd Rivulet.Hosting.Sample
dotnet run
```

**Endpoints:**
- `GET /` - Application information
- `GET /health/ready` - Readiness check
- `GET /health/live` - Liveness check
- `GET /metrics` - Prometheus metrics
- `POST /api/batch/square` - Square numbers in parallel
- `POST /api/batch/fetch` - Fetch URLs in parallel
- `POST /api/batch/batch-sum` - Process batches

**Key Features:**
- Dependency injection integration
- Configuration binding
- Background services
- Health checks
- Prometheus metrics endpoint

**Example Requests:**

```bash
# Square numbers
curl -X POST http://localhost:5000/api/batch/square \
  -H "Content-Type: application/json" \
  -d '[1, 2, 3, 4, 5, 6, 7, 8, 9, 10]'

# Fetch URLs
curl -X POST http://localhost:5000/api/batch/fetch \
  -H "Content-Type: application/json" \
  -d '["https://httpbin.org/status/200", "https://httpbin.org/status/404"]'

# Batch sum
curl -X POST http://localhost:5000/api/batch/batch-sum \
  -H "Content-Type: application/json" \
  -d '{"numbers": [1,2,3,4,5,6,7,8,9,10], "batchSize": 3}'
```

---

## Running All Samples

To build all samples:
```bash
dotnet build
```

To run a specific sample:
```bash
cd <sample-directory>
dotnet run
```

## Configuration

### Rivulet.Hosting.Sample Configuration

Edit `appsettings.json` to configure Rivulet options:

```json
{
  "Rivulet": {
    "MaxDegreeOfParallelism": 10,
    "MaxRetries": 3,
    "BaseDelay": "00:00:00.5",
    "MaxDelay": "00:00:05",
    "BufferSize": 100,
    "ErrorMode": "CollectAndContinue",
    "OrderedOutput": false
  }
}
```

## Learning Path

1. **Start with Rivulet.ConsoleSample** to understand core operators
2. **Explore Rivulet.Diagnostics.Sample** for production observability
3. **Review Rivulet.OpenTelemetry.Sample** for distributed tracing
4. **Study Rivulet.Testing.Sample** for testing strategies
5. **Examine Rivulet.Hosting.Sample** for enterprise integration

## Next Steps

- Read the [API Documentation](../README.md)
- Review [ROADMAP.md](../ROADMAP.md) for upcoming features
- Check [RELEASE_GUIDE.md](../RELEASE_GUIDE.md) for release process
- Contribute on [GitHub](https://github.com/your-org/Rivulet)

## Support

For questions or issues:
- Open an issue on GitHub
- Check existing documentation
- Review test projects for more examples
