# Rivulet Ecosystem Roadmap

## Current Status (v1.1.x)

### ✅ Implemented Core Features

The following features from the original Top 5 analysis are **already implemented** in Rivulet.Core v1.1.x:

1. **EventCounters + Metrics** ✅
   - Production visibility via `MetricsTracker` and `RivuletEventSource`
   - Real-time monitoring of active workers, queue depth, throughput, retries, failures

2. **Rate Limiting/Token Bucket** ✅
   - API throttling via `TokenBucket` and `RateLimitOptions`
   - Controlled bursts with sustained rate limits

3. **Circuit Breaker Pattern** ✅
   - Production resilience via `CircuitBreaker` and `CircuitBreakerOptions`
   - Automatic failure detection and recovery

4. **Adaptive Concurrency** ✅
   - Auto-tune parallelism via `AdaptiveConcurrencyController`
   - Dynamic adjustment based on performance metrics

### 🎯 Next Major Feature

**Pipeline Composition API** (planned for v2.0.0)
- Multi-stage processing with chained operators
- Different concurrency levels per stage
- Simplified ETL and data pipeline creation

---

## Ecosystem Strategy

Rivulet will expand from a single core library into a **focused ecosystem** of optional packages. This approach:
- Keeps `Rivulet.Core` lean and fast
- Enables deep integrations without bloat
- Allows users to pay only for what they use
- Maintains zero dependencies in Core

---

## Package Prioritization Matrix

### Tier 1: Essential Production Packages (Next 6 months)
**Goal**: Make Rivulet production-ready for enterprise scenarios

| Package | Impact | Difficulty | Priority | Status | Target Version |
|---------|--------|------------|----------|--------|----------------|
| **Rivulet.Diagnostics** | 🟢 Very High | 🟡 Medium | 🔴 Critical | ✅ **Complete** | v1.2.0 |
| **Rivulet.Diagnostics.OpenTelemetry** | 🟢 High | 🟡 Medium | 🔴 Critical | ✅ **Complete** | v1.2.0 |
| **Rivulet.Testing** | 🟢 High | 🟢 Low | 🟠 High | ✅ **Complete** | v1.2.0 |
| **Rivulet.Hosting** | 🟢 High | 🟡 Medium | 🟠 High | ✅ **Complete** | v1.2.0 |

**Rationale**:
- **Diagnostics**: Essential for production observability and SRE adoption
- **OpenTelemetry**: Industry standard, enables correlation across services
- **Testing**: Encourages best practices, reduces flaky tests in user code
- **Hosting**: Integration with .NET Generic Host is table stakes for production services

---

### Tier 2: High-Value Integrations (6-12 months)
**Goal**: Integrate with most common scenarios and tech stacks

| Package | Impact | Difficulty | Priority | Target Version |
|---------|--------|------------|----------|----------------|
| **Rivulet.Http** | 🟢 Very High | 🟡 Medium | 🔴 Critical | v1.3.0 |
| **Rivulet.RetryPolicies** | 🟢 High | 🟡 Medium | 🟠 High | v1.3.0 |
| **Rivulet.Sql** | 🟢 High | 🟡 Medium | 🟠 High | v1.3.0 |
| **Rivulet.Azure** | 🟡 Medium-High | 🟠 Med-High | 🟡 Medium | v1.4.0 |
| **Rivulet.Aws** | 🟡 Medium-High | 🟠 Med-High | 🟡 Medium | v1.4.0 |

**Rationale**:
- **Http**: 80% of I/O workloads are HTTP - makes Rivulet immediately useful
- **RetryPolicies**: Production-grade resilience without copying custom code
- **Sql**: Data-heavy batch jobs (ETL) need safe database parallelization
- **Cloud SDKs**: Cloud-native workloads want frictionless integration

---

### Tier 3: Advanced Features (12-18 months)
**Goal**: Enable sophisticated scenarios and differentiation

