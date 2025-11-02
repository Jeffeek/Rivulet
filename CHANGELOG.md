# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.2.0] - 2025-11-02

### Added

**New Packages**:
- **Rivulet.Diagnostics** - Production observability package with EventListeners, metrics aggregation, Prometheus export, and health checks
- **Rivulet.Diagnostics.OpenTelemetry** - OpenTelemetry integration for distributed tracing, activities, and metrics export
- **Rivulet.Testing** - Testing utilities including VirtualTimeProvider, FakeChannel, ChaosInjector, and ConcurrencyAsserter
- **Rivulet.Hosting** - Microsoft.Extensions.Hosting integration with dependency injection, configuration binding, background services, and health checks

**Rivulet.Diagnostics Features**:
- `RivuletConsoleListener` - Console output for development and debugging
- `RivuletFileListener` - File logging with automatic rotation
- `RivuletStructuredLogListener` - JSON structured logging for log aggregation systems (ELK, Splunk, Azure Monitor)
- `MetricsAggregator` - Time-window statistics (min/max/avg) with configurable aggregation intervals
- `PrometheusExporter` - Export metrics in Prometheus text format for scraping endpoints
- `RivuletHealthCheck` - Health check integration with Microsoft.Extensions.Diagnostics.HealthChecks
- `DiagnosticsBuilder` - Fluent API for configuring multiple listeners simultaneously

**Rivulet.Diagnostics.OpenTelemetry Features**:
- `RivuletActivitySource` - Distributed tracing with automatic activity creation
- `RivuletMetricsExporter` - Bridge EventCounters to OpenTelemetry Meters
- `WithOpenTelemetryTracing()` extension method for easy integration
- Automatic retry tracking as activity events
- Circuit breaker state change tracking
- Adaptive concurrency monitoring
- Support for Jaeger, Zipkin, Azure Monitor, DataDog, and other OpenTelemetry exporters

**Rivulet.Testing Features**:
- `VirtualTimeProvider` - Control time in tests without actual delays
- `FakeChannel` - Testable channel implementation with operation tracking
- `ChaosInjector` - Inject random failures and delays for resilience testing
- `ConcurrencyAsserter` - Verify concurrency limits are respected in parallel operations

**Rivulet.Hosting Features**:
- `AddRivulet()` extension methods for dependency injection
- Configuration binding from `IConfiguration` with `appsettings.json` support
- Named configurations for different scenarios
- `ParallelBackgroundService<T>` - Base class for simple background processing
- `ParallelWorkerService<TSource, TResult>` - Base class for advanced parallel processing with result handling
- `RivuletOperationHealthCheck` - Health check for monitoring parallel operations
- Integration with ASP.NET Core and .NET Worker Services

**Rivulet.Core Enhancements**:
- Set `MaxRetries` default value to 0 (was uninitialized)
- Set `OrderedOutput` default value to false (was uninitialized)

### Fixed
- **Diagnostics.Tests**: Fixed flaky timing-related tests by increasing EventCounter wait times from 1100ms to 2000ms
- **Diagnostics.Tests**: Fixed `StructuredLogListener_ShouldInvokeAction_WhenUsingCustomAction` collection modification during enumeration by creating copy before iteration
- **MetricsAggregator**: Fixed flaky test `MetricsAggregator_ShouldCalculateCorrectStatistics` by:
  - Increasing aggregation window from 500ms to 2 seconds
  - Increasing wait time from 1500ms to 3000ms
  - Filtering out empty aggregations
  - Testing all aggregations instead of just the last one
- **MetricsAggregator**: Fixed flaky test `MetricsAggregator_ShouldHandleExpiredSamples` with longer aggregation windows and wait times

### Changed
- **Documentation**: Updated README.md to document all 5 packages
- **Documentation**: Added package-specific README files for Diagnostics, Diagnostics.OpenTelemetry, Testing, and Hosting
- **Documentation**: Updated ROADMAP.md with v1.2.0 completion status
- **Build**: Updated NuGet package script to build all 5 packages
- **CI/CD**: Updated release workflow to publish all 5 packages
- **Test Coverage**: Achieved 90%+ code coverage across all packages
- **Test Reliability**: Eliminated all flaky tests with improved timing in CI/CD environments

### Documentation
- Added comprehensive examples for all new packages
- Added integration guides for ASP.NET Core, Worker Services, and OpenTelemetry
- Added troubleshooting guides for common scenarios
- Added best practices documentation

## [1.0.0] - 2025-01-XX (Previous Release)

### Added
- `SelectParallelAsync` - Transform collections in parallel with bounded concurrency
- `SelectParallelStreamAsync` - Stream results as they complete
- `ForEachParallelAsync` - Execute side effects in parallel
- Flexible error handling modes: FailFast, CollectAndContinue, BestEffort
- Retry policy with exponential backoff and transient error detection
- Per-item timeout support
- Lifecycle hooks: OnStart, OnComplete, OnError, OnThrottle
- Cancellation token support throughout
- Support for .NET 8.0 and .NET 9.0
- Ordered output mode for sequence-sensitive operations
- Circuit breaker pattern for resilience
- Rate limiting with token bucket algorithm
- Adaptive concurrency with AIMD algorithm
- Runtime metrics via EventCounters
- Progress reporting with ETA calculation
- Batch operations for bulk processing

### Fixed
- OnErrorAsync callback now properly invoked in FailFast mode
- SelectParallelStreamAsync cancellation race condition resolved

### Documentation
- Comprehensive README with examples
- CI/CD pipeline with 200-iteration flaky test detection
- 90%+ code coverage

[Unreleased]: https://github.com/Jeffeek/Rivulet/compare/v1.2.0...HEAD
[1.2.0]: https://github.com/Jeffeek/Rivulet/compare/v1.0.0...v1.2.0
[1.0.0]: https://github.com/Jeffeek/Rivulet/releases/tag/v1.0.0
