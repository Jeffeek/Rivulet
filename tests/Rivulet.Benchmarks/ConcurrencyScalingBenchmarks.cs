using BenchmarkDotNet.Attributes;
using Rivulet.Core;

namespace Rivulet.Benchmarks;

[
    MemoryDiagnoser,
    MarkdownExporter
]
// ReSharper disable once MemberCanBeFileLocal
// ReSharper disable once ClassCanBeSealed.Global
public class ConcurrencyScalingBenchmarks
{
    private const int ItemCount = 1000;
    private IEnumerable<int> _source = null!;

    // ReSharper disable once UnassignedField.Global
    [Params(
        4,
        8,
        16,
        32,
        64,
        128)]
    public int MaxDegreeOfParallelism;

    [GlobalSetup]
    public void Setup() => _source = Enumerable.Range(1, ItemCount);

    [Benchmark(Description = "MaxDegreeOfParallelism")]
    public Task<List<int>> Parallelism() =>
        _source.SelectParallelAsync(static async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x * 2;
            },
            new() { MaxDegreeOfParallelism = MaxDegreeOfParallelism });
}
