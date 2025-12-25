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
public class BatchingBenchmarks
{
    private const int ItemCount = 5000;
    private IEnumerable<int> _source = null!;

    // ReSharper disable once UnassignedField.Global
    [Params(100, 500, 1000)]
    public int BatchSize;

    [GlobalSetup]
    public void Setup() => _source = Enumerable.Range(1, ItemCount);

    [Benchmark(Description = "BatchParallelAsync")]
    public Task<List<int>> BatchParallel() =>
        _source.BatchParallelAsync(
            BatchSize,
            static async (batch, ct) =>
            {
                await Task.Delay(5, ct);
                return batch.Count;
            });

    [Benchmark(Description = "BatchParallelStreamAsync")]
    public async Task<int> BatchParallelStream() =>
        await _source.ToAsyncEnumerable()
            .BatchParallelStreamAsync(
                BatchSize,
                static async (batch, ct) =>
                {
                    await Task.Delay(5, ct);
                    return batch.Count;
                })
            .SumAsync();

    [Benchmark(Baseline = true, Description = "Baseline - Individual items")]
    public Task<List<int>> Baseline_IndividualItems() =>
        _source.SelectParallelAsync(static async (_, ct) =>
        {
            await Task.Delay(5, ct);
            return 1;
        });
}
