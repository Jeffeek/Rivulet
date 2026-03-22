# Rivulet Samples

This directory contains complete working examples demonstrating how to use all Rivulet packages in real-world scenarios.

## Available Samples

### 1. Rivulet.Core.Sample
**Package:** `Rivulet.Core`

Safe, async-first parallel operators with bounded concurrency, retries, and backpressure for I/O-heavy workloads.

- **SelectParallelAsync** - Process items and collect results
- **SelectParallelStreamAsync** - Stream results as they complete
- **ForEachParallelAsync** - Fire-and-forget parallel processing
- **BatchParallelAsync** - Process items in configurable batches

**Run:**
```bash
cd Rivulet.Core.Sample
dotnet run
```

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

---

### 2. Rivulet.Diagnostics.Sample
**Package:** `Rivulet.Diagnostics`

Enterprise observability for Rivulet.Core with EventListener wrappers, metric aggregators, and health check integration.

- **RivuletEventSource** - EventSource-based metrics
- **RivuletConsoleListener** - Real-time console metrics
- **RivuletFileListener** - File-based logging with rotation
- **RivuletStructuredLogListener** - JSON structured logging
- **PrometheusExporter** - Prometheus text format export
- **RivuletHealthCheck** - Health check integration
- **MetricsAggregator** - Statistical analysis

**Run:**
```bash
cd Rivulet.Diagnostics.Sample
dotnet run
```

**Key Features:**
- EventListener Wrappers: Console, File, and Structured JSON logging
- Metrics Aggregation: Time-window based metric aggregation with statistics
- Prometheus Export: Export metrics in Prometheus text format
- Health Check Integration: Microsoft.Extensions.Diagnostics.HealthChecks support
- Fluent Builder API: Easy configuration with DiagnosticsBuilder

---

### 3. Rivulet.Diagnostics.OpenTelemetry.Sample
**Package:** `Rivulet.Diagnostics.OpenTelemetry`

OpenTelemetry integration for Rivulet.Core providing distributed tracing, metrics export, and comprehensive observability.

- **Activity/Span creation** - Automatic distributed tracing
- **Retry tracking** - Record retry attempts with context
- **Error recording** - Detailed error tracking with transient classification
- **Custom attributes** - Attach business context to spans
- **Circuit breaker events** - Track state changes
- **Adaptive concurrency** - Monitor concurrency adjustments

**Run:**
```bash
cd Rivulet.Diagnostics.OpenTelemetry.Sample
dotnet run
```

**Key Features:**
- Distributed Tracing: Automatic activity creation with parent-child relationships
- Metrics Export: Bridge EventCounters to OpenTelemetry Meters
- Retry Tracking: Record retry attempts as activity events
- Circuit Breaker Events: Track circuit state changes in traces
- Adaptive Concurrency: Monitor concurrency adjustments
- Error Correlation: Link errors with retry attempts and transient classification

---

### 4. Rivulet.Hosting.Sample
**Package:** `Rivulet.Hosting`

Integration package for using Rivulet with Microsoft.Extensions.Hosting, ASP.NET Core, and the .NET Generic Host.

- **ParallelWorkerService** - Background worker base class
- **ParallelBackgroundService** - Queue processor base class
- **AddRivulet()** - DI integration
- **RivuletOptions** - Configuration binding

**Run:**
```bash
cd Rivulet.Hosting.Sample
dotnet run
```

**Key Features:**
- Dependency Injection integration
- Configuration binding for `ParallelOptionsRivulet`
- Base classes for parallel background services
- Health checks for monitoring parallel operations
- Support for ASP.NET Core and Worker Services

---

### 5. Rivulet.Testing.Sample
**Package:** `Rivulet.Testing`

Testing utilities for Rivulet parallel operations including deterministic schedulers, virtual time, fake channels, and chaos injection.

- **VirtualTimeProvider** - Control time for deterministic tests
- **ChaosInjector** - Inject failures and latency
- **ConcurrencyAsserter** - Verify concurrency limits
- **FakeChannel** - Deterministic channel behavior
- **DeterministicScheduler** - Predictable task execution

**Run:**
```bash
cd Rivulet.Testing.Sample
dotnet run
```

**Key Features:**
- VirtualTimeProvider: Control time in tests without actual delays
- FakeChannel: Testable channel implementation with operation tracking
- ChaosInjector: Inject failures and delays for resilience testing
- ConcurrencyAsserter: Assert and verify concurrency behavior

---

### 6. Rivulet.Http.Sample
**Package:** `Rivulet.Http`

