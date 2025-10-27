# Rivulet Ecosystem Packages - Priority Matrix

## Visual Priority Map

```
         Impact
           ^
   Very    |  [Diagnostics]    [Http]
   High  5 |  [OTel]           [Pipeline v2.0]
           |
   High  4 |  [Testing]   [RetryPolicies]  [Channels]
           |  [Hosting]   [Sql]
           |
  Medium 3 |  [Azure]   [Batching]   [Persistence]
           |  [Aws]     [Caching]    [Quotas]
           |
   Low   2 |  [Kafka]  [Serialization]  [Generators]
           |  [RabbitMQ]  [Prometheus]
           |
         1 |  [Samples]  [Tracing]  [Dataflow]
           |
           +---------------------------------------->
              Low     Medium    Med-High    High
                    (1-2)      (3-4)      (5)
                         Difficulty
```

---

## Tier 1: Must-Have (v1.2.0 - Next 6 months)
**Critical for production adoption**

| Rank | Package | Why Critical | Timeline |
|------|---------|-------------|----------|
| 🥇 **1** | **Rivulet.Diagnostics** | Production observability, SRE adoption, monitoring/alerting | Q1 2026 |
| 🥈 **2** | **Rivulet.Diagnostics.OpenTelemetry** | Industry standard, distributed tracing | Q1 2026 |
| 🥉 **3** | **Rivulet.Testing** | Encourages best practices, reduces flaky tests | Q1 2026 |
| **4** | **Rivulet.Hosting** | .NET Generic Host integration, table stakes | Q1 2026 |
| **5** | **Rivulet.Samples** | Accelerates onboarding, best practices | Q1 2026 |

**Success Criteria**: Companies can confidently deploy Rivulet in production with full observability

---

## Tier 2: High-Value (v1.3.0-v1.4.0 - 6-12 months)
**Makes common scenarios turnkey**

| Rank | Package | Why Important | Timeline |
|------|---------|--------------|----------|
| 🥇 **6** | **Rivulet.Http** | 80% of I/O workloads are HTTP | Q2-Q3 2026 |
| 🥈 **7** | **Rivulet.RetryPolicies** | Production-grade resilience without custom code | Q2-Q3 2026 |
| 🥉 **8** | **Rivulet.Sql** | ETL and data-heavy batch jobs | Q2-Q3 2026 |
| **9** | **Rivulet.Azure** | Cloud-native Azure workloads | Q4 2026 |
| **10** | **Rivulet.Aws** | Cloud-native AWS workloads | Q4 2026 |

**Success Criteria**: Developers can build common scenarios (HTTP APIs, databases, cloud storage) with minimal code

---

## Tier 3: Advanced (v1.5.0-v1.6.0 - 12-18 months)
**Sophisticated scenarios and differentiation**

| Rank | Package | Why Valuable | Timeline |
|------|---------|-------------|----------|
| **11** | **Rivulet.Channels** | Priority queues, work-stealing for complex workloads | Q1 2027 |
| **12** | **Rivulet.Batching** | Adaptive batching beyond Core's basics | Q1 2027 |
| **13** | **Rivulet.Caching** | Avoid duplicate work, reduce downstream load | Q1 2027 |
| **14** | **Rivulet.Persistence** | Durable pipeline state for long-running operations | Q2 2027 |
| **15** | **Rivulet.Quotas** | Multi-tenant and per-key rate limiting | Q2 2027 |

**Success Criteria**: Advanced users can build sophisticated multi-tenant, long-running pipelines

---

## Tier 4: Specialized (v1.7.0-v1.8.0 - 18-24 months)
**Deep integration with specific technologies**

| Rank | Package | Why Useful | Timeline |
|------|---------|-----------|----------|
| **16** | **Rivulet.Kafka** | Event-driven architectures, streaming workloads | Q3 2027 |
| **17** | **Rivulet.RabbitMQ** | Message queue processing | Q3 2027 |
| **18** | **Rivulet.SQS** | AWS message queue integration | Q3 2027 |
| **19** | **Rivulet.Serialization** | High-performance serialization in hot paths | Q4 2027 |
| **20** | **Rivulet.Monitoring.Prometheus** | Alternative to OTel for Prometheus-first teams | Q4 2027 |

**Success Criteria**: Rivulet integrates seamlessly with major event-driven and data serialization technologies

---

## Tier 5: Future (v2.0+ - 24+ months)
**Advanced developer experience**

