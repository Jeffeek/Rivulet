using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Rivulet.Core;
using Rivulet.Core.Observability;

namespace Rivulet.Benchmarks;

[
    SimpleJob(RuntimeMoniker.Net80),
    SimpleJob(RuntimeMoniker.Net90),
    MemoryDiagnoser,
    MarkdownExporter
]
// ReSharper disable once ClassCanBeSealed.Global
// ReSharper disable once MemberCanBeFileLocal
public class FileIoBenchmarks
{
    private const int ItemCount = 200;
    private const int MaxParallelism = 32;

    private string _tempDir = null!;
    private string[] _filePaths = null!;

    [GlobalSetup]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"rivulet-bench-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _filePaths = Enumerable.Range(1, ItemCount)
            .Select(i => Path.Combine(_tempDir, $"item-{i}.json"))
            .ToArray();

        // Each file holds a small JSON payload (~80 bytes) — realistic for config or record files
        const string content = """{"id":0,"name":"Sample","value":42,"tags":["alpha","beta","gamma"]}""";
        foreach (var path in _filePaths)
            File.WriteAllText(path, content);
    }

    [GlobalCleanup]
    public void Cleanup() => Directory.Delete(_tempDir, recursive: true);

    [Benchmark(Baseline = true, Description = "Baseline - Sequential reads")]
    public async Task<long> Baseline_Sequential()
    {
        long totalChars = 0;
        foreach (var path in _filePaths)
        {
            var content = await File.ReadAllTextAsync(path, CancellationToken.None);
            totalChars += content.Length;
        }

        return totalChars;
    }

    [Benchmark(Description = "Baseline - Task.WhenAll (unbounded)")]
    public async Task<long> Baseline_TaskWhenAll()
    {
        var tasks = _filePaths.Select(static path => File.ReadAllTextAsync(path, CancellationToken.None));
        var results = await Task.WhenAll(tasks);
        return results.Sum(static r => (long)r.Length);
    }

    [Benchmark(Description = "Baseline - Parallel.ForEachAsync")]
    public async Task<long> Baseline_ParallelForEachAsync()
    {
        long totalChars = 0;
        await Parallel.ForEachAsync(
            _filePaths,
            new ParallelOptions { MaxDegreeOfParallelism = MaxParallelism },
            async (path, ct) =>
            {
                var content = await File.ReadAllTextAsync(path, ct);
                Interlocked.Add(ref totalChars, content.Length);
            });

        return totalChars;
    }

    [Benchmark(Description = "SelectParallelAsync - Bounded reads")]
    public async Task<long> SelectParallel_BoundedReads()
    {
        var results = await _filePaths.SelectParallelAsync(
            static (path, ct) => new ValueTask<string>(File.ReadAllTextAsync(path, ct)),
            new() { MaxDegreeOfParallelism = MaxParallelism });

        return results.Sum(static r => (long)r.Length);
    }

    [Benchmark(Description = "ForEachParallelAsync - Bounded processing")]
    public async Task<long> ForEachParallel_BoundedProcessing()
    {
        long totalChars = 0;
        await _filePaths.ToAsyncEnumerable()
            .ForEachParallelAsync(
                async (path, ct) =>
                {
                    var content = await File.ReadAllTextAsync(path, ct);
                    Interlocked.Add(ref totalChars, content.Length);
                },
                new() { MaxDegreeOfParallelism = MaxParallelism });

        return totalChars;
    }

    [Benchmark(Description = "SelectParallelAsync - With observability")]
    public async Task<long> SelectParallel_WithObservability()
    {
        var results = await _filePaths.SelectParallelAsync(
            static (path, ct) => new ValueTask<string>(File.ReadAllTextAsync(path, ct)),
            new()
            {
                MaxDegreeOfParallelism = MaxParallelism,
                Metrics = new MetricsOptions
                {
                    SampleInterval = TimeSpan.FromMilliseconds(50),
                    OnMetricsSample = static _ => ValueTask.CompletedTask
                },
                Progress = new ProgressOptions
                {
                    ReportInterval = TimeSpan.FromMilliseconds(50),
                    OnProgress = static _ => ValueTask.CompletedTask
                }
            });

        return results.Sum(static r => (long)r.Length);
    }
}