| Package | Impact | Difficulty | Priority | Target Version |
|---------|--------|------------|----------|----------------|
| **Rivulet.Channels** | 🟢 High | 🟠 Med-High | 🟡 Medium | v1.5.0 |
| **Rivulet.Batching** | 🟡 Medium-High | 🟡 Medium | 🟡 Medium | v1.5.0 |
| **Rivulet.Caching** | 🟡 Medium-High | 🟡 Medium | 🟡 Medium | v1.5.0 |
| **Rivulet.Persistence** | 🟡 Medium | 🟠 Med-High | 🟡 Medium | v1.6.0 |
| **Rivulet.Quotas** | 🟡 Medium | 🟡 Medium | 🟡 Medium | v1.6.0 |

**Rationale**:
- **Channels**: Priority queues and work-stealing for heterogeneous workloads
- **Batching**: Adaptive batching strategies beyond Core's basic batching
- **Caching**: Avoid duplicate expensive work, reduce downstream load
- **Persistence**: Durable pipeline state for long-running operations
- **Quotas**: Multi-tenant and per-key rate limiting

---

### Tier 4: Specialized Connectors (18-24 months)
**Goal**: Deep integration with specific technologies

| Package | Impact | Difficulty | Priority | Target Version |
|---------|--------|------------|----------|----------------|
| **Rivulet.Kafka** | 🟡 Medium-High | 🟠 Med-High | 🟡 Medium | v1.7.0 |
| **Rivulet.RabbitMQ** | 🟡 Medium | 🟠 Med-High | 🟡 Medium | v1.7.0 |
| **Rivulet.SQS** | 🟡 Medium | 🟠 Med-High | 🟡 Medium | v1.7.0 |
| **Rivulet.Serialization** | 🟡 Medium | 🟡 Medium | 🟡 Medium | v1.8.0 |
| **Rivulet.Monitoring.Prometheus** | 🟡 Medium | 🟢 Low | 🟡 Medium | v1.8.0 |

**Rationale**:
- **Message Queues**: Real-world pipelines often source from queues/streams
- **Serialization**: High-performance serializers for hot paths
- **Prometheus**: Alternative to OTel for teams that prefer Prometheus

---

### Tier 5: Advanced Developer Tools (24+ months)
**Goal**: Enhanced developer experience and performance

| Package | Impact | Difficulty | Priority | Target Version |
|---------|--------|------------|----------|----------------|
| **Rivulet.Generators** | 🟡 Medium | 🔴 High | 🟡 Medium | v2.1.0 |
| **Rivulet.Dataflow** | 🟡 Medium | 🟡 Medium | 🟡 Medium | v2.1.0 |
| **Rivulet.Tracing** | 🟡 Medium | 🟡 Medium | 🟢 Low | v2.2.0 |

**Rationale**:
- **Generators**: Source generators for compile-time optimizations
- **Dataflow**: Bridge to TPL Dataflow for migration scenarios
- **Tracing**: Lightweight tracing without full OTel dependency

**Note**: Samples and examples are maintained in the main repository's `samples/` folder and within each package's README, rather than as a separate NuGet package.

---

## Detailed Roadmap by Version

### v1.2.0 - Production Observability (Q1 2026)
**Theme**: Enterprise-ready observability and hosting

**Packages**:
1. **Rivulet.Diagnostics** ✅ **COMPLETE**
   - EventListener wrappers (Console, File, Structured JSON)
   - Metrics aggregation with time-window statistics
   - Prometheus export support
   - Health check integration
   - Fluent DiagnosticsBuilder API

2. **Rivulet.Diagnostics.OpenTelemetry** ✅ **COMPLETE**
   - Activities, Metrics, Logs integration
   - Semantic conventions for parallel processing
   - Distributed tracing support
   - Correlation across services

3. **Rivulet.Testing** ✅ **COMPLETE**
   - Virtual time for testing
   - Fake channels for isolation
   - Chaos injection (faults, timeouts)
   - Concurrency assertions

