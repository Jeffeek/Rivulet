# Rivulet Roadmap & Packages

## Current Status (v1.3.0 - Released)

### ✅ Implemented in Rivulet.Core v1.1.x
- EventCounters + Metrics
- Rate Limiting/Token Bucket
- Circuit Breaker Pattern
- Adaptive Concurrency

<!-- VERSIONS_START -->
### ✅ 1.0.0 - Released

- **Rivulet.Core** - Safe, async-first parallel operators with bounded concurrency, retries, and backpressure for I/O-heavy workloads.

### ✅ 1.1.0 - Released

- **Rivulet.Diagnostics** - Enterprise observability for Rivulet.Core with EventListener wrappers, metric aggregators, and health check integration.
- **Rivulet.Diagnostics.OpenTelemetry** - OpenTelemetry integration for Rivulet.Core providing distributed tracing, metrics export, and comprehensive observability.

### ✅ 1.2.0 - Released

- **Rivulet.Hosting** - Integration package for using Rivulet with Microsoft.Extensions.Hosting, ASP.NET Core, and the .NET Generic Host.
- **Rivulet.Testing** - Testing utilities for Rivulet parallel operations including deterministic schedulers, virtual time, fake channels, and chaos injection.

### ✅ 1.3.0 - Released

- **Rivulet.Http** - Parallel HTTP operations with automatic retries, resilient downloads, and HttpClientFactory integration.
- **Rivulet.IO** - Parallel file and directory operations with bounded concurrency, resilience, and streaming support for efficient I/O processing.
- **Rivulet.Sql** - Safe parallel SQL operations with connection pooling awareness and bulk operations.
- **Rivulet.Sql.SqlServer** - SQL Server-specific optimizations for Rivulet.Sql including SqlBulkCopy integration for 10-100x faster bulk inserts.
- **Rivulet.Sql.PostgreSql** - PostgreSQL-specific optimizations for Rivulet.Sql including COPY command integration for 10-100x faster bulk inserts.
- **Rivulet.Sql.MySql** - MySQL-specific optimizations for Rivulet.Sql using MySqlBulkLoader (LOAD DATA LOCAL INFILE) for 10-100x faster bulk inserts.
- **Rivulet.Polly** - Integration between Rivulet parallel processing and [Polly](https://github.com/App-vNext/Polly) resilience policies.

### 🚧 2.0.0 - In Development

- **Rivulet.Pipeline** - Multi-stage pipeline composition for Rivulet with fluent API, per-stage concurrency, backpressure management between stages, and streaming support.
- **Rivulet.Csv** - Parallel CSV parsing and writing with CsvHelper integration, bounded concurrency, and batching support for high-throughput data processing.
<!-- VERSIONS_END -->

---

## Package Priority Matrix

```
         Impact
           ^
   Very    |  [Diagnostics]✅    [Http]✅     [Sql.SqlServer]✅
   High  5 |  [Diagnostics.OpenTelemetry]✅           [EntityFramework]
           |  [Json]🆕  [IO]✅
           |
   High  4 |  [Testing]✅   [Polly]✅  [Csv]🆕
           |  [Hosting]✅   [Sql]✅    [Azure.Storage]
           |
  Medium 3 |  [Aws.S3]  [Sql.PostgreSql]✅  [Sql.MySql]✅
           |  [Pipeline v2.0]
           |
   Low   2 |  [Batching?]  [Generators?]
           |
         1 |
           |
           +---------------------------------------->
              Low     Medium    Med-High    High
                    (1-2)      (3-4)      (5)
                         Difficulty
```

---

## Roadmap by Version

### v2.0.0 - Pipeline Composition + CSV (In Development)

**Goal**: Multi-stage processing framework and CSV integration

| Package | Description | Impact | Status |
|---------|-------------|--------|--------|
| **Rivulet.Pipeline** | Multi-stage pipeline composition with fluent API, per-stage concurrency, backpressure management | 🟢 Very High | In Development |
| **Rivulet.Csv** | Parallel CSV parsing and writing with CsvHelper integration, bounded concurrency, batching | 🟢 High | In Development |

**Pipeline Example**:
```csharp
var pipeline = PipelineBuilder<string, ProcessedData>
    .StartWith(urls)
    .SelectParallel(FetchDataAsync, concurrency: 32)
    .ThenSelectParallel(TransformAsync, concurrency: 16)
    .ThenBatch(100)
    .ThenForEachParallel(SaveBatchAsync, concurrency: 4)
    .WithCircuitBreaker(threshold: 0.1)
    .WithRetries(3)
    .Build();

var results = await pipeline.ExecuteAsync(cancellationToken);
```

**CSV Example**:
```csharp
var records = await csvFiles
    .ParseCsvParallelAsync<Product>(new CsvOperationOptions { ... });
```

---

### Future Considerations (Post-v2.0)

#### Under Evaluation

| Package | Status | Reasoning |
|---------|--------|-----------|
| **Rivulet.Batching** | ❓ Deferred | Rivulet.Core already has BatchParallelAsync. Only create if adaptive batching/complex features justify separate package. **Wait for user demand.** |
| **Rivulet.Generators** | ❓ Deferred | Source generators are complex to maintain. Only worth it if perf analysis shows clear gains. **Defer until v2.0 is stable.** |

#### Explicitly Removed

| Package | Status | Reasoning |
|---------|--------|-----------|
| **Rivulet.Caching** | ❌ Removed | Overlaps with existing .NET caching (IMemoryCache, IDistributedCache). **Provide samples instead.** |
| **Rivulet.Persistence** | ❌ Removed | Checkpointing/resume is complex. Many existing solutions (Durable Task, Rebus, MassTransit). **Don't reinvent wheel.** |
| **Rivulet.Quotas** | ❌ Removed | Token bucket per tenant is already in Core (RateLimiter). **Might not need separate package.** |
| **Rivulet.Kafka** | ❌ Removed | Heavy dependency, significant maintenance burden. **Wait for v2.0 pipeline API + user demand.** |
| **Rivulet.RabbitMQ** | ❌ Removed | Heavy dependency, overlaps with MassTransit/NServiceBus. **Wait for v2.0 pipeline API + user demand.** |
| **Rivulet.SQS** | ❌ Removed | Heavy dependency, AWS SDK complexity. **Wait for v2.0 pipeline API + user demand.** |
| **Rivulet.Serialization** | ❌ Removed | System.Text.Json, MessagePack, protobuf-net already exist. **Users can use any serializer. Provide samples instead.** |
| **Rivulet.Monitoring.Prometheus** | ❌ Removed | **Already exists in Rivulet.Diagnostics!** PrometheusExporter is included. **Redundant package.** |
| **Rivulet.Dataflow** | ❌ Removed | TPL Dataflow is built into .NET. **Provide migration guide/samples instead of package.** |
| **Rivulet.Tracing** | ❌ Removed | Lightweight tracing exists (Activity API, System.Diagnostics). **Use existing .NET tracing APIs.** |
| **Full Azure/AWS packages** | ❌ Removed | Too broad. **Focus on storage (Blob/S3) first.** Defer other services (Cosmos, Functions, Lambda, DynamoDB) until storage proves demand. |

---

## Quick Decision Tree

**I need to...**

- **📊 Monitor production pipelines** → `Rivulet.Diagnostics`, `Rivulet.Diagnostics.OpenTelemetry`
- **🌐 Call HTTP APIs in parallel** → `Rivulet.Http`
- **📁 Process files in parallel** → `Rivulet.IO`
- **🗃️ Run parallel database operations** → Start with `Rivulet.Sql` (works with any database)
  - **High-performance SQL Server bulk operations** → `Rivulet.Sql.SqlServer` (10-100x faster)
  - **High-performance PostgreSQL bulk operations** → `Rivulet.Sql.PostgreSql` (10-100x faster)
  - **High-performance MySQL bulk operations** → `Rivulet.Sql.MySql` (10-100x faster)
- **📊 Process CSV files** → `Rivulet.Csv` (v2.0.0)
- **🏢 Deploy as hosted service** → `Rivulet.Hosting`
- **🧪 Test my pipeline code** → `Rivulet.Testing`
- **🔄 Build multi-stage pipeline** → `Rivulet.Pipeline` (v2.0.0)
- **🛡️ Add resilience policies** → `Rivulet.Polly`

---

## Common Scenarios

### Production Web API

```text
Rivulet.Core + Rivulet.Http + Rivulet.Diagnostics.OpenTelemetry + Rivulet.Hosting
```

### High-Throughput Data Processing

```text
Rivulet.Core + Rivulet.Sql.SqlServer (or .PostgreSql/.MySql) + Rivulet.IO
+ Rivulet.Diagnostics + Rivulet.Hosting
```

*Use provider-specific SQL packages for 10-100x bulk operation performance*

### Data Import/Export Pipeline

```text
Rivulet.Core + Rivulet.IO + Rivulet.Sql + Rivulet.Csv + Rivulet.Diagnostics
```

*Process CSV files and load into database in parallel*

### Cross-Database Application

```text
Rivulet.Core + Rivulet.Sql (provider-agnostic) + Rivulet.Diagnostics
```

*Use base Rivulet.Sql for applications that need to support multiple database providers*

### Multi-Stage Pipeline

```text
Rivulet.Core + Rivulet.Pipeline + Rivulet.Diagnostics
```

*Chain parallel stages with per-stage concurrency and backpressure*

---

## Success Metrics

### v1.3.0 (Released)
- 🚧 95%+ test coverage target across all packages
- 🚧 Comprehensive integration with HTTP, SQL, and Polly

### v2.0.0 (In Development)
- 🎯 10,000+ total ecosystem downloads
- 🎯 100+ production pipelines using v2.0 API
- 🎯 200+ GitHub stars, 10+ external contributors

---

## Architecture Principles

1. **Zero Dependencies in Core** - Only minimal dependencies, optional features in separate packages
2. **Granular Packages** - Heavy dependencies get separate packages
3. **Consistent API Patterns** - Extension methods, Options classes, async-first
4. **High Quality** - ≥95% test coverage, performance benchmarks
5. **Synchronized Versioning** - All packages align with Core version
6. **Don't Duplicate .NET** - Use existing .NET features when available

---

## Package Design Principles

### Provider-Agnostic Base Packages
- **Rivulet.Sql** - Works with any ADO.NET provider (SQL Server, PostgreSQL, MySQL, SQLite, Oracle, etc.)
- Pros: Simple, flexible, cross-database compatible
- When to use: Multi-database support, simpler scenarios, good default performance

### Provider-Specific Enhancement Packages
- **Rivulet.Sql.SqlServer**, **Rivulet.Sql.PostgreSql**, **Rivulet.Sql.MySql**
- Pros: 10-100x performance gains for bulk operations, provider-specific features
- When to use: High-throughput scenarios, single database provider, maximum performance needed
- Note: Optional - references base Rivulet.Sql package

### Example
```csharp
// Option 1: Provider-agnostic (works with any database)
await users.BulkInsertAsync(
    () => new SqlConnection(connectionString),
    BuildBatchedInserts,
    options);
// Performance: ~1,000 rows/sec

// Option 2: Provider-specific (SQL Server only)
await users.BulkInsertUsingSqlBulkCopyAsync(
    () => new SqlConnection(connectionString),
    options);
// Performance: ~50,000+ rows/sec (10-100x faster!)
```

---

## Lessons Learned

### What We Got Right ✅
1. **JSON, IO, CSV are critical** - These are more important than niche integrations
2. **Provider-specific SQL packages have proven value** - SqlBulkCopy is 10-100x faster
3. **Focus on fundamentals first** - HTTP, JSON, files before message queues
4. **Prometheus export in Diagnostics** - No need for separate package

### What We're Deferring 🚧
1. **Message queues** - Heavy dependencies, wait for v2.0 + user demand
2. **Caching, Persistence, Quotas** - Complex, overlaps with existing solutions
3. **Full cloud suites** - Too broad, focus on storage first

### What We're Not Doing ❌
1. **Serialization** - Already exists (System.Text.Json, MessagePack)
2. **Tracing** - Already exists (Activity API, System.Diagnostics)
3. **Dataflow** - Already exists (TPL Dataflow)
4. **Duplicate .NET features** - Provide samples/migration guides instead

---

**Last Updated**: 2026-03-23
**Version**: 1.3.0
**Status**: v1.3.0 Released — v2.0.0 In Development (Pipeline, Csv)