| Rank | Package | Why Interesting | Timeline |
|------|---------|----------------|----------|
| **21** | **Rivulet.Generators** | Source generators for compile-time optimizations | Q3 2028 |
| **22** | **Rivulet.Dataflow** | TPL Dataflow bridge for migration scenarios | Q3 2028 |
| **23** | **Rivulet.Tracing** | Lightweight tracing without OTel dependency | Q4 2028 |
| **24** | **Rivulet.Gcp** | Google Cloud Platform integration | Q4 2028 |

**Success Criteria**: Rivulet provides best-in-class DX and covers all major cloud platforms

---

## Special: Core Enhancement

**Pipeline Composition API (v2.0.0 - Q1 2028)**
- Not a separate package - enhancement to Rivulet.Core
- Multi-stage processing with chained operators
- Different concurrency per stage
- Transforms Rivulet from utility library to framework

---

## Dependency Graph

```
v1.2.0 Foundation
├── Rivulet.Diagnostics ⭐
├── Rivulet.Diagnostics.OpenTelemetry ⭐
├── Rivulet.Testing ⭐
├── Rivulet.Hosting ⭐
└── Rivulet.Samples

v1.3.0 Common Integrations
├── Rivulet.Http ⭐ (depends on Core)
├── Rivulet.RetryPolicies (depends on Core)
└── Rivulet.Sql (depends on Core)

v1.4.0 Cloud
├── Rivulet.Azure (depends on Http)
└── Rivulet.Aws (depends on Http)

v1.5.0 Advanced
├── Rivulet.Channels (depends on Core)
├── Rivulet.Batching (depends on Core)
└── Rivulet.Caching (depends on Core)

v1.6.0 Durability
├── Rivulet.Persistence (depends on Caching)
└── Rivulet.Quotas (depends on Channels)

v1.7.0 Message Queues
├── Rivulet.Kafka (depends on Persistence)
├── Rivulet.RabbitMQ (depends on Persistence)
└── Rivulet.SQS (depends on Persistence)

v1.8.0 Performance
├── Rivulet.Serialization (depends on Core)
└── Rivulet.Monitoring.Prometheus (depends on Diagnostics)

v2.0.0 Major
└── Pipeline Composition API (Core enhancement)

v2.1.0 Tooling
├── Rivulet.Generators (depends on v2.0)
└── Rivulet.Dataflow (depends on v2.0)
```

⭐ = Critical path packages

---

## Implementation Order Rationale

### Phase 1 (v1.2.0): Observability First
**Why**: Without observability, companies won't deploy to production
- Diagnostics enables monitoring, alerting, and troubleshooting
- OpenTelemetry is industry standard for distributed systems
- Testing tools prevent user code from becoming flaky
- Hosting integration is expected for production services

### Phase 2 (v1.3.0): Common Scenarios
**Why**: HTTP is the most common I/O workload
- Http package immediately useful for 80% of users
- RetryPolicies adds production-grade resilience
- Sql covers data-heavy ETL scenarios
- Quick wins for adoption

### Phase 3 (v1.4.0): Cloud Native
**Why**: Cloud workloads are increasingly common
- Azure and AWS cover majority of cloud deployments
- Builds on Http foundation
- Natural progression from generic HTTP to cloud-specific

### Phase 4 (v1.5.0): Advanced Features
**Why**: Users have requested sophisticated features
- Channels enable priority queues and work-stealing
- Batching improvements for high-throughput scenarios
- Caching avoids duplicate expensive work

### Phase 5 (v1.6.0): Long-Running Pipelines
**Why**: Production systems need durability
- Persistence enables checkpointing and resume
- Quotas enable multi-tenant scenarios
- Builds on Caching and Channels foundations

### Phase 6 (v1.7.0+): Specialized Connectors
**Why**: Niche but important use cases
- Message queues for event-driven architectures
- Serialization for performance-critical paths
- Prometheus for specific monitoring needs

### Phase 7 (v2.0.0): Framework Evolution
**Why**: Strategic transformation
- Pipeline API transforms Rivulet from utility to framework
- Enables complex multi-stage scenarios
- Major version allows breaking changes if needed

---

## Package Combinations (Common Scenarios)

### Scenario 1: Production Web API
```
Rivulet.Core (base)
+ Rivulet.Http (API calls)
+ Rivulet.Diagnostics.OpenTelemetry (monitoring)
+ Rivulet.Hosting (background service)
+ Rivulet.RetryPolicies (resilience)
```