4. **Rivulet.Hosting** ✅ **COMPLETE**
   - `IHostedService` wrappers
   - Dependency injection extensions
   - Health checks (backlog size, stalled workers)
   - Configuration binding from `IConfiguration`

**Estimated Timeline**: 3-4 months
**Dependencies**: None (all build on Core)

---

### v1.3.0 - Common Integrations (Q2-Q3 2026)
**Theme**: Make common scenarios turnkey

**Packages**:
1. **Rivulet.Http**
   - `HttpClient`-aware operators
   - Per-request cancellation
   - Streaming bodies with backpressure
   - Resilient download/upload helpers
   - Example: `SelectParallelHttpAsync`, `DownloadStreamParallelAsync`

2. **Rivulet.RetryPolicies**
   - Exponential backoff with jitter
   - Decorrelated jitter
   - Hedging (dual requests)
   - Circuit breaker integration
   - Policy composition

3. **Rivulet.Sql**
   - ADO.NET async operations with concurrency limits
   - Connection pooling awareness
   - Transaction-scoped parallelism
   - Batched commands
   - Safe parallel DB operations

**Estimated Timeline**: 3-4 months
**Dependencies**: v1.2.0

---

### v1.4.0 - Cloud Integration (Q4 2026)
**Theme**: Cloud-native workload support

**Packages**:
1. **Rivulet.Azure**
   - Blob Storage parallel transfers with backpressure
   - Cosmos DB batch operations
   - Service Bus integration
   - Functions-friendly adapters

2. **Rivulet.Aws**
   - S3 parallel transfers
   - DynamoDB batch operations
   - SQS message processing
   - Lambda-friendly adapters

3. **Rivulet.Gcp**
   - Cloud Storage parallel transfers
   - Firestore batch operations
   - Pub/Sub integration

**Estimated Timeline**: 4-5 months
**Dependencies**: v1.3.0 (Http)

---

### v1.5.0 - Advanced Operators (Q1 2027)
**Theme**: Sophisticated data processing

**Packages**:
1. **Rivulet.Channels**
   - Priority channels
   - Fair-queue mux/demux
   - Work-stealing for heterogeneous workloads
   - Custom backpressure policies

2. **Rivulet.Batching**
   - Time-window + size-window hybrid batching
   - Dynamic batch sizing (adaptive to throughput/latency)
   - Group-by-key batching
   - Advanced aggregation strategies

3. **Rivulet.Caching**
   - Async cache layers (memory/Redis)
   - Batching + de-dupe for identical keys
   - Dog-pile prevention
   - Per-key throttling

**Estimated Timeline**: 3-4 months
**Dependencies**: v1.4.0

---

### v1.6.0 - Durability & Multi-tenancy (Q2 2027)
**Theme**: Long-running pipelines and tenant isolation

**Packages**:
1. **Rivulet.Persistence**
   - Durable pipeline state and resume
   - Checkpointing of in-flight items
   - Storing retry state
   - Dedupe keys and idempotency tokens
   - Storage adapters (file, Redis, SQL)

2. **Rivulet.Quotas**
   - Token bucket per tenant/key
   - Leaky bucket implementation
   - Dynamic throttles from configuration
   - Concurrency budgeting across pipelines

**Estimated Timeline**: 3-4 months
**Dependencies**: v1.5.0 (Channels, Caching)

---

### v1.7.0 - Message Queue Connectors (Q3 2027)
**Theme**: Event-driven and streaming workloads

**Packages**:
1. **Rivulet.Kafka**
   - Backpressure-aware consumption
   - At-least-once and at-most-once patterns
   - Checkpointing hooks
   - Outbox-style idempotency helpers

2. **Rivulet.RabbitMQ**
   - Channel pooling
   - Ack/Nack semantics
   - Dead letter queue handling

3. **Rivulet.SQS**
   - Visibility timeout management
   - Batch operations
   - Message attributes support

