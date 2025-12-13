using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Rivulet.Core;

namespace Rivulet.Benchmarks;

[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MarkdownExporter]
public class AdvancedFeaturesBenchmarks
{
    private const int ItemCount = 500;
    private IEnumerable<int> _source = null!;

    [GlobalSetup]
    public void Setup() => _source = Enumerable.Range(1, ItemCount);

    [Benchmark(Baseline = true, Description = "No advanced features")]
    public async Task<List<int>> NoAdvancedFeatures()
    {
        return await _source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(2, ct);
                return x * 2;
            },
            new() { MaxDegreeOfParallelism = 16 });
    }

    [Benchmark(Description = "With CircuitBreaker")]
    public async Task<List<int>> WithCircuitBreaker()
    {
        return await _source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(2, ct);
                return x * 2;
            },
            new()
            {
                MaxDegreeOfParallelism = 16,
                CircuitBreaker = new()
                {
                    FailureThreshold = 5,
                    SuccessThreshold = 2,
                    OpenTimeout = TimeSpan.FromSeconds(10)
                }
            });
    }

    [Benchmark(Description = "With RateLimit")]
    public async Task<List<int>> WithRateLimit()
    {
        return await _source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(2, ct);
                return x * 2;
            },
            new()
            {
                MaxDegreeOfParallelism = 16,
                RateLimit = new()
                {
                    TokensPerSecond = 1000,
                    BurstCapacity = 100
                }
            });
    }

    [Benchmark(Description = "With AdaptiveConcurrency")]
    public async Task<List<int>> WithAdaptiveConcurrency()
    {
        return await _source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(2, ct);
                return x * 2;
            },
            new()
            {
                MaxDegreeOfParallelism = 32,
                AdaptiveConcurrency = new()
                {
                    MinConcurrency = 4,
                    MaxConcurrency = 32,
                    MinSuccessRate = 0.95
                }
            });
    }

    [Benchmark(Description = "With Progress tracking")]
    public async Task<List<int>> WithProgressTracking()
    {
        return await _source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(2, ct);
                return x * 2;
            },
            new()
            {
                MaxDegreeOfParallelism = 16,
                Progress = new()
                {
                    ReportInterval = TimeSpan.FromMilliseconds(100),
                    OnProgress = _ => ValueTask.CompletedTask
                }
            });
    }

    [Benchmark(Description = "With Metrics tracking")]
    public async Task<List<int>> WithMetricsTracking()
    {
        return await _source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(2, ct);
                return x * 2;
            },
            new()
            {
                MaxDegreeOfParallelism = 16,
                Metrics = new()
                {
                    SampleInterval = TimeSpan.FromMilliseconds(100),
                    OnMetricsSample = _ => ValueTask.CompletedTask
                }
            });
    }

    [Benchmark(Description = "With all features combined")]
    public async Task<List<int>> WithAllFeatures()
    {
        return await _source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(2, ct);
                return x * 2;
            },
            new()
            {
                MaxDegreeOfParallelism = 16,
                CircuitBreaker = new()
                {
                    FailureThreshold = 5,
                    SuccessThreshold = 2,
                    OpenTimeout = TimeSpan.FromSeconds(10)
                },
                RateLimit = new()
                {
                    TokensPerSecond = 1000,
                    BurstCapacity = 100
                },
                Progress = new()
                {
                    ReportInterval = TimeSpan.FromMilliseconds(100),
                    OnProgress = _ => ValueTask.CompletedTask
                },
                Metrics = new()
                {
                    SampleInterval = TimeSpan.FromMilliseconds(100),
                    OnMetricsSample = _ => ValueTask.CompletedTask
                }
            });
    }
}
