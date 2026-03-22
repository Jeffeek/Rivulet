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
### Released

#### [Rivulet.Core](https://www.nuget.org/packages/Rivulet.Core)
[![NuGet](https://img.shields.io/nuget/v/Rivulet.Core.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Core) [![Downloads](https://img.shields.io/nuget/dt/Rivulet.Core.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Core)

Safe, async-first parallel operators with bounded concurrency, retries, and backpressure for I/O-heavy workloads. [**Docs**](src/Rivulet.Core/README.md)

**Key Features:**
- ✅ Bounded Concurrency - Control max parallel operations with backpressure
- ✅ Adaptive Concurrency - Auto-scale workers based on latency and success rate (AIMD algorithm)
- ✅ Retry Policies - Automatic retries with exponential backoff for transient errors
- ✅ Circuit Breaker - Prevent cascading failures with automatic service protection
- ✅ Rate Limiting - Token bucket algorithm for controlling operation rates
- ✅ Error Handling Modes - FailFast, CollectAndContinue, or BestEffort
- ✅ Streaming Support - Process results incrementally via `IAsyncEnumerable<T>`
- ✅ Ordered Output - Maintain input sequence order when needed
- ✅ Runtime Metrics - Built-in monitoring via EventCounters and custom callbacks
- ✅ Progress Reporting - Periodic snapshots with throughput, ETA, and percent-complete
- ✅ Cancellation - Full `CancellationToken` support throughout
- ✅ Lifecycle Hooks - OnStart, OnComplete, OnRetry, OnError, OnThrottle, OnDrain callbacks
- ✅ Fallback Values - Supply default results for failed items instead of throwing
- ✅ Per-Item Timeouts - Enforce timeouts for individual operations
- ✅ Works with both `IEnumerable<T>` and `IAsyncEnumerable<T>`

#### [Rivulet.Diagnostics](https://www.nuget.org/packages/Rivulet.Diagnostics)
[![NuGet](https://img.shields.io/nuget/v/Rivulet.Diagnostics.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Diagnostics) [![Downloads](https://img.shields.io/nuget/dt/Rivulet.Diagnostics.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Diagnostics)

Enterprise observability for Rivulet.Core with EventListener wrappers, metric aggregators, and health check integration. [**Docs**](src/Rivulet.Diagnostics/README.md)

**Key Features:**
- EventListener Wrappers: Console, File, and Structured JSON logging
- Metrics Aggregation: Time-window based metric aggregation with statistics
- Prometheus Export: Export metrics in Prometheus text format
- Health Check Integration: Microsoft.Extensions.Diagnostics.HealthChecks support
- Fluent Builder API: Easy configuration with DiagnosticsBuilder

#### [Rivulet.Diagnostics.OpenTelemetry](https://www.nuget.org/packages/Rivulet.Diagnostics.OpenTelemetry)
[![NuGet](https://img.shields.io/nuget/v/Rivulet.Diagnostics.OpenTelemetry.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Diagnostics.OpenTelemetry) [![Downloads](https://img.shields.io/nuget/dt/Rivulet.Diagnostics.OpenTelemetry.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Diagnostics.OpenTelemetry)

OpenTelemetry integration for Rivulet.Core providing distributed tracing, metrics export, and comprehensive observability. [**Docs**](src/Rivulet.Diagnostics.OpenTelemetry/README.md)

**Key Features:**
- Distributed Tracing: Automatic activity creation with parent-child relationships
- Metrics Export: Bridge EventCounters to OpenTelemetry Meters
- Retry Tracking: Record retry attempts as activity events
- Circuit Breaker Events: Track circuit state changes in traces
- Adaptive Concurrency: Monitor concurrency adjustments
- Error Correlation: Link errors with retry attempts and transient classification

#### [Rivulet.Hosting](https://www.nuget.org/packages/Rivulet.Hosting)
[![NuGet](https://img.shields.io/nuget/v/Rivulet.Hosting.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Hosting) [![Downloads](https://img.shields.io/nuget/dt/Rivulet.Hosting.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Hosting)

Integration package for using Rivulet with Microsoft.Extensions.Hosting, ASP.NET Core, and the .NET Generic Host. [**Docs**](src/Rivulet.Hosting/README.md)

**Key Features:**
- Dependency Injection integration
- Configuration binding for `ParallelOptionsRivulet`
- Base classes for parallel background services
- Health checks for monitoring parallel operations
- Support for ASP.NET Core and Worker Services

#### [Rivulet.Testing](https://www.nuget.org/packages/Rivulet.Testing)
[![NuGet](https://img.shields.io/nuget/v/Rivulet.Testing.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Testing) [![Downloads](https://img.shields.io/nuget/dt/Rivulet.Testing.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Testing)

Testing utilities for Rivulet parallel operations including deterministic schedulers, virtual time, fake channels, and chaos injection. [**Docs**](src/Rivulet.Testing/README.md)

**Key Features:**
- VirtualTimeProvider: Control time in tests without actual delays
- FakeChannel: Testable channel implementation with operation tracking
- ChaosInjector: Inject failures and delays for resilience testing
- ConcurrencyAsserter: Assert and verify concurrency behavior

#### [Rivulet.Http](https://www.nuget.org/packages/Rivulet.Http)
[![NuGet](https://img.shields.io/nuget/v/Rivulet.Http.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Http) [![Downloads](https://img.shields.io/nuget/dt/Rivulet.Http.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Http)

Parallel HTTP operations with automatic retries, resilient downloads, and HttpClientFactory integration. [**Docs**](src/Rivulet.Http/README.md)

**Key Features:**
- HttpClientFactory integration
- Connection pooling awareness
- Transient error handling (timeouts, 5xx responses)
- Bounded concurrency to avoid overwhelming servers
- Progress reporting for downloads

#### [Rivulet.IO](https://www.nuget.org/packages/Rivulet.IO)
[![NuGet](https://img.shields.io/nuget/v/Rivulet.IO.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.IO) [![Downloads](https://img.shields.io/nuget/dt/Rivulet.IO.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.IO)

Parallel file and directory operations with bounded concurrency, resilience, and streaming support for efficient I/O processing. [**Docs**](src/Rivulet.IO/README.md)

**Key Features:**
- Safe concurrent file access
- Directory tree processing
- File pattern matching (glob patterns)
- Progress reporting
- Atomic write operations

#### [Rivulet.Sql](https://www.nuget.org/packages/Rivulet.Sql)
[![NuGet](https://img.shields.io/nuget/v/Rivulet.Sql.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Sql) [![Downloads](https://img.shields.io/nuget/dt/Rivulet.Sql.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Sql)

Safe parallel SQL operations with connection pooling awareness and bulk operations. [**Docs**](src/Rivulet.Sql/README.md)

**Key Features:**
- Works with any ADO.NET provider
- Connection pooling awareness
- Transaction support
- Parameterized queries
- Respects database connection pool limits

#### [Rivulet.Sql.SqlServer](https://www.nuget.org/packages/Rivulet.Sql.SqlServer)
[![NuGet](https://img.shields.io/nuget/v/Rivulet.Sql.SqlServer.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Sql.SqlServer) [![Downloads](https://img.shields.io/nuget/dt/Rivulet.Sql.SqlServer.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Sql.SqlServer)

SQL Server-specific optimizations for Rivulet.Sql including SqlBulkCopy integration for 10-100x faster bulk inserts. [**Docs**](src/Rivulet.Sql.SqlServer/README.md)

**Key Features:**
- SqlBulkCopy Integration: Ultra-high performance bulk inserts (50,000+ rows/sec)
- Parallel Bulk Operations: Process multiple batches in parallel
- Automatic Column Mapping: Maps DataTable columns to SQL Server table columns
- Custom Column Mappings: Support for explicit source-to-destination column mappings
- DataReader Support: Bulk insert from IDataReader sources
- Configurable Batching: Control batch size and timeout settings

#### [Rivulet.Sql.PostgreSql](https://www.nuget.org/packages/Rivulet.Sql.PostgreSql)
[![NuGet](https://img.shields.io/nuget/v/Rivulet.Sql.PostgreSql.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Sql.PostgreSql) [![Downloads](https://img.shields.io/nuget/dt/Rivulet.Sql.PostgreSql.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Sql.PostgreSql)

PostgreSQL-specific optimizations for Rivulet.Sql including COPY command integration for 10-100x faster bulk inserts. [**Docs**](src/Rivulet.Sql.PostgreSql/README.md)

**Key Features:**
- COPY Command Integration: Ultra-high performance bulk inserts using COPY
- Multiple Formats: Binary, CSV, and text formats supported
- Parallel Operations: Process multiple batches in parallel
- Streaming Import: Efficient memory usage with streaming
- Custom Delimiters: Support for CSV with custom delimiters
- Header Support: Handle CSV files with headers

#### [Rivulet.Sql.MySql](https://www.nuget.org/packages/Rivulet.Sql.MySql)
[![NuGet](https://img.shields.io/nuget/v/Rivulet.Sql.MySql.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Sql.MySql) [![Downloads](https://img.shields.io/nuget/dt/Rivulet.Sql.MySql.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Sql.MySql)

MySQL-specific optimizations for Rivulet.Sql including MySqlBulkCopy and MySqlBulkLoader (LOAD DATA INFILE) integration for 10-100x faster bulk inserts. [**Docs**](src/Rivulet.Sql.MySql/README.md)

**Key Features:**
- MySqlBulkCopy: High-performance bulk inserts for in-memory data
- MySqlBulkLoader: LOAD DATA LOCAL INFILE for maximum performance with CSV data
- File-based Loading: Direct file import support
- Parallel Operations: Process multiple batches in parallel
- Custom Delimiters: Support for any field separator
- Automatic Column Mapping: Maps columns automatically

#### [Rivulet.Polly](https://www.nuget.org/packages/Rivulet.Polly)
[![NuGet](https://img.shields.io/nuget/v/Rivulet.Polly.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Polly) [![Downloads](https://img.shields.io/nuget/dt/Rivulet.Polly.svg?style=flat-square)](https://www.nuget.org/packages/Rivulet.Polly)

Integration between Rivulet parallel processing and [Polly](https://github.com/App-vNext/Polly) resilience policies. [**Docs**](src/Rivulet.Polly/README.md)

**Key Features:**
- Use Polly policies with Rivulet - Apply any Polly policy to parallel operations
- Convert Rivulet to Polly - Use Rivulet configuration as standalone Polly policies
- Advanced resilience patterns - Hedging, result-based retry, and more
- Battle-tested - Built on Polly's production-proven resilience library

### In Development

#### Rivulet.Pipeline

Multi-stage pipeline composition for Rivulet with fluent API, per-stage concurrency, backpressure management between stages, and streaming support. [**Docs**](src/Rivulet.Pipeline/README.md)

**Key Features:**
- Fluent Builder API - Type-safe pipeline construction with IntelliSense support
- Per-Stage Concurrency - Different parallelism levels for each processing stage
- Backpressure Management - Automatic flow control between stages using channels
- Streaming & Buffered Modes - Memory-efficient streaming or materialized results
- Full Rivulet.Core Integration - Retries, circuit breakers, rate limiting, metrics

#### Rivulet.Csv

Parallel CSV parsing and writing with CsvHelper integration, bounded concurrency, and batching support for high-throughput data processing. [**Docs**](src/Rivulet.Csv/README.md)

**Key Features:**
- CsvHelper integration for robust CSV parsing
- Multi-type operations (2-5 generic type parameters)
- Memory-efficient streaming with IAsyncEnumerable
- Per-file CSV configuration (delimiters, culture, class maps)
- Progress tracking and lifecycle callbacks
- Error handling modes (FailFast, CollectAndContinue, BestEffort)
- Circuit breaker and retry support
- Ordered and unordered output options
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

## 📚 Documentation

- [Full Documentation](https://rivulet2.readthedocs.io/)
- [Contributing Guide](CONTRIBUTING.md)
- [Roadmap](ROADMAP.md)
- [Security Policy](SECURITY.md)
- [Code of Conduct](CODE_OF_CONDUCT.md)
- [Benchmarks](tests/Rivulet.Benchmarks/README.md)

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