**Estimated Timeline**: 4-5 months
**Dependencies**: v1.6.0 (Persistence for checkpointing)

---

### v1.8.0 - Performance & Monitoring (Q4 2027)
**Theme**: Optimize hot paths and alternative monitoring

**Packages**:
1. **Rivulet.Serialization**
   - High-performance serializers (System.Text.Json, protobuf, MessagePack)
   - Precompiled delegates
   - Zero-copy streams
   - Operator overloads: `SelectParallelDeserializeAsync`

2. **Rivulet.Monitoring.Prometheus**
   - Prometheus metrics exporter
   - Counters, gauges, histograms
   - Complements OTel for Prometheus-first teams

**Estimated Timeline**: 2-3 months
**Dependencies**: v1.2.0 (Diagnostics)

---

### v2.0.0 - Pipeline Composition (Q1 2028)
**Theme**: Multi-stage processing and orchestration

**Core Enhancement**:
1. **Pipeline Composition API**
   - Fluent API for chaining operations
   - Different concurrency per stage
   - Backpressure management between stages
   - Error propagation across pipeline
   - Streaming and buffered modes

**Example**:
```csharp
var pipeline = PipelineBuilder<string, ProcessedData>
    .StartWith(urls)
    .SelectParallel(FetchDataAsync, concurrency: 32)
    .ThenSelectParallel(TransformAsync, concurrency: 16)
    .ThenSelectParallel(ValidateAsync, concurrency: 8)
    .ThenBatch(100)
    .ThenForEachParallel(SaveBatchAsync, concurrency: 4)
    .WithCircuitBreaker(threshold: 0.1)
    .WithRetries(3)
    .Build();

var results = await pipeline.ExecuteAsync(cancellationToken);
```

**Breaking Changes**: None - additive API
**Estimated Timeline**: 6-8 months
**Dependencies**: All existing packages benefit from pipeline API

---

### v2.1.0 - Advanced Tooling (Q3 2028)
**Theme**: Developer experience and migration

**Packages**:
1. **Rivulet.Generators**
   - Source generators for compile-time optimizations
   - Avoiding closures
   - Prebinding delegates
   - Fast-paths for value types
   - Auto XML doc examples

2. **Rivulet.Dataflow**
   - TPL Dataflow interoperability
   - Convert Rivulet pipelines to Dataflow blocks
   - Migration helpers
   - Hybrid scenarios

**Estimated Timeline**: 4-5 months
**Dependencies**: v2.0.0 (Pipeline API)

---

## Package Versioning Strategy

All Rivulet.* packages will follow **synchronized versioning** with Core:
- `Rivulet.Core 1.2.0` aligns with `Rivulet.Diagnostics 1.2.0`, `Rivulet.Http 1.2.0`, etc.
- Ensures API compatibility across the ecosystem
- Simplifies dependency management
- Clear communication about feature sets

**Exceptions**:
- Packages targeting v2.0+ may have different major versions if they introduce breaking changes
- Pre-release packages (alpha/beta) may version independently

---

## Package Architecture Principles

### 1. **Zero Dependencies in Core**
- `Rivulet.Core` has only minimal dependencies (System.Linq.Async, System.Threading.Channels)
- All optional features go in separate packages
- Users only pay for what they use

### 2. **Granular Packages**
- Heavy dependencies (cloud SDKs, Kafka) get separate packages
- Sub-namespaces for clarity: `Rivulet.Diagnostics.OpenTelemetry`, `Rivulet.Caching.Redis`
- Users can mix and match

### 3. **Consistent API Patterns**
- Extension methods for Core operators
- Options classes with `init` properties
- Async-first, `ValueTask<T>` in hot paths
- Follow Core's error handling philosophy

### 4. **Testing Requirements**
- Each package maintains ≥95% line coverage
- Integration tests for external dependencies
- Performance benchmarks for hot paths

### 5. **Documentation Standards**
- XML documentation for all public APIs
- README per package with examples
- Migration guides where applicable

---

## Success Metrics

