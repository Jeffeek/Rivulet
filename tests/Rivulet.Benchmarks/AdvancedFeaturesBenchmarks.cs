using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Rivulet.Core;

namespace Rivulet.Benchmarks;

[
    SimpleJob(RuntimeMoniker.Net80),
    SimpleJob(RuntimeMoniker.Net90),
    MemoryDiagnoser,
    MarkdownExporter
]
// ReSharper disable once ClassCanBeSealed.Global
// ReSharper disable once MemberCanBeFileLocal
public class AdvancedFeaturesBenchmarks
{
    private const int ItemCount = 500;
    private IEnumerable<int> _source = null!;

    [GlobalSetup]
    public void Setup() => _source = Enumerable.Range(1, ItemCount);

    [Benchmark(Baseline = true, Description = "No advanced features")]
    public Task<List<int>> NoAdvancedFeatures() =>
        _source.SelectParallelAsync(static async (x, ct) =>
            {
                await Task.Delay(2, ct);
                return x * 2;
            },
            new() { MaxDegreeOfParallelism = 16 });

    [Benchmark(Description = "With CircuitBreaker")]
    public Task<List<int>> WithCircuitBreaker() =>
        _source.SelectParallelAsync(static async (x, ct) =>
            {
                await Task.Delay(2, ct);
                return x * 2;
            },
            new()
            {
                MaxDegreeOfParallelism = 16,
                CircuitBreaker = new() { FailureThreshold = 5, SuccessThreshold = 2, OpenTimeout = TimeSpan.FromSeconds(10) }
            });

    [Benchmark(Description = "With RateLimit")]
    public Task<List<int>> WithRateLimit() =>
        _source.SelectParallelAsync(static async (x, ct) =>
            {
                await Task.Delay(2, ct);
                return x * 2;
            },
            new() { MaxDegreeOfParallelism = 16, RateLimit = new() { TokensPerSecond = 1000, BurstCapacity = 100 } });

    [Benchmark(Description = "With AdaptiveConcurrency")]
    public Task<List<int>> WithAdaptiveConcurrency() =>
        _source.SelectParallelAsync(static async (x, ct) =>
            {
                await Task.Delay(2, ct);
                return x * 2;
            },
            new()
            {
                MaxDegreeOfParallelism = 32,
                AdaptiveConcurrency = new() { MinConcurrency = 4, MaxConcurrency = 32, MinSuccessRate = 0.95 }
            });

    [Benchmark(Description = "With Progress tracking")]
    public Task<List<int>> WithProgressTracking() =>
        _source.SelectParallelAsync(static async (x, ct) =>
            {
                await Task.Delay(2, ct);
                return x * 2;
            },
            new()
            {
                MaxDegreeOfParallelism = 16,
                Progress = new() { ReportInterval = TimeSpan.FromMilliseconds(100), OnProgress = static _ => ValueTask.CompletedTask }
            });

    [Benchmark(Description = "With Metrics tracking")]
    public Task<List<int>> WithMetricsTracking() =>
        _source.SelectParallelAsync(static async (x, ct) =>
            {
                await Task.Delay(2, ct);
                return x * 2;
            },
            new()
            {
                MaxDegreeOfParallelism = 16,
                Metrics = new() { SampleInterval = TimeSpan.FromMilliseconds(100), OnMetricsSample = static _ => ValueTask.CompletedTask }
            });

    [Benchmark(Description = "With all features combined")]
    public Task<List<int>> WithAllFeatures() =>
        _source.SelectParallelAsync(static async (x, ct) =>
            {
                await Task.Delay(2, ct);
                return x * 2;
            },
            new()
            {
                MaxDegreeOfParallelism = 16,
                CircuitBreaker = new() { FailureThreshold = 5, SuccessThreshold = 2, OpenTimeout = TimeSpan.FromSeconds(10) },
                RateLimit = new() { TokensPerSecond = 1000, BurstCapacity = 100 },
                Progress = new() { ReportInterval = TimeSpan.FromMilliseconds(100), OnProgress = static _ => ValueTask.CompletedTask },
                Metrics = new() { SampleInterval = TimeSpan.FromMilliseconds(100), OnMetricsSample = static _ => ValueTask.CompletedTask }
            });
}