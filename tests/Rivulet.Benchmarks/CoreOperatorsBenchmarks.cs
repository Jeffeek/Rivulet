using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Rivulet.Core;

namespace Rivulet.Benchmarks;

[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MarkdownExporter]
public class CoreOperatorsBenchmarks
{
    private const int ItemCount = 1000;
    private IEnumerable<int> _source = null!;

    [GlobalSetup]
    public void Setup()
    {
        _source = Enumerable.Range(1, ItemCount);
    }

    [Benchmark(Description = "SelectParallelAsync - CPU-bound (light)")]
    public async Task<List<int>> SelectParallel_CpuBound_Light()
    {
        return await _source.SelectParallelAsync(
            (x, _) => new ValueTask<int>(x * 2),
            new ParallelOptionsRivulet { MaxDegreeOfParallelism = 8 });
    }

    [Benchmark(Description = "SelectParallelAsync - I/O simulation")]
    public async Task<List<int>> SelectParallel_IoSimulation()
    {
        return await _source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x * 2;
            },
            new ParallelOptionsRivulet { MaxDegreeOfParallelism = 32 });
    }

    [Benchmark(Description = "SelectParallelAsync - Ordered output")]
    public async Task<List<int>> SelectParallel_OrderedOutput()
    {
        return await _source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x * 2;
            },
            new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 32,
                OrderedOutput = true
            });
    }

    [Benchmark(Description = "SelectParallelStreamAsync - Streaming results")]
    public async Task<int> SelectParallelStream_Streaming()
    {
        return await _source.ToAsyncEnumerable().SelectParallelStreamAsync((x, _) => new ValueTask<int>(x * 2), new ParallelOptionsRivulet { MaxDegreeOfParallelism = 8 }).CountAsync();
    }

    [Benchmark(Description = "ForEachParallelAsync - Side effects")]
    public async Task ForEachParallel_SideEffects()
    {
        var sum = 0;
        await _source.ToAsyncEnumerable().ForEachParallelAsync(
            (x, _) =>
            {
                Interlocked.Add(ref sum, x);
                return ValueTask.CompletedTask;
            },
            new ParallelOptionsRivulet { MaxDegreeOfParallelism = 8 });
    }

    [Benchmark(Baseline = true, Description = "Baseline - Sequential processing")]
    public async Task<List<int>> Baseline_Sequential()
    {
        var results = new List<int>();
        foreach (var item in _source)
        {
            await Task.Delay(1);
            results.Add(item * 2);
        }
        return results;
    }

    [Benchmark(Description = "Baseline - Task.WhenAll (unbounded)")]
    public async Task<int[]> Baseline_TaskWhenAll()
    {
        var tasks = _source.Select(async x =>
        {
            await Task.Delay(1);
            return x * 2;
        });
        return await Task.WhenAll(tasks);
    }
}