### v1.2.0 Targets
- 🎯 **Adoption**: 1,000+ downloads of Diagnostics packages
- 🎯 **Production Use**: 10+ companies using Rivulet.Hosting in production
- 🎯 **Community**: 50+ GitHub stars
- 🎯 **Quality**: 95%+ test coverage across all packages

### v1.4.0 Targets
- 🎯 **Adoption**: 5,000+ total ecosystem downloads
- 🎯 **Cloud Integration**: 25+ companies using Azure/AWS packages
- 🎯 **Ecosystem**: 5+ community-contributed samples
- 🎯 **Documentation**: Complete scenario guides for common use cases

### v2.0.0 Targets
- 🎯 **Adoption**: 10,000+ total ecosystem downloads
- 🎯 **Pipeline API**: 100+ production pipelines using v2.0 API
- 🎯 **Community**: 200+ GitHub stars, 10+ external contributors
- 🎯 **Recognition**: Mentioned in .NET blogs, conferences, podcasts

---

## Community Engagement

### Open Source Strategy
- All packages remain MIT licensed
- Community contributions welcomed for:
  - Cloud connector packages
  - Message queue integrations
  - Serialization providers
  - Sample scenarios

### Documentation
- Comprehensive README per package
- API reference documentation
- Real-world scenario guides
- Performance benchmarking results
- Migration guides from competing libraries

### Support Channels
- GitHub Issues for bug reports
- GitHub Discussions for questions and scenarios
- Stack Overflow tag: `rivulet`
- Sample repository with runnable scenarios

---

## Risk Mitigation

### Technical Risks

**Risk**: Package ecosystem becomes fragmented or abandoned
- **Mitigation**: Focus on Tier 1 packages first, ensure high quality before expanding
- **Mitigation**: Synchronized versioning keeps ecosystem cohesive

**Risk**: Breaking changes in dependencies (cloud SDKs, OTel)
- **Mitigation**: Abstract external dependencies behind interfaces
- **Mitigation**: Comprehensive integration tests catch breaking changes early

**Risk**: Performance regressions from new features
- **Mitigation**: Continuous benchmarking in CI/CD
- **Mitigation**: Separate packages ensure Core remains fast

### Market Risks

**Risk**: Low adoption of ecosystem packages
- **Mitigation**: Start with highest-value packages (Diagnostics, Http)
- **Mitigation**: Real-world samples and documentation
- **Mitigation**: Blog posts and conference talks

**Risk**: Competition from established libraries
- **Mitigation**: Focus on async-first, I/O-optimized niche
- **Mitigation**: Better ergonomics than TPL Dataflow
- **Mitigation**: Production-ready features (observability, resilience)

---

## Next Steps

### Immediate (Next 30 Days)
1. ✅ Review roadmap with stakeholders
2. ✅ Create GitHub milestones for v1.2.0 packages
3. ✅ Set up package project structure (`src/Rivulet.Diagnostics/`, etc.)
4. ✅ Design API surface for Rivulet.Diagnostics
5. ✅ Implement EventListener integration

### Q1 2026 (v1.2.0 Development)
1. ✅ Implement Rivulet.Diagnostics
2. ✅ Implement Rivulet.Diagnostics.OpenTelemetry
3. ✅ Implement Rivulet.Testing
4. ✅ Implement Rivulet.Hosting
5. 🔲 Write comprehensive documentation
6. 🔲 Publish v1.2.0 to NuGet

### Q2-Q3 2026 (v1.3.0 Development)
1. 🔲 Implement Rivulet.Http
2. 🔲 Implement Rivulet.RetryPolicies
3. 🔲 Implement Rivulet.Sql
4. 🔲 Performance benchmarks for new packages
5. 🔲 Publish v1.3.0 to NuGet

---

**Last Updated**: 2025-01-02
**Version**: 1.2.0
**Status**: v1.2.0 Implementation Complete - Ready for Release
**Next Review**: Q2 2025
