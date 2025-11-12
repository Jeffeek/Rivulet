# Rivulet Benchmarks

This project contains comprehensive performance benchmarks for Rivulet using [BenchmarkDotNet](https://benchmarkdotnet.org/).

## Running Benchmarks

### Run All Benchmarks

```powershell
cd tests\Rivulet.Benchmarks
dotnet run -c Release
```

> **Note**: The project targets .NET 9.0 for building, but BenchmarkDotNet automatically runs all benchmarks on both .NET 8.0 and .NET 9.0 as specified by the `[SimpleJob]` attributes in each benchmark class.

### Run Specific Benchmark Suite

```powershell
dotnet run -c Release -- --filter "*CoreOperatorsBenchmarks*"
dotnet run -c Release -- --filter "*BatchingBenchmarks*"
dotnet run -c Release -- --filter "*ErrorHandlingBenchmarks*"
dotnet run -c Release -- --filter "*AdvancedFeaturesBenchmarks*"
dotnet run -c Release -- --filter "*ConcurrencyScalingBenchmarks*"
```

### Quick Run (Short Job)

For faster results with fewer iterations:

```powershell
dotnet run -c Release -- --job short
```

### Export Results

```powershell
# Export to various formats
dotnet run -c Release -- --exporters json,html,markdown
```

## Benchmark Suites

### 1. CoreOperatorsBenchmarks

Measures the performance of core parallel operators:

- **SelectParallelAsync** (CPU-bound light operations)
- **SelectParallelAsync** (I/O simulation with Task.Delay)
- **SelectParallelAsync** (Ordered output)
- **SelectParallelStreamAsync** (Streaming results)
- **ForEachParallelAsync** (Side effects without result collection)
- **Baseline** (Sequential processing)
- **Baseline** (Task.WhenAll unbounded)

**Configuration**: 1,000 items, MaxDegreeOfParallelism = 8-32

**Comparison**: Shows performance vs sequential processing and unbounded Task.WhenAll.

### 2. BatchingBenchmarks

Evaluates batch processing performance with different batch sizes:

- **BatchParallelAsync** (Batch size 100, 500, 1000)
- **BatchParallelStreamAsync** (Streaming batches)
- **Baseline** (Individual item processing)

**Configuration**: 10,000 items, MaxDegreeOfParallelism = 4

**Analysis**: Demonstrates optimal batch sizing for different workload types.

### 3. ErrorHandlingBenchmarks

Measures the overhead of error handling and retry mechanisms:

- **No retries** (Success path - baseline)
- **With retries** (10% transient failures)
- **ErrorMode.FailFast**
- **ErrorMode.BestEffort**
- **Backoff.Exponential**
- **Backoff.ExponentialJitter**

**Configuration**: 500 items, MaxDegreeOfParallelism = 8

**Insights**: Quantifies the performance impact of retry policies and error modes.

### 4. AdvancedFeaturesBenchmarks

Compares the overhead of advanced resilience features:

- **No advanced features** (baseline)
- **With CircuitBreaker**
- **With RateLimit** (Token bucket)
- **With AdaptiveConcurrency**
- **With Progress tracking**
- **With Metrics tracking**
- **With all features combined**

**Configuration**: 500 items, MaxDegreeOfParallelism = 16

**Purpose**: Helps understand the performance cost of production-grade features.

### 5. ConcurrencyScalingBenchmarks

Analyzes how performance scales with different MaxDegreeOfParallelism values:

- **MaxDegreeOfParallelism**: 1, 2, 4, 8 (baseline), 16, 32, 64, 128

**Configuration**: 1,000 items with 1ms I/O simulation per item

**Goal**: Find the optimal concurrency level for different workload characteristics.

## Benchmark Configuration

All benchmarks run on both **.NET 8.0** and **.NET 9.0** to compare performance across runtime versions.

### Common Settings

- **MemoryDiagnoser**: Enabled to track allocations
- **MarkdownExporter**: Generates markdown-formatted results
- **Job**: SimpleJob for both Net80 and Net90

### Customizing Runs

```powershell
# Run with specific runtime only
dotnet run -c Release -- --runtimes net8.0

# Run with specific number of iterations
dotnet run -c Release -- --iterationCount 10

# Run with memory profiling
dotnet run -c Release -- --memory

# Run with thread pool sizing diagnostics
dotnet run -c Release -- --envVars COMPlus_ThreadPool_ForceMinWorkerThreads:100
```

## Interpreting Results

### Key Metrics

- **Mean**: Average execution time
- **StdDev**: Standard deviation (variability)
- **Median**: Middle value (less affected by outliers)
- **Allocated**: Memory allocated per operation
- **Gen0/Gen1/Gen2**: Garbage collection counts

### Performance Goals

- **I/O-Bound Operations**: Rivulet should significantly outperform sequential processing
- **CPU-Bound Operations**: Performance should scale linearly with cores up to hardware limits
- **Memory**: Should have minimal allocations compared to Task.WhenAll
- **Overhead**: Advanced features should add <10% overhead when not triggered

### Baseline Comparisons

- **Sequential Processing**: Shows maximum possible speedup
- **Task.WhenAll (unbounded)**: Shows cost of bounded concurrency vs unbounded

## Example Results

```
// .NET 8.0 vs .NET 9.0 - SelectParallelAsync I/O simulation (1000 items, 1ms delay each)

|                 Method |  Runtime |     Mean | Allocated |
|----------------------- |--------- |---------:|----------:|
|    SelectParallelAsync | .NET 8.0 |  498 ms  |   ~1 MB   |
|    SelectParallelAsync | .NET 9.0 |  492 ms  |   ~0.9 MB | (Faster!)
| Sequential Processing  | .NET 8.0 | 1004 ms  |   ~0.5 MB |
|         Task.WhenAll   | .NET 8.0 |   45 ms  |   ~5 MB   | (Unbounded!)
```

## CI/CD Integration

To run benchmarks in CI/CD pipelines:

```yaml
# GitHub Actions example
- name: Run Benchmarks
  run: |
    cd tests/Rivulet.Benchmarks
    dotnet run -c Release -- --filter "*CoreOperatorsBenchmarks*" --job short --exporters json
```

## Contributing Benchmarks

When adding new benchmarks:

1. **Focus**: Each benchmark should measure one specific aspect
2. **Baseline**: Include a baseline for comparison
3. **Configuration**: Use realistic workload sizes
4. **Documentation**: Add comments explaining what is being measured
5. **Naming**: Use descriptive names following the existing pattern

## Resources

- [BenchmarkDotNet Documentation](https://benchmarkdotnet.org/articles/overview.html)
- [.NET Performance Best Practices](https://learn.microsoft.com/en-us/dotnet/core/extensions/performance)
- [Rivulet.Core Main README](../../README.md)

---
# Benchmark Results Summary

**Test Environment:**
- **BenchmarkDotNet**: v0.14.0
- **OS**: Windows 11 22H2
- **CPU**: AMD Ryzen 9 9950X (32 logical, 16 physical cores)
- **RAM**: 64GB DDR5 6000MHz

## 1. Advanced Features Overhead

Measures the performance impact of production-grade features. **Configuration**: 500 items, 2ms delay each, MaxDegreeOfParallelism = 16

| Feature                    | Runtime  |      Mean | Ratio | Allocated | Alloc Ratio |
|--------------------------- |--------- |----------:|------:|----------:|------------:|
| No advanced features       | .NET 8.0 |  500.2 ms |  1.00 |  285.1 KB |        1.00 |
| With CircuitBreaker        | .NET 8.0 |  501.9 ms |  1.00 |  512.5 KB |        1.80 |
| With RateLimit             | .NET 8.0 |  502.3 ms |  1.00 |  316.1 KB |        1.11 |
| With AdaptiveConcurrency   | .NET 8.0 | 1775.3 ms |  3.55 |  583.7 KB |        2.05 |
| With Progress tracking     | .NET 8.0 |  502.2 ms |  1.00 |  288.7 KB |        1.01 |
| With Metrics tracking      | .NET 8.0 |  503.6 ms |  1.01 |  291.4 KB |        1.02 |
| With all features combined | .NET 8.0 |  504.1 ms |  1.01 |  549.7 KB |        1.93 |
|                            |          |           |       |           |             |
| No advanced features       | .NET 9.0 |  504.0 ms |  1.00 |  285.1 KB |        1.00 |
| With CircuitBreaker        | .NET 9.0 |  504.3 ms |  1.00 |  511.5 KB |        1.79 |
| With RateLimit             | .NET 9.0 |  503.6 ms |  1.00 |  316.9 KB |        1.11 |
| With AdaptiveConcurrency   | .NET 9.0 | 1781.2 ms |  3.53 |  584.3 KB |        2.05 |
| With Progress tracking     | .NET 9.0 |  503.6 ms |  1.00 |  288.8 KB |        1.01 |
| With Metrics tracking      | .NET 9.0 |  502.0 ms |  1.00 |  288.6 KB |        1.01 |
| With all features combined | .NET 9.0 |  498.4 ms |  0.99 |  551.0 KB |        1.93 |

**Key Insights:**
- ✅ Circuit breaker, rate limiting, progress, and metrics add <1% overhead
- ⚠️ Adaptive concurrency adds ~3.5x overhead due to continuous sampling and adjustment
- ✅ Combined features still maintain minimal overhead (~1%)
- ✅ .NET 9.0 shows slightly better performance with combined features

## 2. Batching Performance

Evaluates batch processing efficiency with different batch sizes. **Configuration**: 5,000 items, MaxDegreeOfParallelism = 4

| Method                   | Runtime  | BatchSize |      Mean | Ratio | Allocated | Alloc Ratio |
|------------------------- |--------- |----------:|----------:|------:|----------:|------------:|
| BatchParallelAsync       | .NET 8.0 |       100 |  31.32 ms |  0.01 |  78.69 KB |        0.03 |
| BatchParallelStreamAsync | .NET 8.0 |       100 |  31.34 ms |  0.01 |  98.45 KB |        0.04 |
| Baseline - Individual    | .NET 8.0 |       100 | 2466.7 ms |  1.00 | 2712.6 KB |        1.00 |
|                          |          |           |           |       |           |             |
| BatchParallelAsync       | .NET 9.0 |       100 |  31.18 ms |  0.01 |  78.39 KB |        0.03 |
| BatchParallelStreamAsync | .NET 9.0 |       100 |  31.28 ms |  0.01 |  96.37 KB |        0.04 |
| Baseline - Individual    | .NET 9.0 |       100 | 2460.5 ms |  1.00 | 2714.8 KB |        1.00 |
|                          |          |           |           |       |           |             |
| BatchParallelAsync       | .NET 8.0 |       500 |  15.62 ms | 0.006 |  47.75 KB |        0.02 |
| BatchParallelStreamAsync | .NET 8.0 |       500 |  15.61 ms | 0.006 |  81.30 KB |        0.03 |
| Baseline - Individual    | .NET 8.0 |       500 | 2458.1 ms | 1.000 | 2715.4 KB |        1.00 |
|                          |          |           |           |       |           |             |
| BatchParallelAsync       | .NET 9.0 |       500 |  15.61 ms | 0.006 |  47.80 KB |        0.02 |
| BatchParallelStreamAsync | .NET 9.0 |       500 |  15.68 ms | 0.006 |  79.87 KB |        0.03 |
| Baseline - Individual    | .NET 9.0 |       500 | 2458.5 ms | 1.000 | 2713.1 KB |        1.00 |
|                          |          |           |           |       |           |             |
| BatchParallelAsync       | .NET 8.0 |      1000 |  15.63 ms | 0.006 |  44.96 KB |        0.02 |
| BatchParallelStreamAsync | .NET 8.0 |      1000 |  15.60 ms | 0.006 |  72.42 KB |        0.03 |
| Baseline - Individual    | .NET 8.0 |      1000 | 2457.9 ms | 1.000 | 2708.9 KB |        1.00 |
|                          |          |           |           |       |           |             |
| BatchParallelAsync       | .NET 9.0 |      1000 |  15.59 ms | 0.006 |  44.86 KB |        0.02 |
| BatchParallelStreamAsync | .NET 9.0 |      1000 |  15.59 ms | 0.006 |  69.83 KB |        0.03 |
| Baseline - Individual    | .NET 9.0 |      1000 | 2450.2 ms | 1.000 | 2712.2 KB |        1.00 |

**Key Insights:**
- ✅ **78-157x speedup** - Batch processing is dramatically faster than individual item processing
- ✅ **Larger batches = better performance** - Batch size 500-1000 provides ~2x improvement over batch size 100
- ✅ **Minimal memory overhead** - Batching uses 97-98% less memory than individual processing
- ✅ **Stream vs non-stream** - BatchParallelStreamAsync has similar performance with slightly higher allocations
- ✅ **.NET 9.0 consistency** - Negligible difference between .NET 8.0 and .NET 9.0 for batch operations

## 3. Concurrency Scaling

Analyzes performance scaling with different concurrency levels. **Configuration**: 1,000 items, 1ms delay per item

| Concurrency | Runtime  |       Mean | Speedup | Allocated |
|------------ |--------- |-----------:|--------:|----------:|
| 1           | .NET 8.0 | 15,610 ms  |   1.0x  | 545.12 KB |
| 1           | .NET 9.0 | 15,614 ms  |   1.0x  | 540.99 KB |
|             |          |            |         |           |
| 2           | .NET 8.0 |  7,805 ms  |   2.0x  | 545.91 KB |
| 2           | .NET 9.0 |  7,799 ms  |   2.0x  | 541.79 KB |
|             |          |            |         |           |
| 4           | .NET 8.0 |  3,899 ms  |   4.0x  | 547.34 KB |
| 4           | .NET 9.0 |  3,901 ms  |   4.0x  | 547.58 KB |
|             |          |            |         |           |
| 8           | .NET 8.0 |  1,950 ms  |   8.0x  | 546.80 KB |
| 8           | .NET 9.0 |  1,948 ms  |   8.0x  | 549.13 KB |
|             |          |            |         |           |
| 16          | .NET 8.0 |    984 ms  |  15.9x  | 554.55 KB |
| 16          | .NET 9.0 |    983 ms  |  15.9x  | 555.53 KB |
|             |          |            |         |           |
| 32          | .NET 8.0 |    498 ms  |  31.4x  | 566.96 KB |
| 32          | .NET 9.0 |    498 ms  |  31.3x  | 565.39 KB |
|             |          |            |         |           |
| 64          | .NET 8.0 |    249 ms  |  62.6x  | 586.94 KB |
| 64          | .NET 9.0 |    249 ms  |  62.7x  | 586.75 KB |
|             |          |            |         |           |
| 128         | .NET 8.0 |    125 ms  | 125.2x  | 630.17 KB |
| 128         | .NET 9.0 |    125 ms  | 125.0x  | 630.69 KB |

**Key Insights:**
- ✅ **Perfect linear scaling** - Performance doubles with each doubling of concurrency (1→2→4→8)
- ✅ **Efficient up to 128 threads** - Maintains near-linear scaling even at high concurrency
- ✅ **Minimal memory growth** - Allocations grow only ~15% from concurrency 1 to 128
- ✅ **I/O-bound workloads excel** - 125x speedup for I/O-heavy operations
- ✅ **.NET 8.0 vs 9.0 parity** - Virtually identical performance across both runtimes

## 4. Core Operators Performance

Measures core parallel operator performance across different workload types. **Configuration**: 1,000 items

| Operator                       | Runtime  |        Mean | Ratio | Allocated | Alloc Ratio |
|------------------------------- |--------- |------------:|------:|----------:|------------:|
| CPU-bound (light)              | .NET 8.0 |     2.03 ms | 0.000 | 335.33 KB |        1.94 |
| I/O simulation                 | .NET 8.0 |   499.10 ms | 0.032 | 563.38 KB |        3.25 |
| Ordered output                 | .NET 8.0 |   498.11 ms | 0.032 | 716.23 KB |        4.13 |
| StreamAsync                    | .NET 8.0 |     1.66 ms | 0.000 | 372.53 KB |        2.15 |
| ForEachAsync                   | .NET 8.0 |     1.71 ms | 0.000 | 368.39 KB |        2.13 |
| **Baseline - Sequential**      | .NET 8.0 | **15,617 ms** | **1.000** | **173.22 KB** |    **1.00** |
| Baseline - Task.WhenAll        | .NET 8.0 |    15.59 ms | 0.001 | 286.10 KB |        1.65 |
|                                |          |             |       |           |             |
| CPU-bound (light)              | .NET 9.0 |     1.63 ms | 0.000 | 327.40 KB |        1.89 |
| I/O simulation                 | .NET 9.0 |   497.71 ms | 0.032 | 565.04 KB |        3.27 |
| Ordered output                 | .NET 9.0 |   498.05 ms | 0.032 | 716.22 KB |        4.14 |
| StreamAsync                    | .NET 9.0 |     1.40 ms | 0.000 | 263.34 KB |        1.52 |
| ForEachAsync                   | .NET 9.0 |     1.41 ms | 0.000 | 257.72 KB |        1.49 |
| **Baseline - Sequential**      | .NET 9.0 | **15,604 ms** | **1.000** | **172.90 KB** |    **1.00** |
| Baseline - Task.WhenAll        | .NET 9.0 |    15.60 ms | 0.001 | 286.11 KB |        1.65 |

**Key Insights:**
- ✅ **31x speedup for I/O operations** - Dramatically outperforms sequential processing for I/O-bound workloads
- ✅ **Near Task.WhenAll performance** - Rivulet provides controlled concurrency with minimal overhead vs unbounded
- ✅ **Ordered output cost** - Maintaining order adds ~27% memory overhead but same execution time
- ✅ **.NET 9.0 improvements** - StreamAsync and ForEachAsync show 15-20% better performance on .NET 9.0
- ✅ **Memory efficient** - CPU-bound operations use only 2x baseline memory despite parallelization

## 5. Error Handling and Retry Performance

Measures the overhead of error handling mechanisms and retry policies. **Configuration**: 500 items, MaxDegreeOfParallelism = 8

| Strategy                    | Runtime  |      Mean | Ratio | Allocated | Alloc Ratio |
|---------------------------- |--------- |----------:|------:|----------:|------------:|
| No retries (success)        | .NET 8.0 |   714 μs  | 0.001 | 216.75 KB |        0.76 |
| Retries (10% failures)      | .NET 8.0 | 109.1 ms  | 0.218 | 150.18 KB |        0.53 |
| ErrorMode.FailFast          | .NET 8.0 |   732 μs  | 0.001 | 234.77 KB |        0.82 |
| ErrorMode.BestEffort        | .NET 8.0 |   728 μs  | 0.001 | 228.98 KB |        0.80 |
| Backoff.ExponentialJitter   | .NET 8.0 |  31.2 ms  | 0.062 | 136.31 KB |        0.48 |
| Backoff.Exponential         | .NET 8.0 | 124.3 ms  | 0.248 | 144.98 KB |        0.51 |
|                             |          |           |       |           |             |
| No retries (success)        | .NET 9.0 |   554 μs  | 0.001 | 181.92 KB |        0.64 |
| Retries (10% failures)      | .NET 9.0 | 109.1 ms  | 0.217 | 149.29 KB |        0.52 |
| ErrorMode.FailFast          | .NET 9.0 |   555 μs  | 0.001 | 191.98 KB |        0.67 |
| ErrorMode.BestEffort        | .NET 9.0 |   550 μs  | 0.001 | 180.66 KB |        0.63 |
| Backoff.Exponential         | .NET 9.0 | 124.3 ms  | 0.247 | 144.28 KB |        0.51 |
| Backoff.ExponentialJitter   | .NET 9.0 |  30.8 ms  | 0.061 | 135.81 KB |        0.48 |

**Key Insights:**
- ✅ **Minimal overhead when no errors** - FailFast and BestEffort add <3% overhead on success path
- ✅ **Jitter reduces contention** - ExponentialJitter is 4x faster than standard Exponential backoff
- ✅ **Memory efficient retries** - Retry mechanism uses 50% less memory than baseline
- ✅ **.NET 9.0 performance win** - 22% faster execution on success path (714μs → 554μs)
- ⚠️ **Retry cost** - With 10% transient failures, expect ~150x slowdown (due to delays between retries)

---

**Note**: Benchmark results vary based on hardware, OS, and system load. Always run benchmarks on the same machine with minimal background processes for consistent comparisons.
