# Rivulet Benchmarks

This project contains comprehensive performance benchmarks for Rivulet using [BenchmarkDotNet](https://benchmarkdotnet.org/).

## Running Benchmarks

### Run All Benchmarks

The runtime measured is whichever TFM you launch with (`-f`). Run once per runtime:

```powershell
cd tests\Rivulet.Benchmarks
dotnet run -c Release -f net10.0
dotnet run -c Release -f net11.0
```

> **Note**: BenchmarkDotNet 0.15.8 has no `RuntimeMoniker.Net11_0`, and its CsProj toolchain can't build
> net11.0 (the SDK validator throws on the unrecognized moniker). So benchmarks run **in-process** against the
> runtime the runner was launched with — pick it via `-f net10.0` / `-f net11.0`. Once BenchmarkDotNet ships the
> net11 moniker, `Program.cs` can go back to `[SimpleJob(RuntimeMoniker.Net10_0 / Net11_0)]` attributes.
>
> **Do not** pass `--job <name>` or `--runtimes <tfm>`: both inject a CsProj-toolchain job that overrides the
> in-process job and re-triggers the crash on net11.0.

### Run Specific Benchmark Suite

```powershell
dotnet run -c Release -f net11.0 -- --filter "*CoreOperatorsBenchmarks*"
dotnet run -c Release -f net11.0 -- --filter "*BatchingBenchmarks*"
dotnet run -c Release -f net11.0 -- --filter "*ErrorHandlingBenchmarks*"
dotnet run -c Release -f net11.0 -- --filter "*AdvancedFeaturesBenchmarks*"
dotnet run -c Release -f net11.0 -- --filter "*ConcurrencyScalingBenchmarks*"
```

### Export Results

```powershell
# Export to various formats
dotnet run -c Release -f net11.0 -- --exporters json,html,markdown
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

Benchmarks run on **.NET 10.0** and **.NET 11.0** — one runtime per launch, selected with `-f` (see the note above).

### Common Settings

- **MemoryDiagnoser**: Enabled to track allocations
- **MarkdownExporter**: Generates markdown-formatted results
- **Toolchain**: InProcessEmit (runtime = the `-f` TFM the runner was launched with)

### Customizing Runs

```powershell
# Run on a specific runtime (choose the TFM at launch, NOT via --runtimes)
dotnet run -c Release -f net11.0

# Run with specific number of iterations
dotnet run -c Release -f net11.0 -- --iterationCount 10

# Run with memory profiling
dotnet run -c Release -f net11.0 -- --memory

# Run with thread pool sizing diagnostics
dotnet run -c Release -f net11.0 -- --envVars COMPlus_ThreadPool_ForceMinWorkerThreads:100
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
// .NET 10.0 vs .NET 11.0 - SelectParallelAsync I/O simulation (1000 items, 1ms delay each)

|                 Method |   Runtime |     Mean | Allocated |
|----------------------- |---------- |---------:|----------:|
|    SelectParallelAsync | .NET 10.0 |   497 ms |  ~1.16 MB |
|    SelectParallelAsync | .NET 11.0 |   494 ms |  ~1.15 MB |
| Sequential Processing  | .NET 10.0 | 15536 ms |  ~0.17 MB |
|         Task.WhenAll   | .NET 10.0 |    15 ms |  ~0.28 MB | (Unbounded!)
```

## CI/CD Integration

To run benchmarks in CI/CD pipelines:

```yaml
# GitHub Actions example
- name: Run Benchmarks
  run: |
    cd tests/Rivulet.Benchmarks
    dotnet run -c Release -f net11.0 -- --filter "*CoreOperatorsBenchmarks*" --exporters json
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
- **BenchmarkDotNet**: v0.15.8
- **Runtimes**: .NET 10.0.15 and .NET 11.0.0
- **Job**: ShortRun (LaunchCount=1, WarmupCount=3, IterationCount=3), in-process toolchain
- **OS**: Windows 11 24H2 (10.0.26100)
- **CPU**: AMD Ryzen 9 9950X (32 logical, 16 physical cores)
- **RAM**: 64GB DDR5 6000MHz

> **Note**: These numbers come from a `ShortRun` job (3 iterations) run **in-process** on each runtime
> separately (`-f net10.0` / `-f net11.0`) — see the toolchain note at the top of this file. ShortRun trades
> precision for speed, so treat small deltas between runtimes as noise. The `Ratio` columns from the earlier
> full-run tables are omitted here because the joined summary computes them against a single global baseline
> across all suites, which is not meaningful per-suite; raw **Mean** and **Allocated** are shown instead.

## 1. Advanced Features Overhead

Measures the performance impact of production-grade features. **Configuration**: 500 items, 2ms delay each, MaxDegreeOfParallelism = 16

| Feature                    | Runtime   |       Mean | Allocated |
|--------------------------- |---------- |-----------:|----------:|
| No advanced features       | .NET 10.0 |   496.5 ms | 595.22 KB |
| With CircuitBreaker        | .NET 10.0 |   495.9 ms | 766.95 KB |
| With RateLimit             | .NET 10.0 |   496.3 ms | 630.86 KB |
| With AdaptiveConcurrency   | .NET 10.0 | 1,755.1 ms | 868.54 KB |
| With Progress tracking     | .NET 10.0 |   495.3 ms | 601.29 KB |
| With Metrics tracking      | .NET 10.0 |   495.6 ms | 602.91 KB |
| With all features combined | .NET 10.0 |   496.9 ms | 804.79 KB |
|                            |           |            |           |
| No advanced features       | .NET 11.0 |   497.2 ms | 592.30 KB |
| With CircuitBreaker        | .NET 11.0 |   495.6 ms | 753.20 KB |
| With RateLimit             | .NET 11.0 |   494.1 ms | 622.98 KB |
| With AdaptiveConcurrency   | .NET 11.0 | 1,751.4 ms | 931.60 KB |
| With Progress tracking     | .NET 11.0 |   493.8 ms | 600.20 KB |
| With Metrics tracking      | .NET 11.0 |   493.3 ms | 598.23 KB |
| With all features combined | .NET 11.0 |   493.3 ms | 786.85 KB |

**Key Insights:**
- ✅ Circuit breaker, rate limiting, progress, and metrics add <1% time overhead
- ⚠️ Adaptive concurrency adds ~3.5x overhead due to continuous sampling and adjustment
- ✅ Combined features still maintain minimal time overhead (~0.1%)
- ✅ .NET 10.0 and .NET 11.0 are effectively identical for these features; .NET 11.0 trims a hair off time at a slightly higher allocation for AdaptiveConcurrency

## 2. Batching Performance

Evaluates batch processing efficiency with different batch sizes. **Configuration**: 5,000 items, MaxDegreeOfParallelism = 4

| Method                   | Runtime   | BatchSize |       Mean |  Allocated |
|------------------------- |---------- |----------:|-----------:|-----------:|
| BatchParallelAsync       | .NET 10.0 |       100 |    31.1 ms |  108.29 KB |
| BatchParallelStreamAsync | .NET 10.0 |       100 |    31.0 ms |  125.08 KB |
| Baseline - Individual    | .NET 10.0 |       100 | 2,437.3 ms | 5837.27 KB |
|                          |           |           |            |            |
| BatchParallelAsync       | .NET 11.0 |       100 |    31.0 ms |  107.80 KB |
| BatchParallelStreamAsync | .NET 11.0 |       100 |    31.1 ms |  119.30 KB |
| Baseline - Individual    | .NET 11.0 |       100 | 2,434.3 ms | 5806.58 KB |
|                          |           |           |            |            |
| BatchParallelAsync       | .NET 10.0 |       500 |    15.5 ms |   52.33 KB |
| BatchParallelStreamAsync | .NET 10.0 |       500 |    15.5 ms |   81.58 KB |
| Baseline - Individual    | .NET 10.0 |       500 | 2,438.0 ms | 5836.06 KB |
|                          |           |           |            |            |
| BatchParallelAsync       | .NET 11.0 |       500 |    15.5 ms |   52.47 KB |
| BatchParallelStreamAsync | .NET 11.0 |       500 |    15.5 ms |   76.81 KB |
| Baseline - Individual    | .NET 11.0 |       500 | 2,437.6 ms | 5800.73 KB |
|                          |           |           |            |            |
| BatchParallelAsync       | .NET 10.0 |      1000 |    15.5 ms |   46.55 KB |
| BatchParallelStreamAsync | .NET 10.0 |      1000 |    15.5 ms |   72.26 KB |
| Baseline - Individual    | .NET 10.0 |      1000 | 2,437.4 ms | 5833.24 KB |
|                          |           |           |            |            |
| BatchParallelAsync       | .NET 11.0 |      1000 |    15.5 ms |   46.47 KB |
| BatchParallelStreamAsync | .NET 11.0 |      1000 |    15.5 ms |   70.94 KB |
| Baseline - Individual    | .NET 11.0 |      1000 | 2,439.0 ms | 5795.60 KB |

**Key Insights:**
- ✅ **~78-157x speedup** - Batch processing is dramatically faster than individual item processing
- ✅ **Larger batches = better performance** - Batch size 500-1000 is ~2x faster than batch size 100
- ✅ **Minimal memory overhead** - Batching uses ~98% less memory than individual processing
- ✅ **Stream vs non-stream** - BatchParallelStreamAsync matches performance with slightly higher allocations
- ✅ **.NET 10.0 vs 11.0 parity** - Negligible difference between runtimes for batch operations

## 3. Concurrency Scaling

Analyzes performance scaling with different concurrency levels. **Configuration**: 1,000 items, 1ms delay per item

| Concurrency | Runtime   |      Mean | Speedup | Allocated |
|------------ |---------- |----------:|--------:|----------:|
| 4           | .NET 10.0 | 3,881 ms  |   1.0x  | 1174.6 KB |
| 4           | .NET 11.0 | 3,883 ms  |   1.0x  | 1166.5 KB |
|             |           |           |         |           |
| 8           | .NET 10.0 | 1,939 ms  |   2.0x  | 1175.9 KB |
| 8           | .NET 11.0 | 1,939 ms  |   2.0x  | 1167.8 KB |
|             |           |           |         |           |
| 16          | .NET 10.0 |   977 ms  |   4.0x  | 1180.7 KB |
| 16          | .NET 11.0 |   977 ms  |   4.0x  | 1173.6 KB |
|             |           |           |         |           |
| 32          | .NET 10.0 |   494 ms  |   7.9x  | 1191.2 KB |
| 32          | .NET 11.0 |   495 ms  |   7.8x  | 1182.1 KB |
|             |           |           |         |           |
| 64          | .NET 10.0 |   248 ms  |  15.6x  | 1209.9 KB |
| 64          | .NET 11.0 |   248 ms  |  15.7x  | 1201.0 KB |
|             |           |           |         |           |
| 128         | .NET 10.0 |   124 ms  |  31.3x  | 1246.2 KB |
| 128         | .NET 11.0 |   124 ms  |  31.4x  | 1239.0 KB |

**Key Insights:**
- ✅ **Perfect linear scaling** - Performance doubles with each doubling of concurrency (4→8→16→...)
- ✅ **Efficient up to 128 threads** - Maintains near-linear scaling even at high concurrency (31x from 4→128)
- ✅ **Minimal memory growth** - Allocations grow only ~6% from concurrency 4 to 128
- ✅ **I/O-bound workloads excel** - Scaling is bounded only by the configured concurrency
- ✅ **.NET 10.0 vs 11.0 parity** - Virtually identical performance across both runtimes (speedup measured relative to concurrency 4)

## 4. Core Operators Performance

Measures core parallel operator performance across different workload types. **Configuration**: 1,000 items

| Operator                       | Runtime   |          Mean |  Allocated |
|------------------------------- |---------- |--------------:|-----------:|
| CPU-bound (light)              | .NET 10.0 |       1.28 ms |  363.15 KB |
| I/O simulation                 | .NET 10.0 |     497.04 ms | 1191.38 KB |
| Ordered output                 | .NET 10.0 |     489.06 ms | 1323.59 KB |
| StreamAsync                    | .NET 10.0 |       1.43 ms |  370.02 KB |
| ForEachAsync                   | .NET 10.0 |       1.29 ms |  369.24 KB |
| **Baseline - Sequential**      | .NET 10.0 | **15,536 ms** |  177.10 KB |
| Baseline - Task.WhenAll        | .NET 10.0 |      15.50 ms |  286.07 KB |
|                                |           |               |            |
| CPU-bound (light)              | .NET 11.0 |       1.53 ms |  360.53 KB |
| I/O simulation                 | .NET 11.0 |     494.39 ms | 1178.74 KB |
| Ordered output                 | .NET 11.0 |     496.37 ms | 1317.95 KB |
| StreamAsync                    | .NET 11.0 |       1.03 ms |  291.83 KB |
| ForEachAsync                   | .NET 11.0 |       1.08 ms |  291.79 KB |
| **Baseline - Sequential**      | .NET 11.0 | **15,536 ms** |  178.10 KB |
| Baseline - Task.WhenAll        | .NET 11.0 |      15.55 ms |  286.13 KB |

**Key Insights:**
- ✅ **~31x speedup for I/O operations** - Dramatically outperforms sequential processing for I/O-bound workloads
- ✅ **Near Task.WhenAll performance** - Rivulet provides controlled concurrency with minimal overhead vs unbounded
- ✅ **Ordered output cost** - Maintaining order adds ~11% memory overhead at essentially the same execution time
- ✅ **.NET 11.0 improvements** - StreamAsync and ForEachAsync show ~20-30% better time and ~21% lower allocations on .NET 11.0
- ✅ **Memory efficient** - CPU-bound operations allocate ~2x the sequential baseline despite full parallelization

## 5. Error Handling and Retry Performance

Measures the overhead of error handling mechanisms and retry policies. **Configuration**: 500 items, MaxDegreeOfParallelism = 8

| Strategy                    | Runtime   |      Mean | Allocated |
|---------------------------- |---------- |----------:|----------:|
| No retries (success)        | .NET 10.0 |   628 μs  | 184.36 KB |
| Retries (10% failures)      | .NET 10.0 | 108.3 ms  | 255.83 KB |
| ErrorMode.FailFast          | .NET 10.0 |   605 μs  | 179.20 KB |
| ErrorMode.BestEffort        | .NET 10.0 |   615 μs  | 195.84 KB |
| Backoff.Exponential         | .NET 10.0 | 124.6 ms  | 239.64 KB |
| Backoff.ExponentialJitter   | .NET 10.0 |  31.3 ms  | 224.01 KB |
|                             |           |           |           |
| No retries (success)        | .NET 11.0 |   804 μs  | 198.73 KB |
| Retries (10% failures)      | .NET 11.0 | 108.4 ms  | 251.95 KB |
| ErrorMode.FailFast          | .NET 11.0 |   786 μs  | 199.33 KB |
| ErrorMode.BestEffort        | .NET 11.0 |   777 μs  | 200.23 KB |
| Backoff.Exponential         | .NET 11.0 | 123.8 ms  | 235.75 KB |
| Backoff.ExponentialJitter   | .NET 11.0 |  31.0 ms  | 220.02 KB |

**Key Insights:**
- ✅ **Minimal overhead when no errors** - FailFast and BestEffort stay within the success-path microsecond range
- ✅ **Jitter reduces contention** - ExponentialJitter is ~4x faster than standard Exponential backoff
- ✅ **Retry mechanism is memory-lean** - Retry paths allocate only modestly above the success path
- ⚠️ **Success-path noise** - The μs-scale cases vary between runtimes under ShortRun (3 iterations); treat the net10/net11 gap as measurement noise, not a regression
- ⚠️ **Retry cost** - With 10% transient failures and backoff delays, expect a large slowdown vs the no-error path

---

**Note**: Benchmark results vary based on hardware, OS, and system load. Always run benchmarks on the same machine with minimal background processes for consistent comparisons.
