# Rivulet Roadmap & Packages

## Current Status (v1.3.0)

### ✅ Implemented in Rivulet.Core v1.1.x
- EventCounters + Metrics
- Rate Limiting/Token Bucket
- Circuit Breaker Pattern
- Adaptive Concurrency

### ✅ Completed Packages (v1.2.0)
- **Rivulet.Diagnostics** - Production observability
- **Rivulet.Diagnostics.OpenTelemetry** - Industry standard distributed tracing
- **Rivulet.Testing** - Virtual time, chaos injection, test helpers
- **Rivulet.Hosting** - .NET Generic Host integration

### ✅ Completed Packages (v1.3.0)
- **Rivulet.Http** - Parallel HTTP operations, resilient downloads, HttpClientFactory integration
- **Rivulet.Sql** - Provider-agnostic parallel SQL operations, connection pooling awareness, bulk operations
- **Rivulet.Polly** - Integration with Polly resilience library, advanced patterns (hedging, result-based retry)

---

## Package Priority Matrix

```
         Impact
           ^
   Very    |  [Diagnostics]✅    [Http]✅
   High  5 |  [OTel]✅           [Pipeline v2.0]
           |  [Sql.SqlServer]
           |
   High  4 |  [Testing]✅   [Polly]✅  [EntityFramework]
           |  [Hosting]✅   [Sql]✅
           |
  Medium 3 |  [Azure]   [Batching]   [Persistence]
           |  [Aws]     [Caching]    [Quotas]
           |  [Sql.PostgreSql]  [Sql.MySql]
           |
   Low   2 |  [Kafka]  [Serialization]  [Prometheus]
           |  [RabbitMQ]  [SQS]
           |
         1 |  [Tracing]  [Dataflow]  [Generators]
           |
           +---------------------------------------->
              Low     Medium    Med-High    High
                    (1-2)      (3-4)      (5)
                         Difficulty
```

---

## Roadmap by Version

### v1.3.0 - Common Integrations ✅ (Q2-Q3 2025)
**Goal**: Make common scenarios turnkey

| Package | Description | Impact |
|---------|-------------|--------|
| **Rivulet.Http** ✅ | HttpClient operators, streaming, resilient downloads | 🟢 Very High |
| **Rivulet.Sql** ✅ | Provider-agnostic parallel SQL operations, connection pooling, batching | 🟢 High |
| **Rivulet.Polly** ✅ | Integration with Polly resilience library, advanced patterns (hedging, result-based retry) | 🟢 High |

**Why**: HTTP is 80% of I/O workloads. Database parallelization is critical for performance. Polly is the industry-standard resilience library - native integration provides best-of-both-worlds.

**Note**:
- Rivulet.Sql is provider-agnostic and works with SQL Server, PostgreSQL, MySQL, SQLite, Oracle, and any ADO.NET provider.
- Rivulet.Core has built-in retry/circuit breaker for parallel operations. Use Rivulet.Polly for advanced Polly features (hedging, result-based retry, etc.) or to use Polly policies with Rivulet.

---

### v1.4.0 - High-Performance Database (Q4 2025)
**Goal**: Provider-specific optimizations for massive performance gains

| Package | Description | Impact |
|---------|-------------|--------|
| **Rivulet.Sql.SqlServer** | SqlBulkCopy integration (10-100x faster bulk inserts), table-valued parameters, SQL Server-specific optimizations | 🟢 Very High |
| **Rivulet.Azure** | Blob Storage, Cosmos DB, Service Bus, Functions adapters | 🟡 Med-High |
| **Rivulet.Aws** | S3, DynamoDB, SQS, Lambda adapters | 🟡 Med-High |

**Why**: SqlBulkCopy provides 10-100x performance improvement over batched INSERTs. Users with high-throughput SQL Server workloads need this.

**Note**: Rivulet.Sql.SqlServer is optional - use base Rivulet.Sql for cross-database code. Use provider-specific packages for maximum performance.