### Scenario 2: Cloud ETL Pipeline
```
Rivulet.Core (base)
+ Rivulet.Azure (Blob Storage)
+ Rivulet.Sql (database)
+ Rivulet.Diagnostics (monitoring)
+ Rivulet.Batching (efficient processing)
```

### Scenario 3: Event-Driven System
```
Rivulet.Core (base)
+ Rivulet.Kafka (events)
+ Rivulet.Persistence (checkpointing)
+ Rivulet.Diagnostics.OpenTelemetry (distributed tracing)
+ Rivulet.Hosting (consumer service)
```

### Scenario 4: High-Performance API Client
```
Rivulet.Core (base)
+ Rivulet.Http (API calls)
+ Rivulet.Caching (avoid duplicate requests)
+ Rivulet.Serialization (fast JSON)
+ Rivulet.RetryPolicies (resilience)
```

### Scenario 5: Multi-Tenant SaaS
```
Rivulet.Core (base)
+ Rivulet.Http (API calls)
+ Rivulet.Quotas (per-tenant rate limits)
+ Rivulet.Channels (priority queues)
+ Rivulet.Diagnostics (per-tenant metrics)
```

---

## Quick Decision Tree

**I need to...**

**📊 Monitor production pipelines**
→ Rivulet.Diagnostics, Rivulet.Diagnostics.OpenTelemetry

**🌐 Call HTTP APIs in parallel**
→ Rivulet.Http, Rivulet.RetryPolicies

**☁️ Process cloud storage files**
→ Rivulet.Azure or Rivulet.Aws

**🗃️ Run parallel database operations**
→ Rivulet.Sql

**📨 Process message queue events**
→ Rivulet.Kafka, Rivulet.RabbitMQ, or Rivulet.SQS

**⚡ Optimize performance-critical code**
→ Rivulet.Serialization, Rivulet.Caching

**🏢 Deploy as hosted service**
→ Rivulet.Hosting

**🧪 Test my pipeline code**
→ Rivulet.Testing

**🔄 Build multi-stage pipeline**
→ Wait for v2.0.0 Pipeline Composition API

---

## Resource Allocation

### Team Size Assumptions
- **Core Team**: 1-2 developers
- **Part-time Contributors**: 2-3 developers
- **Community Contributors**: Unpredictable

### Time Estimates per Package

| Package Complexity | Development | Testing | Documentation | Total |
|-------------------|-------------|---------|---------------|-------|
| **Simple** (Samples, Tracing) | 1-2 weeks | 1 week | 1 week | 3-4 weeks |
| **Medium** (Diagnostics, Http) | 3-4 weeks | 2 weeks | 1-2 weeks | 6-8 weeks |
| **Med-High** (Cloud SDKs, Kafka) | 4-6 weeks | 3 weeks | 2 weeks | 9-11 weeks |
| **High** (Pipeline v2.0, Generators) | 8-12 weeks | 4 weeks | 3 weeks | 15-19 weeks |

### v1.2.0 Timeline Example
- Rivulet.Diagnostics: 6-8 weeks
- Rivulet.Diagnostics.OpenTelemetry: 6-8 weeks (parallel)
- Rivulet.Testing: 6-8 weeks (parallel)
- Rivulet.Hosting: 6-8 weeks (parallel)
- Rivulet.Samples: 3-4 weeks (after others)

**Total**: ~12-14 weeks with 2-4 parallel work streams

---

## Success Metrics by Tier

### Tier 1 Success (v1.2.0)
- ✅ 5+ production deployments using Rivulet.Diagnostics
- ✅ 10+ projects using Rivulet.Hosting
- ✅ Positive feedback from SRE/DevOps teams
- ✅ Documentation rated 4.5+ stars

### Tier 2 Success (v1.3.0-v1.4.0)
- ✅ 100+ projects using Rivulet.Http
- ✅ 25+ production cloud workloads (Azure/AWS)
- ✅ 3+ blog posts from external users
- ✅ 1,000+ total ecosystem downloads

### Tier 3+ Success (v1.5.0+)
- ✅ 5,000+ total ecosystem downloads
- ✅ 50+ GitHub stars
- ✅ 10+ community contributions
- ✅ Featured in .NET newsletter or conference

---

**Last Updated**: 2025-10-27
**See Also**: [ROADMAP.md](./ROADMAP.md) for detailed version plans
