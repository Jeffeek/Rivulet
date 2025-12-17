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
public class CoreOperatorsBenchmarks
{
    private const int ItemCount = 1000;
    private IEnumerable<int> _source = null!;

    [GlobalSetup]
    public void Setup() => _source = Enumerable.Range(1, ItemCount);

    [Benchmark(Description = "SelectParallelAsync - CPU-bound (light)")]
    public Task<List<int>> SelectParallel_CpuBound_Light() =>
        _source.SelectParallelAsync(static (x, _) => new ValueTask<int>(x * 2),
            new() { MaxDegreeOfParallelism = 8 });

    [Benchmark(Description = "SelectParallelAsync - I/O simulation")]
    public Task<List<int>> SelectParallel_IoSimulation() =>
        _source.SelectParallelAsync(static async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x * 2;
            },
            new() { MaxDegreeOfParallelism = 32 });

    [Benchmark(Description = "SelectParallelAsync - Ordered output")]
    public Task<List<int>> SelectParallel_OrderedOutput() =>
        _source.SelectParallelAsync(static async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x * 2;
            },
            new() { MaxDegreeOfParallelism = 32, OrderedOutput = true });

    [Benchmark(Description = "SelectParallelStreamAsync - Streaming results")]
    public async Task<int> SelectParallelStream_Streaming() =>
        await _source
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(static (x, _) => new ValueTask<int>(x * 2), new() { MaxDegreeOfParallelism = 8 })
            .CountAsync();

    [Benchmark(Description = "ForEachParallelAsync - Side effects")]
    public Task ForEachParallel_SideEffects()
    {
        var sum = 0;
        return _source.ToAsyncEnumerable()
            .ForEachParallelAsync(
                (x, _) =>
                {
                    Interlocked.Add(ref sum, x);
                    return ValueTask.CompletedTask;
                },
                new() { MaxDegreeOfParallelism = 8 });
    }

    [Benchmark(Baseline = true, Description = "Baseline - Sequential processing")]
    public async Task<List<int>> Baseline_Sequential()
    {
        var results = new List<int>();
        foreach (var item in _source)
        {
            await Task.Delay(1, CancellationToken.None);
            results.Add(item * 2);
        }

        return results;
    }

    [Benchmark(Description = "Baseline - Task.WhenAll (unbounded)")]
    public Task<int[]> Baseline_TaskWhenAll()
    {
        var tasks = _source.Select(static async x =>
        {
            await Task.Delay(1, CancellationToken.None);
            return x * 2;
        });
        return Task.WhenAll(tasks);
    }
}