Parallel HTTP operations with automatic retries, resilient downloads, and HttpClientFactory integration.

- **GetParallelAsync** - Fetch multiple URLs concurrently
- **PostParallelAsync** - Submit data in parallel
- **DownloadParallelAsync** - Download files in parallel

**Run:**
```bash
cd Rivulet.Http.Sample
dotnet run
```

**Key Features:**
- HttpClientFactory integration
- Connection pooling awareness
- Transient error handling (timeouts, 5xx responses)
- Bounded concurrency to avoid overwhelming servers
- Progress reporting for downloads

---

### 7. Rivulet.IO.Sample
**Package:** `Rivulet.IO`

Parallel file and directory operations with bounded concurrency, resilience, and streaming support for efficient I/O processing.

- **ReadAllTextParallelAsync** - Read multiple files
- **WriteAllTextParallelAsync** - Write multiple files
- **ProcessFilesParallelAsync** - Process files in directory
- **TransformFilesParallelAsync** - Transform files

**Run:**
```bash
cd Rivulet.IO.Sample
dotnet run
```

**Key Features:**
- Safe concurrent file access
- Directory tree processing
- File pattern matching (glob patterns)
- Progress reporting
- Atomic write operations

---

### 8. Rivulet.Sql.Sample
**Package:** `Rivulet.Sql`

Safe parallel SQL operations with connection pooling awareness and bulk operations.

- **ExecuteQueriesParallelAsync** - Execute multiple queries
- **ExecuteCommandsParallelAsync** - Execute multiple commands
- **BulkInsertAsync** - Provider-agnostic bulk insert

**Run:**
```bash
cd Rivulet.Sql.Sample
dotnet run
```

**Key Features:**
- Works with any ADO.NET provider
- Connection pooling awareness
- Transaction support
- Parameterized queries
- Respects database connection pool limits

---

### 9. Rivulet.Sql.SqlServer.Sample
**Package:** `Rivulet.Sql.SqlServer`

SQL Server-specific optimizations for Rivulet.Sql including SqlBulkCopy integration for 10-100x faster bulk inserts.

- **BulkInsertUsingSqlBulkCopyAsync** - Optimized bulk insert
- **BulkInsertUsingSqlBulkCopyAsync (3 overloads)**

**Run:**
```bash
cd Rivulet.Sql.SqlServer.Sample
dotnet run
```

**Key Features:**
- SqlBulkCopy Integration: Ultra-high performance bulk inserts (50,000+ rows/sec)
- Parallel Bulk Operations: Process multiple batches in parallel
- Automatic Column Mapping: Maps DataTable columns to SQL Server table columns
- Custom Column Mappings: Support for explicit source-to-destination column mappings
- DataReader Support: Bulk insert from IDataReader sources
- Configurable Batching: Control batch size and timeout settings

---

### 10. Rivulet.Sql.PostgreSql.Sample
**Package:** `Rivulet.Sql.PostgreSql`

PostgreSQL-specific optimizations for Rivulet.Sql including COPY command integration for 10-100x faster bulk inserts.

- **BulkInsertUsingCopyAsync** - COPY command integration

**Run:**
```bash
cd Rivulet.Sql.PostgreSql.Sample
dotnet run
```

**Key Features:**
- COPY Command Integration: Ultra-high performance bulk inserts using COPY
- Multiple Formats: Binary, CSV, and text formats supported
- Parallel Operations: Process multiple batches in parallel
- Streaming Import: Efficient memory usage with streaming
- Custom Delimiters: Support for CSV with custom delimiters
- Header Support: Handle CSV files with headers

---

### 11. Rivulet.Sql.MySql.Sample
**Package:** `Rivulet.Sql.MySql`

MySQL-specific optimizations for Rivulet.Sql including MySqlBulkCopy and MySqlBulkLoader (LOAD DATA INFILE) integration for 10-100x faster bulk inserts.

- **BulkInsertUsingMySqlBulkLoaderAsync** - MySqlBulkLoader integration

**Run:**
```bash
cd Rivulet.Sql.MySql.Sample
dotnet run
```

**Key Features:**
- MySqlBulkCopy: High-performance bulk inserts for in-memory data
- MySqlBulkLoader: LOAD DATA LOCAL INFILE for maximum performance with CSV data
- File-based Loading: Direct file import support
- Parallel Operations: Process multiple batches in parallel
- Custom Delimiters: Support for any field separator
- Automatic Column Mapping: Maps columns automatically

---

