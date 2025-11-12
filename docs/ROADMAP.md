# Rivulet Roadmap & Packages

## Current Status (v1.2.0)

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

---

## Package Priority Matrix

```
         Impact
           ^
   Very    |  [Diagnostics]✅    [Http]
   High  5 |  [OTel]✅           [Pipeline v2.0]
           |
   High  4 |  [Testing]✅   [RetryPolicies]  [Channels]
           |  [Hosting]✅   [Sql]
           |
  Medium 3 |  [Azure]   [Batching]   [Persistence]
           |  [Aws]     [Caching]    [Quotas]
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

### v1.3.0 - Common Integrations (Q1-Q2 2026)
**Goal**: Make common scenarios turnkey

| Package | Description | Impact |
|---------|-------------|--------|
| **Rivulet.Http** | HttpClient operators, streaming, resilient downloads | 🟢 Very High |
| **Rivulet.RetryPolicies** | Exponential backoff, jitter, hedging, policy composition | 🟢 High |
| **Rivulet.Sql** | Safe parallel DB operations, connection pooling, batching | 🟢 High |

**Why**: HTTP is 80% of I/O workloads. Production needs resilience and database parallelization.

---

### v1.4.0 - Cloud Native (Q3-Q4 2026)
**Goal**: Cloud workload integration

| Package | Description | Impact |
|---------|-------------|--------|
| **Rivulet.Azure** | Blob Storage, Cosmos DB, Service Bus, Functions adapters | 🟡 Med-High |
| **Rivulet.Aws** | S3, DynamoDB, SQS, Lambda adapters | 🟡 Med-High |

**Why**: Cloud-native workloads need frictionless integration.

---

### v1.5.0 - Advanced Features (Q1 2027)
**Goal**: Sophisticated data processing

| Package | Description | Impact |
|---------|-------------|--------|
| **Rivulet.Channels** | Priority queues, work-stealing, custom backpressure | 🟢 High |
| **Rivulet.Batching** | Adaptive batching, time-window + size-window hybrid | 🟡 Med-High |
| **Rivulet.Caching** | Async cache layers, de-dupe, dog-pile prevention | 🟡 Med-High |

---

### v1.6.0 - Durability (Q2 2027)
**Goal**: Long-running pipelines

| Package | Description | Impact |
|---------|-------------|--------|
| **Rivulet.Persistence** | Checkpointing, resume, idempotency tokens | 🟡 Medium |
| **Rivulet.Quotas** | Token bucket per tenant/key, dynamic throttles | 🟡 Medium |

---

### v1.7.0 - Message Queues (Q3 2027)
**Goal**: Event-driven workloads

| Package | Description | Impact |
|---------|-------------|--------|
| **Rivulet.Kafka** | Backpressure-aware consumption, checkpointing | 🟡 Med-High |
| **Rivulet.RabbitMQ** | Channel pooling, ack/nack semantics | 🟡 Medium |
| **Rivulet.SQS** | Visibility timeout management, batch operations | 🟡 Medium |

---

### v1.8.0 - Performance (Q4 2027)
**Goal**: Optimize hot paths

| Package | Description | Impact |
|---------|-------------|--------|
| **Rivulet.Serialization** | High-performance serializers (JSON, protobuf, MessagePack) | 🟡 Medium |
| **Rivulet.Monitoring.Prometheus** | Prometheus metrics exporter | 🟡 Medium |

---

### v2.0.0 - Pipeline Composition (Q1 2028)
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

### v2.1.0 - Advanced Tooling (Q3 2028)
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
- **🌐 Call HTTP APIs in parallel** → `Rivulet.Http`, `Rivulet.RetryPolicies`
- **☁️ Process cloud storage files** → `Rivulet.Azure` or `Rivulet.Aws`
- **🗃️ Run parallel database operations** → `Rivulet.Sql`
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
+ Rivulet.Hosting + Rivulet.RetryPolicies
```

### Cloud ETL Pipeline
```
Rivulet.Core + Rivulet.Azure + Rivulet.Sql
+ Rivulet.Diagnostics + Rivulet.Batching
```

### Event-Driven System
```
Rivulet.Core + Rivulet.Kafka + Rivulet.Persistence
+ Rivulet.Diagnostics.OpenTelemetry + Rivulet.Hosting
```

### Multi-Tenant SaaS
```
Rivulet.Core + Rivulet.Http + Rivulet.Quotas
+ Rivulet.Channels + Rivulet.Diagnostics
```

---

## Success Metrics

### v1.2.0 (Current)
- ✅ 5+ production deployments using Rivulet.Diagnostics
- ✅ 10+ projects using Rivulet.Hosting
- ✅ 95%+ test coverage across all packages

### v1.4.0 (2026)
- 🎯 1,000+ total ecosystem downloads
- 🎯 25+ production cloud workloads (Azure/AWS)
- 🎯 3+ blog posts from external users

### v2.0.0 (2028)
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

**Last Updated**: 2025-11-03
**Version**: 1.2.0
**Status**: v1.2.0 Complete - Planning v1.3.0
