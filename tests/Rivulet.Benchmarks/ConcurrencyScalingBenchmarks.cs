using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Rivulet.Core;

namespace Rivulet.Benchmarks;

[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MarkdownExporter]
public class ConcurrencyScalingBenchmarks
{
    private const int ItemCount = 1000;
    private IEnumerable<int> _source = null!;

    // ReSharper disable once UnassignedField.Global
    [Params([1, 2, 4, 8, 16, 32, 64, 128])] public int MaxDegreeOfParallelism;

    [GlobalSetup]
    public void Setup()
    {
        _source = Enumerable.Range(1, ItemCount);
    }

    [Benchmark(Description = "MaxDegreeOfParallelism")]
    public async Task<List<int>> Parallelism()
    {
        return await _source.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x * 2;
            },
            new ParallelOptionsRivulet { MaxDegreeOfParallelism = MaxDegreeOfParallelism });
    }
}