### 12. Rivulet.Polly.Sample
**Package:** `Rivulet.Polly`

Integration between Rivulet parallel processing and [Polly](https://github.com/App-vNext/Polly) resilience policies.

- **SelectParallelWithPolicyAsync** - Apply Polly policies to parallel operations
- **SelectParallelWithHedgingAsync** - Hedging pattern (parallel redundant requests)
- **ToPollyRetryPipeline** - Convert Rivulet retry to Polly pipeline

**Run:**
```bash
cd Rivulet.Polly.Sample
dotnet run
```

**Key Features:**
- Use Polly policies with Rivulet - Apply any Polly policy to parallel operations
- Convert Rivulet to Polly - Use Rivulet configuration as standalone Polly policies
- Advanced resilience patterns - Hedging, result-based retry, and more
- Battle-tested - Built on Polly's production-proven resilience library

---

### 13. Rivulet.Pipeline.Sample
**Package:** `Rivulet.Pipeline`

Multi-stage pipeline composition for Rivulet with fluent API, per-stage concurrency, backpressure management between stages, and streaming support.

- **PipelineBuilder.Create** - Create type-safe pipeline builders with fluent API
- **SelectParallel** - Parallel transform stage with async/sync selectors
- **WhereParallel** - Parallel filter stage with async/sync predicates
- **Batch** - Group items into fixed-size batches with optional timeout
- **BatchSelectParallel** - Batch and transform items in parallel
- **SelectManyParallel** - Flatten/expand collections in parallel
- **Tap** - Execute side effects without transforming items
- **Buffer** - Decouple upstream/downstream with channel-based buffering
- **Throttle** - Rate limit items using token bucket algorithm
- **ExecuteAsync** - Execute pipeline and collect all results
- **ExecuteStreamAsync** - Stream results as IAsyncEnumerable

**Run:**
```bash
cd Rivulet.Pipeline.Sample
dotnet run
```

**Key Features:**
- Fluent Builder API - Type-safe pipeline construction with IntelliSense support
- Per-Stage Concurrency - Different parallelism levels for each processing stage
- Backpressure Management - Automatic flow control between stages using channels
- Streaming & Buffered Modes - Memory-efficient streaming or materialized results
- Full Rivulet.Core Integration - Retries, circuit breakers, rate limiting, metrics

---

### 14. Rivulet.Csv.Sample
**Package:** `Rivulet.Csv`

Parallel CSV parsing and writing with CsvHelper integration, bounded concurrency, and batching support for high-throughput data processing.

- **ParseCsvParallelAsync** - Parse multiple CSV files in parallel and return all records as a flattened list
- **ParseCsvParallelGroupedAsync** - Parse multiple CSV file groups in parallel with per-file configuration, returning records grouped by file path
- **ParseCsvParallelGroupedAsync (Multi-Type)** - Parse 2-5 different record types concurrently with grouped results
- **StreamCsvParallelAsync** - Stream CSV records as IAsyncEnumerable with memory-efficient processing
- **WriteCsvParallelAsync** - Write collections of records to multiple CSV files in parallel with per-file configuration
- **WriteCsvParallelAsync (Multi-Type)** - Write 2-5 different record types to separate files concurrently
- **TransformCsvParallelAsync** - Transform CSV files in parallel applying sync or async transformation functions

**Run:**
```bash
cd Rivulet.Csv.Sample
dotnet run
```

**Key Features:**
- CsvHelper integration for robust CSV parsing
- Multi-type operations (2-5 generic type parameters)
- Memory-efficient streaming with IAsyncEnumerable
- Per-file CSV configuration (delimiters, culture, class maps)
- Progress tracking and lifecycle callbacks
- Error handling modes (FailFast, CollectAndContinue, BestEffort)
- Circuit breaker and retry support
- Ordered and unordered output options

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

## Learning Path

1. **Start with Rivulet.Core.Sample** to understand core operators
2. **Explore Rivulet.Diagnostics.Sample** for production observability
3. **Review Rivulet.Diagnostics.OpenTelemetry.Sample** for distributed tracing
4. **Study Rivulet.Testing.Sample** for testing strategies
5. **Examine Rivulet.Hosting.Sample** for enterprise integration

## Next Steps

- Read the [Documentation](https://rivulet2.readthedocs.io)
- Review [ROADMAP.md](../ROADMAP.md) for upcoming features
- Contribute on [GitHub](https://github.com/Jeffeek/Rivulet)

## Support

For questions or issues:
- Open an issue on GitHub
- Check existing documentation
- Review test projects for more examples