---

### v1.5.0 - ORM & Advanced Features (Q1-Q2 2026)
**Goal**: Entity Framework integration and sophisticated data processing

| Package | Description | Impact |
|---------|-------------|--------|
| **Rivulet.EntityFramework** | Parallel queries with automatic DbContext lifecycle, multi-tenant scenarios, parallel migrations, EF Core-aware retry logic | 🟢 High |
| **Rivulet.Batching** | Adaptive batching, time-window + size-window hybrid | 🟡 Med-High |
| **Rivulet.Caching** | Async cache layers, de-dupe, dog-pile prevention | 🟡 Med-High |

**Why**: Entity Framework Core is extremely popular. Users need safe parallel context management for multi-tenant queries, report generation, and parallel migrations.

**Note**:
- **Focus**: Context lifecycle management, parallel queries, multi-tenant scenarios
- **Don't duplicate**: Use EFCore.BulkExtensions or Rivulet.Sql for bulk operations
- **Key scenarios**: Multi-tenant parallel queries, report generation, parallel database migrations

**Example**:
```csharp
// Parallel queries across tenant databases
var results = await tenantIds.QueryParallelAsync(
    contextFactory,
    (context, tenantId) => context.Users.Where(u => u.TenantId == tenantId),
    new EfOptions
    {
        QueryTracking = QueryTrackingBehavior.NoTracking,
        ParallelOptions = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 10 }
    });
```

---

### v1.6.0 - Multi-Database & Durability (Q3 2026)
**Goal**: PostgreSQL/MySQL optimizations and long-running pipelines

| Package | Description | Impact |
|---------|-------------|--------|
| **Rivulet.Sql.PostgreSql** | COPY command integration (very fast bulk operations), Npgsql-specific features, PostgreSQL-specific optimizations | 🟡 Medium |
| **Rivulet.Sql.MySql** | LOAD DATA INFILE integration, MySqlConnector optimizations, MySQL-specific features | 🟡 Medium |
| **Rivulet.Persistence** | Checkpointing, resume, idempotency tokens | 🟡 Medium |
| **Rivulet.Quotas** | Token bucket per tenant/key, dynamic throttles | 🟡 Medium |

**Why**: PostgreSQL and MySQL users need provider-specific bulk operation performance similar to SqlBulkCopy.

**Note**: These packages provide massive performance gains for PostgreSQL/MySQL users (similar to Rivulet.Sql.SqlServer). Optional - use base Rivulet.Sql for cross-database compatibility.

---

### v1.7.0 - Message Queues (Q4 2026)
**Goal**: Event-driven workloads

| Package | Description | Impact |
|---------|-------------|--------|
| **Rivulet.Kafka** | Backpressure-aware consumption, checkpointing | 🟡 Med-High |
| **Rivulet.RabbitMQ** | Channel pooling, ack/nack semantics | 🟡 Medium |
| **Rivulet.SQS** | Visibility timeout management, batch operations | 🟡 Medium |

---

### v1.8.0 - Performance (Q1 2027)
**Goal**: Optimize hot paths

| Package | Description | Impact |
|---------|-------------|--------|
| **Rivulet.Serialization** | High-performance serializers (JSON, protobuf, MessagePack) | 🟡 Medium |
| **Rivulet.Monitoring.Prometheus** | Prometheus metrics exporter | 🟡 Medium |

---

### v2.0.0 - Pipeline Composition (Q2 2027)
**Goal**: Multi-stage processing framework

**Core Enhancement**: Pipeline Composition API
- Fluent API for chaining operations
- Different concurrency per stage
- Backpressure management between stages
- Streaming and buffered modes

**Example**:
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

---

### v2.1.0 - Advanced Tooling (Q3 2027)
**Goal**: Enhanced developer experience

