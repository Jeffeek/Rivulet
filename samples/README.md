# Rivulet Samples

This directory contains complete working examples demonstrating how to use all Rivulet packages in real-world scenarios.

## Available Samples

### 1. Rivulet.ConsoleSample
**Package:** `Rivulet.Core`

Core parallel processing operators with bounded concurrency, retry policies, and error handling

- **SelectParallelAsync** - Process items and collect results
- **SelectParallelStreamAsync** - Stream results as they complete
- **ForEachParallelAsync** - Fire-and-forget parallel processing
- **BatchParallelAsync** - Process items in configurable batches

**Run:**
```bash
cd Rivulet.ConsoleSample
dotnet run
```

**Key Features:**
- Bounded concurrency control
- Retry policies with exponential backoff
- Circuit breaker pattern
- Error handling modes (StopOnFirstError, CollectAndContinue)
- Ordered and unordered output

---

### 2. Rivulet.Diagnostics.Sample
**Package:** `Rivulet.Diagnostics`

Production-ready observability with EventSource metrics, structured logging, and health checks

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
- EventSource-based metrics (ETW, EventPipe)
- Multiple export formats
- Health monitoring
- Throughput and error rate tracking
- Zero allocation in hot paths

---

### 3. Rivulet.OpenTelemetry.Sample
**Package:** `Rivulet.Diagnostics.OpenTelemetry`

OpenTelemetry integration for distributed tracing and W3C Trace Context propagation

- **Activity/Span creation** - Automatic distributed tracing
- **Retry tracking** - Record retry attempts with context
- **Error recording** - Detailed error tracking with transient classification
- **Custom attributes** - Attach business context to spans
- **Circuit breaker events** - Track state changes
- **Adaptive concurrency** - Monitor concurrency adjustments

**Run:**
```bash
cd Rivulet.OpenTelemetry.Sample
dotnet run
```

**Key Features:**
- W3C Trace Context propagation
- OpenTelemetry Metrics and Traces
- Correlation across distributed systems
- Integration with Jaeger/Zipkin/OTLP exporters

---

### 4. Rivulet.Hosting.Sample
**Package:** `Rivulet.Hosting`

ASP.NET Core integration with background services, dependency injection, and configuration binding

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
- Dependency injection integration
- Configuration binding (appsettings.json)
- Background services
- Health checks
- Graceful shutdown support

---

### 5. Rivulet.Testing.Sample
**Package:** `Rivulet.Testing`

Testing utilities for deterministic tests with time control, chaos injection, and concurrency verification

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
- Fast deterministic tests
- Fault injection testing
- Concurrency verification
- No actual delays needed
- Integration with xUnit/NUnit/MSTest

---

### 6. Rivulet.Http.Sample
**Package:** `Rivulet.Http`

Parallel HTTP operations with HttpClientFactory integration and connection pooling awareness

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

Parallel file operations with safe directory processing and file transformations

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

Provider-agnostic parallel SQL operations with connection pooling awareness

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

SQL Server optimizations with SqlBulkCopy integration (10-100x faster bulk inserts)

- **BulkInsertUsingSqlBulkCopyAsync** - Optimized bulk insert
- **BulkInsertUsingSqlBulkCopyAsync (3 overloads)**

**Run:**
```bash
cd Rivulet.Sql.SqlServer.Sample
dotnet run
```

**Key Features:**
- SqlBulkCopy integration (10-100x faster)
- Batch size optimization
- Table-valued parameters
- Progress reporting
- Automatic table creation

---

### 10. Rivulet.Sql.PostgreSql.Sample
**Package:** `Rivulet.Sql.PostgreSql`

PostgreSQL optimizations with COPY command integration (10-100x faster bulk operations)

- **BulkInsertUsingCopyAsync** - COPY command integration

**Run:**
```bash
cd Rivulet.Sql.PostgreSql.Sample
dotnet run
```

**Key Features:**
- COPY command integration (10-100x faster)
- Binary and text format support
- Progress reporting
- Automatic table creation

---

### 11. Rivulet.Sql.MySql.Sample
**Package:** `Rivulet.Sql.MySql`

MySQL optimizations with LOAD DATA INFILE integration using MySqlBulkLoader

- **BulkInsertUsingMySqlBulkLoaderAsync** - MySqlBulkLoader integration

**Run:**
```bash
cd Rivulet.Sql.MySql.Sample
dotnet run
```

**Key Features:**
- MySqlBulkLoader integration (10-100x faster)
- Local and remote file loading
- Progress reporting
- Automatic table creation

---

### 12. Rivulet.Polly.Sample
**Package:** `Rivulet.Polly`

Polly v8 integration with hedging, result-based retry, and resilience pipeline composition

- **SelectParallelWithPolicyAsync** - Apply Polly policies to parallel operations
- **SelectParallelWithHedgingAsync** - Hedging pattern (parallel redundant requests)
- **ToPollyRetryPipeline** - Convert Rivulet retry to Polly pipeline

**Run:**
```bash
cd Rivulet.Polly.Sample
dotnet run
```

**Key Features:**
- Polly v8 ResiliencePipeline integration
- Hedging pattern support
- Result-based retry policies
- Policy composition
- Fallback strategies

---

### 13. Rivulet.Csv.Sample
**Package:** `Rivulet.Csv`

Parallel CSV parsing and writing for Rivulet with CsvHelper integration, bounded concurrency, and batching support for high-throughput data processing

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

### 14. Rivulet.Pipeline.Sample
**Package:** `Rivulet.Pipeline`

Multi-stage pipeline composition for Rivulet with fluent API, per-stage concurrency, backpressure management between stages, and streaming support

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
- Fluent builder API with type-safe stage chaining
- Per-stage concurrency configuration via StageOptions
- Backpressure management using System.Threading.Channels
- Reuses Core components (TokenBucket, ParallelOptionsRivulet)
- Pipeline lifecycle callbacks (start, complete, stage events)
- Per-stage metrics tracking (items in/out, timing)
- Retry policies, circuit breaker, and error modes per stage
- Cancellation support propagated through all stages
- Streaming execution with IAsyncEnumerable

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

1. **Start with Rivulet.Console.Sample** to understand core operators
2. **Explore Rivulet.Diagnostics.Sample** for production observability
3. **Review Rivulet.OpenTelemetry.Sample** for distributed tracing
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
