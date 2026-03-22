<div align="center">
  <img src="assets/readme_logo.png" alt="Rivulet Logo" width="600">
</div>

<div align="center">

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Codecov](https://codecov.io/gh/Jeffeek/Rivulet/branch/master/graph/badge.svg)](https://codecov.io/gh/Jeffeek/Rivulet)
[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/Jeffeek/Rivulet/badge)](https://scorecard.dev/viewer/?uri=github.com/Jeffeek/Rivulet)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Documentation](https://img.shields.io/readthedocs/rivulet2?label=docs)](https://rivulet2.readthedocs.io/)

![CI/CD Pipeline](https://img.shields.io/github/actions/workflow/status/Jeffeek/Rivulet/release.yml?label=RELEASE)
![CI/CD Pipeline](https://img.shields.io/github/actions/workflow/status/Jeffeek/Rivulet/ci.yml?label=CI)
![CI/CD Pipeline](https://img.shields.io/github/actions/workflow/status/Jeffeek/Rivulet/codeql.yml?label=CodeQL)
![CI/CD Pipeline](https://img.shields.io/github/actions/workflow/status/Jeffeek/Rivulet/flaky-test-detection.yml?label=FlakyTests)

</div>

---

<div align="center">

**Safe, async-first parallel operators with bounded concurrency, retries, cancellation, and streaming backpressure for I/O-heavy workloads.**

📚 **[Read the Full Documentation](https://rivulet2.readthedocs.io/)**

</div>

---

## 📦 Packages

<!-- PACKAGES_START -->
### Core Packages

#### ✅ [Rivulet.Core](https://www.nuget.org/packages/Rivulet.Core)
[![NuGet](https://img.shields.io/nuget/v/Rivulet.Core.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Core) [![Downloads](https://img.shields.io/nuget/dt/Rivulet.Core.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Core)

Core parallel processing operators with bounded concurrency, retry policies, and error handling

**Key Features:**
- Bounded concurrency control
- Retry policies with exponential backoff
- Circuit breaker pattern
- Error handling modes (StopOnFirstError, CollectAndContinue)
- Ordered and unordered output

#### ✅ [Rivulet.Diagnostics](https://www.nuget.org/packages/Rivulet.Diagnostics)
[![NuGet](https://img.shields.io/nuget/v/Rivulet.Diagnostics.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Diagnostics) [![Downloads](https://img.shields.io/nuget/dt/Rivulet.Diagnostics.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Diagnostics)

Production-ready observability with EventSource metrics, structured logging, and health checks

**Key Features:**
- EventSource-based metrics (ETW, EventPipe)
- Multiple export formats
- Health monitoring
- Throughput and error rate tracking
- Zero allocation in hot paths

#### ✅ [Rivulet.Diagnostics.OpenTelemetry](https://www.nuget.org/packages/Rivulet.Diagnostics.OpenTelemetry)
[![NuGet](https://img.shields.io/nuget/v/Rivulet.Diagnostics.OpenTelemetry.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Diagnostics.OpenTelemetry) [![Downloads](https://img.shields.io/nuget/dt/Rivulet.Diagnostics.OpenTelemetry.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Diagnostics.OpenTelemetry)

OpenTelemetry integration for distributed tracing and W3C Trace Context propagation

**Key Features:**
- W3C Trace Context propagation
- OpenTelemetry Metrics and Traces
- Correlation across distributed systems
- Integration with Jaeger/Zipkin/OTLP exporters

#### ✅ [Rivulet.Hosting](https://www.nuget.org/packages/Rivulet.Hosting)
[![NuGet](https://img.shields.io/nuget/v/Rivulet.Hosting.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Hosting) [![Downloads](https://img.shields.io/nuget/dt/Rivulet.Hosting.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Hosting)

ASP.NET Core integration with background services, dependency injection, and configuration binding

**Key Features:**
- Dependency injection integration
- Configuration binding (appsettings.json)
- Background services
- Health checks
- Graceful shutdown support

#### ✅ [Rivulet.Testing](https://www.nuget.org/packages/Rivulet.Testing)
[![NuGet](https://img.shields.io/nuget/v/Rivulet.Testing.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Testing) [![Downloads](https://img.shields.io/nuget/dt/Rivulet.Testing.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Testing)

Testing utilities for deterministic tests with time control, chaos injection, and concurrency verification

**Key Features:**
- Fast deterministic tests
- Fault injection testing
- Concurrency verification
- No actual delays needed
- Integration with xUnit/NUnit/MSTest

#### ✅ [Rivulet.Http](https://www.nuget.org/packages/Rivulet.Http)
[![NuGet](https://img.shields.io/nuget/v/Rivulet.Http.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Http) [![Downloads](https://img.shields.io/nuget/dt/Rivulet.Http.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Http)

Parallel HTTP operations with HttpClientFactory integration and connection pooling awareness

**Key Features:**
- HttpClientFactory integration
- Connection pooling awareness
- Transient error handling (timeouts, 5xx responses)
- Bounded concurrency to avoid overwhelming servers
- Progress reporting for downloads

#### ✅ [Rivulet.IO](https://www.nuget.org/packages/Rivulet.IO)
[![NuGet](https://img.shields.io/nuget/v/Rivulet.IO.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.IO) [![Downloads](https://img.shields.io/nuget/dt/Rivulet.IO.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.IO)

Parallel file operations with safe directory processing and file transformations

**Key Features:**
- Safe concurrent file access
- Directory tree processing
- File pattern matching (glob patterns)
- Progress reporting
- Atomic write operations

#### ✅ [Rivulet.Sql](https://www.nuget.org/packages/Rivulet.Sql)
[![NuGet](https://img.shields.io/nuget/v/Rivulet.Sql.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Sql) [![Downloads](https://img.shields.io/nuget/dt/Rivulet.Sql.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Sql)

Provider-agnostic parallel SQL operations with connection pooling awareness

**Key Features:**
- Works with any ADO.NET provider
- Connection pooling awareness
- Transaction support
- Parameterized queries
- Respects database connection pool limits

#### ✅ [Rivulet.Sql.SqlServer](https://www.nuget.org/packages/Rivulet.Sql.SqlServer)
[![NuGet](https://img.shields.io/nuget/v/Rivulet.Sql.SqlServer.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Sql.SqlServer) [![Downloads](https://img.shields.io/nuget/dt/Rivulet.Sql.SqlServer.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Sql.SqlServer)

SQL Server optimizations with SqlBulkCopy integration (10-100x faster bulk inserts)

**Key Features:**
- SqlBulkCopy integration (10-100x faster)
- Batch size optimization
- Table-valued parameters
- Progress reporting
- Automatic table creation

#### ✅ [Rivulet.Sql.PostgreSql](https://www.nuget.org/packages/Rivulet.Sql.PostgreSql)
[![NuGet](https://img.shields.io/nuget/v/Rivulet.Sql.PostgreSql.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Sql.PostgreSql) [![Downloads](https://img.shields.io/nuget/dt/Rivulet.Sql.PostgreSql.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Sql.PostgreSql)

PostgreSQL optimizations with COPY command integration (10-100x faster bulk operations)

**Key Features:**
- COPY command integration (10-100x faster)
- Binary and text format support
- Progress reporting
- Automatic table creation

#### ✅ [Rivulet.Sql.MySql](https://www.nuget.org/packages/Rivulet.Sql.MySql)
[![NuGet](https://img.shields.io/nuget/v/Rivulet.Sql.MySql.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Sql.MySql) [![Downloads](https://img.shields.io/nuget/dt/Rivulet.Sql.MySql.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Sql.MySql)

MySQL optimizations with LOAD DATA INFILE integration using MySqlBulkLoader

**Key Features:**
- MySqlBulkLoader integration (10-100x faster)
- Local and remote file loading
- Progress reporting
- Automatic table creation

#### ✅ [Rivulet.Polly](https://www.nuget.org/packages/Rivulet.Polly)
[![NuGet](https://img.shields.io/nuget/v/Rivulet.Polly.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Polly) [![Downloads](https://img.shields.io/nuget/dt/Rivulet.Polly.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Polly)

Polly v8 integration with hedging, result-based retry, and resilience pipeline composition

**Key Features:**
- Polly v8 ResiliencePipeline integration
- Hedging pattern support
- Result-based retry policies
- Policy composition
- Fallback strategies

#### 🚧 [Rivulet.Csv](https://www.nuget.org/packages/Rivulet.Csv)
[![NuGet](https://img.shields.io/nuget/v/Rivulet.Csv.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Csv) [![Downloads](https://img.shields.io/nuget/dt/Rivulet.Csv.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Csv)

Parallel CSV parsing and writing for Rivulet with CsvHelper integration, bounded concurrency, and batching support for high-throughput data processing

**Key Features:**
- CsvHelper integration for robust CSV parsing
- Multi-type operations (2-5 generic type parameters)
- Memory-efficient streaming with IAsyncEnumerable
- Per-file CSV configuration (delimiters, culture, class maps)
- Progress tracking and lifecycle callbacks
- Error handling modes (FailFast, CollectAndContinue, BestEffort)
- Circuit breaker and retry support
- Ordered and unordered output options

#### 🚧 [Rivulet.Pipeline](https://www.nuget.org/packages/Rivulet.Pipeline)
[![NuGet](https://img.shields.io/nuget/v/Rivulet.Pipeline.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Pipeline) [![Downloads](https://img.shields.io/nuget/dt/Rivulet.Pipeline.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Pipeline)

Multi-stage pipeline composition for Rivulet with fluent API, per-stage concurrency, backpressure management between stages, and streaming support

**Key Features:**
- Fluent builder API with type-safe stage chaining
- Per-stage concurrency configuration via StageOptions
- Backpressure management using System.Threading.Channels
- Reuses Core components (TokenBucket, ParallelOptionsRivulet)
- Pipeline lifecycle callbacks (start, complete, stage events)
- Per-stage metrics tracking (items in/out, timing)
- Retry policies, circuit breaker, and error modes per stage
- Cancellation support propagated through all stages
- Streaming execution with IAsyncEnumerable

### Integration Packages (v1.4.0 🚧)
<!-- PACKAGES_END -->

---

## 🚀 Features

- Async-first (`ValueTask`), works with `IEnumerable<T>` and `IAsyncEnumerable<T>`
- Bounded concurrency with backpressure (Channels)
- Retry policy with transient detection and configurable backoff strategies (Exponential, ExponentialJitter, DecorrelatedJitter, Linear, LinearJitter)
- Per-item timeouts, cancellation, lifecycle hooks
- Flexible error modes: FailFast, CollectAndContinue, BestEffort
- Ordered output mode for sequence-sensitive operations

---

## 🚦 Quick Start

```csharp
var results = await urls.SelectParallelAsync(
    async (url, ct) =>
    {
        using var resp = await http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        return (url, (int)resp.StatusCode);
    },
    new ParallelOptionsRivulet {
        MaxDegreeOfParallelism = 32,
        MaxRetries = 3,
        IsTransient = ex => ex is HttpRequestException or TaskCanceledException,
        ErrorMode = ErrorMode.CollectAndContinue
    });
```

---

## 📖 Core Features

### Streaming Results

```csharp
await foreach (var r in source.SelectParallelStreamAsync(
    async (x, ct) => await ComputeAsync(x, ct),
    new ParallelOptionsRivulet { MaxDegreeOfParallelism = 16 }))
{
    // consume incrementally
}
```

### Ordered Output

Maintain input order when sequence matters:

```csharp
// Results returned in same order as input, despite parallel processing
var results = await items.SelectParallelAsync(
    async (item, ct) => await ProcessAsync(item, ct),
    new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 32,
        OrderedOutput = true  // Ensures results match input order
    });

// Streaming with ordered output
await foreach (var result in source.SelectParallelStreamAsync(
    async (x, ct) => await TransformAsync(x, ct),
    new ParallelOptionsRivulet { OrderedOutput = true }))
{
    // Results arrive in input order
}
```

### Backoff Strategies

Choose from multiple backoff strategies to optimize retry behavior:

```csharp
// Exponential backoff with jitter - recommended for rate-limited APIs
// Reduces thundering herd by randomizing retry delays
var results = await requests.SelectParallelAsync(
    async (req, ct) => await apiClient.SendAsync(req, ct),
    new ParallelOptionsRivulet
    {
        MaxRetries = 4,
        BaseDelay = TimeSpan.FromMilliseconds(100),
        BackoffStrategy = BackoffStrategy.ExponentialJitter,  // Random(0, BaseDelay * 2^attempt)
        IsTransient = ex => ex is HttpRequestException
    });

// Decorrelated jitter - best for preventing synchronization across multiple clients
var results = await tasks.SelectParallelAsync(
    async (task, ct) => await ProcessAsync(task, ct),
    new ParallelOptionsRivulet
    {
        MaxRetries = 3,
        BackoffStrategy = BackoffStrategy.DecorrelatedJitter,  // Random based on previous delay
        IsTransient = ex => ex is TimeoutException
    });

// Linear backoff - gentler, predictable increase
var results = await items.SelectParallelAsync(
    async (item, ct) => await SaveAsync(item, ct),
    new ParallelOptionsRivulet
    {
        MaxRetries = 5,
        BaseDelay = TimeSpan.FromSeconds(1),
        BackoffStrategy = BackoffStrategy.Linear,  // BaseDelay * attempt
        IsTransient = ex => ex is InvalidOperationException
    });
```

**Available strategies:**
- **Exponential** *(default)*: `BaseDelay * 2^(attempt-1)` - Predictable exponential growth
- **ExponentialJitter**: `Random(0, BaseDelay * 2^(attempt-1))` - Reduces thundering herd
- **DecorrelatedJitter**: `Random(BaseDelay, PreviousDelay * 3)` - Prevents client synchronization
- **Linear**: `BaseDelay * attempt` - Gentler, linear growth
- **LinearJitter**: `Random(0, BaseDelay * attempt)` - Linear with randomization

### Progress Reporting

Track progress with real-time metrics for long-running operations:

```csharp
// Monitor ETL job progress with ETA
var records = await database.GetRecordsAsync().SelectParallelAsync(
    async (record, ct) => await TransformAndLoadAsync(record, ct),
    new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 20,
        Progress = new ProgressOptions
        {
            ReportInterval = TimeSpan.FromSeconds(5),
            OnProgress = progress =>
            {
                Console.WriteLine($"Progress: {progress.ItemsCompleted}/{progress.TotalItems}");
                Console.WriteLine($"Rate: {progress.ItemsPerSecond:F1} items/sec");
                Console.WriteLine($"ETA: {progress.EstimatedTimeRemaining}");
                Console.WriteLine($"Errors: {progress.ErrorCount}");
                return ValueTask.CompletedTask;
            }
        }
    });

// Streaming progress (total unknown)
await foreach (var result in stream.SelectParallelStreamAsync(
    async (item, ct) => await ProcessAsync(item, ct),
    new ParallelOptionsRivulet
    {
        Progress = new ProgressOptions
        {
            ReportInterval = TimeSpan.FromSeconds(10),
            OnProgress = progress =>
            {
                // No ETA or percent for streams - total is unknown
                Console.WriteLine($"Processed: {progress.ItemsCompleted}");
                Console.WriteLine($"Rate: {progress.ItemsPerSecond:F1} items/sec");
                return ValueTask.CompletedTask;
            }
        }
    }))
{
    // Process results as they arrive
}
```

**Progress metrics:**
- **ItemsStarted**: Total items that began processing
- **ItemsCompleted**: Successfully completed items
- **TotalItems**: Total count (known for arrays/lists, null for streams)
- **ErrorCount**: Failed items across all retries
- **Elapsed**: Time since operation started
- **ItemsPerSecond**: Processing rate
- **EstimatedTimeRemaining**: ETA (when total is known)
- **PercentComplete**: 0-100% (when total is known)

### Batching Operations

Process items in batches for bulk operations like database inserts, batch API calls, or file operations:

```csharp
// Bulk database inserts - batch 100 records at a time
var results = await records.BatchParallelAsync(
    batchSize: 100,
    async (batch, ct) =>
    {
        // Insert entire batch in a single database call
        await db.BulkInsertAsync(batch, ct);
        return batch.Count;
    },
    new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 4, // Process 4 batches in parallel
        MaxRetries = 3,
        IsTransient = ex => ex is SqlException
    });

// Batch API calls with timeout - flush partial batches after delay
var apiResults = await items.BatchParallelAsync(
    batchSize: 50,
    async (batch, ct) =>
    {
        // Call API with batch of items
        return await apiClient.ProcessBatchAsync(batch, ct);
    },
    batchTimeout: TimeSpan.FromSeconds(2) // Flush batch after 2 seconds even if not full
);

// Streaming batches from async source
await foreach (var result in dataStream.BatchParallelStreamAsync(
    batchSize: 100,
    async (batch, ct) =>
    {
        await ProcessBatchAsync(batch, ct);
        return batch.Count;
    },
    new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 8,
        OrderedOutput = true, // Maintain batch order
        Progress = new ProgressOptions
        {
            ReportInterval = TimeSpan.FromSeconds(5),
            OnProgress = progress =>
            {
                Console.WriteLine($"Batches processed: {progress.ItemsCompleted}");
                return ValueTask.CompletedTask;
            }
        }
    }))
{
    // Process batch results as they complete
    Console.WriteLine($"Batch completed with {result} items");
}
```

**Key Features:**
- **Size-based batching**: Groups items into batches of specified size
- **Timeout-based flushing**: Optional timeout to flush incomplete batches (async streams only)
- **Parallel batch processing**: Process multiple batches concurrently with bounded parallelism
- **All existing features**: Works with retries, error handling, progress tracking, ordered output
- **Efficient for bulk operations**: Reduces API calls, database round-trips, and I/O overhead

**Use Cases:**
- Bulk database inserts/updates/deletes
- Batch API calls to external services
- File processing in chunks
- Message queue batch processing
- ETL pipelines with staged operations

### Runtime Metrics & EventCounters

Monitor parallel operations with built-in metrics via .NET EventCounters and optional callbacks for custom monitoring systems:

```csharp
// Zero-cost monitoring with EventCounters (always enabled)
// Monitor with: dotnet-counters monitor --process-id <PID> --counters Rivulet.Core
var results = await items.SelectParallelAsync(ProcessAsync, options);

// Custom metrics callback for Prometheus, DataDog, Application Insights
var options = new ParallelOptionsRivulet
{
    MaxDegreeOfParallelism = 32,
    Metrics = new MetricsOptions
    {
        SampleInterval = TimeSpan.FromSeconds(10),
        OnMetricsSample = async snapshot =>
        {
            // Export to your monitoring system
            await prometheus.RecordMetrics(new
            {
                active_workers = snapshot.ActiveWorkers,
                items_completed = snapshot.ItemsCompleted,
                throughput = snapshot.ItemsPerSecond,
                error_rate = snapshot.ErrorRate,
                total_retries = snapshot.TotalRetries
            });
        }
    }
};

var results = await urls.SelectParallelAsync(
    async (url, ct) => await httpClient.GetAsync(url, ct),
    options);
```

**Available Metrics:**
- **ActiveWorkers**: Current number of active worker tasks
- **QueueDepth**: Items waiting in the input channel queue
- **ItemsStarted**: Total items that began processing
- **ItemsCompleted**: Total items completed successfully
- **TotalRetries**: Cumulative retry attempts across all items
- **TotalFailures**: Total failed items (after all retries)
- **ThrottleEvents**: Backpressure events when queue is full
- **ItemsPerSecond**: Current throughput rate
- **ErrorRate**: Failure rate (TotalFailures / ItemsStarted)
- **Elapsed**: Time since operation started

**EventCounters (zero-cost monitoring):**
```bash
# Monitor in real-time with dotnet-counters
dotnet-counters monitor --process-id <PID> --counters Rivulet.Core

# Available counters:
# - items-started
# - items-completed
# - total-retries
# - total-failures
# - throttle-events
# - drain-events
```

**Key Features:**
- **Zero-cost when not monitored**: EventCounters have minimal overhead
- **Thread-safe**: Uses lock-free Interlocked operations
- **Callback isolation**: Exceptions in callbacks don't break operations
- **Integrates with all operators**: SelectParallelAsync, SelectParallelStreamAsync, ForEachParallelAsync, BatchParallel*

**Use Cases:**
- Production monitoring and alerting
- Performance tuning and capacity planning
- Debugging throughput issues
- SLA compliance verification
- Auto-scaling triggers

---

## 🔧 Advanced Features

### Rate Limiting with Token Bucket

Control the maximum rate of operations using the token bucket algorithm, perfect for respecting API rate limits or smoothing traffic bursts:

```csharp
// Limit to 100 requests/sec with burst capacity of 200
var results = await apiUrls.SelectParallelAsync(
    async (url, ct) => await httpClient.GetAsync(url, ct),
    new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 32,
        RateLimit = new RateLimitOptions
        {
            TokensPerSecond = 100,  // Sustained rate
            BurstCapacity = 200     // Allow brief bursts
        }
    });

// Heavy operations consume more tokens
var results = await heavyTasks.SelectParallelAsync(
    async (task, ct) => await ProcessHeavyAsync(task, ct),
    new ParallelOptionsRivulet
    {
        RateLimit = new RateLimitOptions
        {
            TokensPerSecond = 50,
            BurstCapacity = 50,
            TokensPerOperation = 5  // Each operation costs 5 tokens
        }
    });
```

**Key Features:**
- **Token bucket algorithm**: Allows controlled bursts while maintaining average rate
- **Configurable rates**: Set tokens per second and burst capacity
- **Weighted operations**: Different operations can consume different token amounts
- **Works with all operators**: SelectParallel*, ForEachParallel*, BatchParallel*
- **Combines with retries**: Rate limiting applies to retry attempts too

**Use Cases:**
- Respect API rate limits (e.g., 1000 requests/hour)
- Smooth traffic bursts to downstream services
- Prevent resource exhaustion
- Control database connection usage
- Implement fair resource sharing

### Circuit Breaker

Protect your application from cascading failures when a downstream service is unhealthy. The circuit breaker monitors for failures and automatically fails fast when a threshold is reached, giving the failing service time to recover.

```csharp
// Protect against a flaky API - open after 5 consecutive failures
var results = await urls.SelectParallelAsync(
    async (url, ct) => await httpClient.GetAsync(url, ct),
    new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 32,
        CircuitBreaker = new CircuitBreakerOptions
        {
            FailureThreshold = 5,                      // Open after 5 consecutive failures
            SuccessThreshold = 2,                       // Close after 2 consecutive successes in HalfOpen
            OpenTimeout = TimeSpan.FromSeconds(30),     // Test recovery after 30 seconds
            OnStateChange = async (from, to) =>
            {
                Console.WriteLine($"Circuit {from} → {to}");
                await LogCircuitStateChangeAsync(from, to);
            }
        }
    });

// Time-based failure tracking (percentage within window)
var results = await requests.SelectParallelAsync(
    async (req, ct) => await apiClient.SendAsync(req, ct),
    new ParallelOptionsRivulet
    {
        CircuitBreaker = new CircuitBreakerOptions
        {
            FailureThreshold = 10,                          // Open if 10 failures occur...
            SamplingDuration = TimeSpan.FromSeconds(60),     // ...within 60 seconds
            OpenTimeout = TimeSpan.FromMinutes(5)
        }
    });
```

**Circuit States:**
- **Closed**: Normal operation. Operations execute normally. Failures are tracked.
- **Open**: Failure threshold exceeded. Operations fail immediately with `CircuitBreakerOpenException` without executing. Prevents cascading failures.
- **HalfOpen**: After `OpenTimeout` expires, circuit allows limited operations to test recovery. Success transitions to Closed. Failure transitions back to Open.

**Key Features:**
- **Fail-fast protection**: Prevents overwhelming failing services
- **Automatic recovery testing**: Transitions to HalfOpen after timeout to probe health
- **Flexible failure tracking**: Consecutive failures or time-window based (with `SamplingDuration`)
- **State change callbacks**: Monitor circuit transitions for alerting/logging
- **Works with all operators**: SelectParallel*, ForEachParallel*, BatchParallel*

**Use Cases:**
- Protecting downstream microservices from overload
- Preventing cascading failures in distributed systems
- Graceful degradation when dependencies are unhealthy
- Reducing latency by failing fast instead of waiting for timeouts

### Adaptive Concurrency

Automatically adjust parallelism based on real-time performance metrics. Instead of using a fixed `MaxDegreeOfParallelism`, adaptive concurrency dynamically scales workers up when performance is good and scales down when latency increases or errors occur.

```csharp
// Auto-scale between 1-32 workers based on latency and success rate
var results = await urls.SelectParallelAsync(
    async (url, ct) => await httpClient.GetAsync(url, ct),
    new ParallelOptionsRivulet
    {
        AdaptiveConcurrency = new AdaptiveConcurrencyOptions
        {
            MinConcurrency = 1,                             // Lower bound
            MaxConcurrency = 32,                            // Upper bound
            InitialConcurrency = 8,                         // Starting point (optional)
            TargetLatency = TimeSpan.FromMilliseconds(100), // Target p50 latency
            MinSuccessRate = 0.95,                          // 95% success rate threshold
            SampleInterval = TimeSpan.FromSeconds(1),       // How often to adjust
            OnConcurrencyChange = async (old, @new) =>
            {
                Console.WriteLine($"Concurrency: {old} → {@new}");
                await metricsClient.RecordGaugeAsync("concurrency", @new);
            }
        }
    });

// Different adjustment strategies
var results = await tasks.SelectParallelAsync(
    async (task, ct) => await ProcessAsync(task, ct),
    new ParallelOptionsRivulet
    {
        AdaptiveConcurrency = new AdaptiveConcurrencyOptions
        {
            MinConcurrency = 2,
            MaxConcurrency = 64,
            IncreaseStrategy = AdaptiveConcurrencyStrategy.Aggressive, // Faster increase
            DecreaseStrategy = AdaptiveConcurrencyStrategy.Gradual,    // Slower decrease
            MinSuccessRate = 0.90
        }
    });
```

**How It Works:**
Uses AIMD (Additive Increase Multiplicative Decrease) algorithm similar to TCP congestion control:
- **Increase**: When success rate is high and latency is acceptable, add workers gradually (AIMD: +1, Aggressive: +10%)
- **Decrease**: When latency exceeds target or success rate drops, reduce workers sharply (AIMD/Aggressive: -50%, Gradual: -25%)
- Samples performance every `SampleInterval` and adjusts within `[MinConcurrency, MaxConcurrency]` bounds

**Adjustment Strategies:**
- **AIMD** (default): Additive Increase (+1), Multiplicative Decrease (-50%) - Like TCP
- **Aggressive**: Faster increase (+10%), same decrease (-50%) - For rapidly changing workloads
- **Gradual**: Same increase (+1), gentler decrease (-25%) - For stable workloads

**Key Features:**
- **Self-tuning**: Automatically finds optimal concurrency for current load
- **Latency-aware**: Reduces workers when operations are too slow
- **Error-aware**: Scales down when success rate drops below threshold
- **Bounded**: Always stays within configured min/max limits
- **Observable**: Callbacks for monitoring concurrency changes
- **Works with all operators**: SelectParallel*, ForEachParallel*, BatchParallel*

**Use Cases:**
- Variable load scenarios where optimal concurrency changes over time
- Auto-scaling to match downstream service capacity
- Preventing overload when downstream services slow down
- Maximizing throughput without manual tuning
- Handling unpredictable workload patterns

---

## 📦 Package Guides

### Rivulet.Diagnostics - Enterprise Observability

`Rivulet.Diagnostics` adds EventListener wrappers (console, file, structured JSON), time-window metric aggregation, Prometheus export, and ASP.NET Core health check integration via a fluent `DiagnosticsBuilder` API.

See the [Rivulet.Diagnostics README](src/Rivulet.Diagnostics/README.md) for complete documentation.

### Rivulet.Diagnostics.OpenTelemetry - Distributed Tracing & Metrics

`Rivulet.Diagnostics.OpenTelemetry` bridges Rivulet's EventCounters to OpenTelemetry Meters and creates activity spans for each parallel operation, supporting Jaeger, Zipkin, Azure Monitor, DataDog, and any OTLP exporter.

See the [Rivulet.Diagnostics.OpenTelemetry README](src/Rivulet.Diagnostics.OpenTelemetry/README.md) for complete documentation.

---

## ⚡ Performance Benchmarks

Rivulet.Core includes comprehensive benchmarks using BenchmarkDotNet to measure performance across .NET 8.0 and .NET 9.0. The benchmarks help validate performance characteristics, identify regressions, and guide optimization efforts.

### Running Benchmarks

```powershell
# Run all benchmarks
cd tests\Rivulet.Benchmarks
dotnet run -c Release

# Run specific benchmark suite
dotnet run -c Release -- --filter "*CoreOperatorsBenchmarks*"

# Quick run with fewer iterations
dotnet run -c Release -- --job short

# Export results to multiple formats
dotnet run -c Release -- --exporters json,html,markdown
```

### Benchmark Suites

#### 1. CoreOperatorsBenchmarks
Measures performance of core parallel operators:
- `SelectParallelAsync` (CPU-bound and I/O-bound workloads)
- `SelectParallelStreamAsync` (streaming results)
- `ForEachParallelAsync` (side effects)
- Comparison with sequential processing and unbounded `Task.WhenAll`

**Configuration**: 1,000 items with various MaxDegreeOfParallelism settings

#### 2. BatchingBenchmarks
Evaluates batch processing performance with different batch sizes (100, 500, 1000):
- `BatchParallelAsync` performance characteristics
- `BatchParallelStreamAsync` streaming behavior
- Optimal batch sizing analysis

**Configuration**: 10,000 items, MaxDegreeOfParallelism = 4

#### 3. ErrorHandlingBenchmarks
Quantifies the overhead of error handling and retry mechanisms:
- Retry policy overhead with transient failures (10% failure rate)
- Different error modes (FailFast, BestEffort, CollectAndContinue)
- Backoff strategy performance (Exponential, ExponentialJitter)

**Configuration**: 500 items with simulated failures

#### 4. AdvancedFeaturesBenchmarks
Measures the performance cost of production-grade features:
- Circuit breaker overhead
- Rate limiting (token bucket) overhead
- Adaptive concurrency overhead
- Progress tracking overhead
- Metrics tracking overhead
- Combined feature overhead

**Configuration**: 500 items to isolate feature-specific costs

#### 5. ConcurrencyScalingBenchmarks
Analyzes how performance scales with different MaxDegreeOfParallelism values (1, 2, 4, 8, 16, 32, 64, 128) to help identify optimal concurrency levels for various workload types.

**Configuration**: 1,000 items with 1ms I/O simulation per item

### Typical Performance Characteristics

Based on benchmark runs on modern hardware:

- **I/O-Bound Operations**: ~31x faster than sequential with `MaxDegreeOfParallelism = 32`; scales linearly up to 128
- **Memory Efficiency**: Minimal allocation growth (only ~15%) across concurrency 1–128
- **Advanced Features Overhead**: <1% for circuit breaker, rate limiting, progress, and metrics; adaptive concurrency adds ~3.5x due to continuous sampling
- **Optimal Parallelism**: Typically 16-64 for I/O-bound, 2-8 for CPU-bound (varies by hardware)
- **.NET 8.0 vs .NET 9.0**: Virtually identical for I/O-bound; .NET 9.0 shows ~22% faster success-path for CPU-light operations

### Example Results

```
BenchmarkDotNet v0.14.0, Windows 11
AMD Ryzen 9 9950X, 32 logical / 16 physical cores, 64 GB DDR5

|                 Method |  Runtime |      Mean | Allocated |
|----------------------- |--------- |---------: |----------:|
|    SelectParallelAsync | .NET 8.0 |   499 ms  |   563 KB  |
|    SelectParallelAsync | .NET 9.0 |   498 ms  |   565 KB  |
| Sequential Processing  | .NET 8.0 | 15,617 ms |   173 KB  |
|         Task.WhenAll   | .NET 8.0 |    16 ms  |   286 KB  | (Unbounded!)

// 1000 items, 1ms I/O delay each, MaxDegreeOfParallelism = 32
// SelectParallelAsync achieves ~31x speedup with controlled memory usage
```

**Key Insights**:
- Rivulet provides near-optimal performance while maintaining bounded concurrency
- Memory usage grows only ~15% from concurrency 1 to 128
- .NET 8.0 and .NET 9.0 show virtually identical performance for I/O-bound workloads
- Advanced features add minimal overhead when not actively engaged

### Interpreting Benchmark Results

- **Mean**: Average execution time across iterations
- **Allocated**: Total memory allocated per operation (lower is better)
- **Gen0/Gen1/Gen2**: Garbage collection counts
- **Baseline**: Reference implementation for comparison (usually marked with `*`)

See [tests/Rivulet.Benchmarks/README.md](tests/Rivulet.Benchmarks/README.md) for detailed documentation and full benchmark results.

---

## 🗺️ Roadmap

See the full [Roadmap](ROADMAP.md) for detailed plans.

### v1.3.0 - ✅ Released
- **Rivulet.Http** ✅ - Parallel HTTP operations with HttpClientFactory integration
- **Rivulet.IO** ✅ - Parallel file operations, directory processing
- **Rivulet.Sql** ✅ - Provider-agnostic parallel SQL operations
- **Rivulet.Sql.SqlServer** ✅ - SqlBulkCopy integration (10-100x faster bulk inserts)
- **Rivulet.Sql.PostgreSql** ✅ - COPY command integration (10-100x faster)
- **Rivulet.Sql.MySql** ✅ - LOAD DATA INFILE with MySqlBulkLoader (10-100x faster)
- **Rivulet.Polly** ✅ - Polly v8 integration, hedging, result-based retry
- **Rivulet.Csv** ✅ - Parallel CSV parsing and writing with CsvHelper integration
- **Rivulet.Pipeline** ✅ - Multi-stage pipeline composition with fluent API

### v1.4.0 (Q1-Q2 2026) - JSON + Cloud Storage
- **Rivulet.Json** 🆕 - Parallel JSON processing, deserialization, JsonPath queries
- **Rivulet.Azure.Storage** - Blob Storage parallel operations
- **Rivulet.Aws.S3** - S3 parallel operations

### v1.5.0 (Q2-Q3 2026) - ORM
- **Rivulet.EntityFramework** - EF Core parallel queries, multi-tenant support

---

## 🛠️ Development

The repository includes PowerShell scripts to streamline development and release workflows.

### Build Script

Build, restore, and test the solution locally.

```powershell
# Debug build with tests (default)
.\Build.ps1

# Release build with tests
.\Build.ps1 -Configuration Release

# Skip tests
.\Build.ps1 -SkipTests
```

### Package Script

Build and inspect NuGet packages locally before releasing.

```powershell
# Build all packages with test version
.\NugetPackage.ps1

# Build specific package
.\NugetPackage.ps1 -Project Core
.\NugetPackage.ps1 -Project Diagnostics

# Build with specific version
.\NugetPackage.ps1 -Version "1.2.3" -Project All
```

Creates packages in `./test-packages` and extracts contents to `./test-extract` for verification.

### Commit Script

Generate high-quality commit messages using AI (Claude, Gemini, or OpenAI).

```powershell
# Quick setup - set API key for your preferred provider
$env:ANTHROPIC_API_KEY = "your-key"  # For Claude
$env:GOOGLE_API_KEY = "your-key"      # For Gemini
$env:OPENAI_API_KEY = "your-key"      # For OpenAI

# Auto-detect provider from environment
.\SmartCommit.ps1

# Or specify provider explicitly
.\SmartCommit.ps1 -Provider Claude
.\SmartCommit.ps1 -Provider Gemini
.\SmartCommit.ps1 -Provider OpenAI
```

**Advanced**: Create `.smartcommit.config.json` (see `.smartcommit.config.example.json`) to configure:
- Default provider
- API keys (alternative to environment variables)
- Model versions (claude-3-5-sonnet, gemini-2.0-flash, gpt-4o, etc.)

This script:
- Analyzes your staged changes using git diff
- Calls your chosen AI provider to generate a meaningful commit message
- Shows the suggested message and allows you to:
  - **[y]** Accept and commit
  - **[r]** Request revision with feedback (e.g., "make it shorter", "add more detail")
  - **[n]** Cancel
- Iteratively refines the message based on your feedback
- Commits changes when you accept

**Get API keys**:
- Claude: [console.anthropic.com](https://console.anthropic.com/)
- Gemini: [aistudio.google.com/apikey](https://aistudio.google.com/apikey)
- OpenAI: [platform.openai.com/api-keys](https://platform.openai.com/api-keys)

### Release Script

Create release branch, tag, and trigger automated publishing.

```powershell
# Create release for version 1.0.0 (creates branch release/1.0.x, tag v1.0.0)
.\Release.ps1 -Version "1.0.0"

# Create patch release 1.0.1 (uses existing branch release/1.0.x, tag v1.0.1)
.\Release.ps1 -Version "1.0.1"

# Create pre-release (creates branch release/2.0.x, tag v2.0.0-beta)
.\Release.ps1 -Version "2.0.0-beta"
```

**Branching Strategy**:
- Branches: `release/{major}.{minor}.x` (e.g., `release/1.0.x` for all 1.0.* versions)
- Tags: `v{full.version}` (e.g., `v1.0.0`, `v1.0.1`, `v1.0.2`)
- Master branch for active development
- Patch releases reuse the same release branch

This script:
- Creates/switches to `release/{major}.{minor}.x` branch
- Displays release information (commit details, author, version, repository)
- **Asks for confirmation (y/Y) before proceeding**
- Creates git tag `v{version}` and pushes to GitHub
- Triggers the release workflow that builds, tests, and publishes to NuGet.org

The confirmation step shows:
- Version and tag information
- Current commit hash, author, date, and message
- List of actions that will be performed
- Allows you to cancel before any changes are pushed

---

## 📚 Documentation

- [Contributing Guide](CONTRIBUTING.md)
- [Roadmap](ROADMAP.md) - Future plans
- [Security Policy](SECURITY.md) - Vulnerability reporting
- [Code of Conduct](CODE_OF_CONDUCT.md)

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE.txt) file for details.

---

## 🙏 Acknowledgments

Built with ❤️ using:
- .NET 8.0 and .NET 9.0
- System.Threading.Channels for backpressure
- xUnit for testing
- BenchmarkDotNet for performance validation