| Package | Description | Impact |
|---------|-------------|--------|
| **Rivulet.Generators** | Source generators for compile-time optimizations | 🟡 Medium |
| **Rivulet.Dataflow** | TPL Dataflow interoperability, migration helpers | 🟡 Medium |
| **Rivulet.Tracing** | Lightweight tracing without full OTel dependency | 🟡 Medium |

---

## Quick Decision Tree

**I need to...**

- **📊 Monitor production pipelines** → `Rivulet.Diagnostics`, `Rivulet.Diagnostics.OpenTelemetry`
- **🌐 Call HTTP APIs in parallel** → `Rivulet.Http`
- **☁️ Process cloud storage files** → `Rivulet.Azure` or `Rivulet.Aws`
- **🗃️ Run parallel database operations** → Start with `Rivulet.Sql` (works with any database)
  - **High-performance SQL Server bulk operations** → `Rivulet.Sql.SqlServer` (10-100x faster)
  - **High-performance PostgreSQL bulk operations** → `Rivulet.Sql.PostgreSql` (v1.6.0+)
  - **High-performance MySQL bulk operations** → `Rivulet.Sql.MySql` (v1.6.0+)
- **🏛️ Use Entity Framework Core** → `Rivulet.EntityFramework` (v1.5.0+)
  - Parallel queries across tenant databases
  - Multi-tenant scenarios with automatic DbContext lifecycle
  - Parallel database migrations
- **📨 Process message queue events** → `Rivulet.Kafka`, `Rivulet.RabbitMQ`, `Rivulet.SQS`
- **⚡ Optimize performance-critical code** → `Rivulet.Serialization`, `Rivulet.Caching`
- **🏢 Deploy as hosted service** → `Rivulet.Hosting`
- **🧪 Test my pipeline code** → `Rivulet.Testing`
- **🔄 Build multi-stage pipeline** → Wait for v2.0.0 Pipeline Composition API

---

## Common Scenarios

### Production Web API
```
Rivulet.Core + Rivulet.Http + Rivulet.Diagnostics.OpenTelemetry
+ Rivulet.Hosting
```

### Cloud ETL Pipeline
```
Rivulet.Core + Rivulet.Azure + Rivulet.Sql + Rivulet.Sql.SqlServer
+ Rivulet.Diagnostics + Rivulet.Batching
```

### High-Throughput Data Processing
```
Rivulet.Core + Rivulet.Sql.SqlServer (or .PostgreSql/.MySql)
+ Rivulet.Diagnostics + Rivulet.Hosting
```
*Use provider-specific packages for 10-100x bulk operation performance*

### Multi-Tenant SaaS with EF Core
```
Rivulet.Core + Rivulet.EntityFramework + Rivulet.Http
+ Rivulet.Quotas + Rivulet.Diagnostics.OpenTelemetry
```
*Parallel queries across tenant databases with automatic context management*

### Event-Driven System
```
Rivulet.Core + Rivulet.Kafka + Rivulet.Persistence
+ Rivulet.Diagnostics.OpenTelemetry + Rivulet.Hosting
```

### Cross-Database Application
```
Rivulet.Core + Rivulet.Sql (provider-agnostic)
+ Rivulet.Diagnostics
```
*Use base Rivulet.Sql for applications that need to support multiple database providers*

---

## Success Metrics

### v1.2.0 (Current)
- ✅ 5+ production deployments using Rivulet.Diagnostics
- ✅ 10+ projects using Rivulet.Hosting
- ✅ 95%+ test coverage across all packages

### v1.4.0 (Q1 2026)
- 🎯 1,000+ total ecosystem downloads
- 🎯 25+ production cloud workloads (Azure/AWS)
- 🎯 3+ blog posts from external users

### v2.0.0 (Q2 2027)
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

**Last Updated**: 2025-11-20
**Version**: 1.3.0
**Status**: v1.3.0 Released - Planning v1.4.0 (Q4 2025)
