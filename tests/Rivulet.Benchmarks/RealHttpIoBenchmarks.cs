using System.Net;
using System.Net.Sockets;
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
public class RealHttpIoBenchmarks
{
    private const int ItemCount = 100;
    private const int MaxParallelism = 16;

    // Small JSON response — realistic for a lightweight API endpoint
    private static readonly byte[] ResponseBody = """{"id":1,"status":"ok"}"""u8.ToArray();

    private HttpListener _listener = null!;
    private HttpClient _httpClient = null!;
    private IReadOnlyList<int> _source = null!;
    private string _baseUrl = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        var port = GetFreePort();
        _baseUrl = $"http://localhost:{port}/";

        _listener = new HttpListener();
        _listener.Prefixes.Add(_baseUrl);
        _listener.Start();

        // Single accept loop — each accepted context is handled concurrently (fire-and-forget)
        _ = Task.Run(async () =>
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (HttpListenerException) { return; }
                catch (ObjectDisposedException) { return; }

                _ = HandleRequestAsync(context);
            }
        });

        _httpClient = new HttpClient();
        _source = Enumerable.Range(1, ItemCount).ToList();

        // Warm up: establish the connection before benchmarking starts
        await _httpClient.GetStringAsync(_baseUrl);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _listener.Stop();
        _httpClient.Dispose();
    }

    [Benchmark(Baseline = true, Description = "Baseline - Sequential requests")]
    public async Task<long> Baseline_Sequential()
    {
        long totalBytes = 0;
        foreach (var id in _source)
        {
            var content = await _httpClient.GetStringAsync($"{_baseUrl}?id={id}");
            totalBytes += content.Length;
        }

        return totalBytes;
    }

    [Benchmark(Description = "Baseline - Task.WhenAll (unbounded)")]
    public async Task<long> Baseline_TaskWhenAll()
    {
        var tasks = _source.Select(id => _httpClient.GetStringAsync($"{_baseUrl}?id={id}"));
        var results = await Task.WhenAll(tasks);
        return results.Sum(static r => (long)r.Length);
    }

    [Benchmark(Description = "Baseline - Parallel.ForEachAsync")]
    public async Task<long> Baseline_ParallelForEachAsync()
    {
        long totalBytes = 0;
        await Parallel.ForEachAsync(
            _source,
            new ParallelOptions { MaxDegreeOfParallelism = MaxParallelism },
            async (id, ct) =>
            {
                var content = await _httpClient.GetStringAsync($"{_baseUrl}?id={id}", ct);
                Interlocked.Add(ref totalBytes, content.Length);
            });

        return totalBytes;
    }

    [Benchmark(Description = "SelectParallelAsync - Bounded requests")]
    public async Task<long> SelectParallel_BoundedRequests()
    {
        var results = await _source.SelectParallelAsync(
            (id, ct) => new ValueTask<string>(_httpClient.GetStringAsync($"{_baseUrl}?id={id}", ct)),
            new() { MaxDegreeOfParallelism = MaxParallelism });

        return results.Sum(static r => (long)r.Length);
    }

    [Benchmark(Description = "SelectParallelAsync - With rate limiting (50 req/s)")]
    public async Task<long> SelectParallel_WithRateLimit()
    {
        var results = await _source.SelectParallelAsync(
            (id, ct) => new ValueTask<string>(_httpClient.GetStringAsync($"{_baseUrl}?id={id}", ct)),
            new()
            {
                MaxDegreeOfParallelism = MaxParallelism,
                RateLimit = new() { TokensPerSecond = 50, BurstCapacity = MaxParallelism }
            });

        return results.Sum(static r => (long)r.Length);
    }

    [Benchmark(Description = "SelectParallelAsync - With observability")]
    public async Task<long> SelectParallel_WithObservability()
    {
        var results = await _source.SelectParallelAsync(
            (id, ct) => new ValueTask<string>(_httpClient.GetStringAsync($"{_baseUrl}?id={id}", ct)),
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

    private static async Task HandleRequestAsync(HttpListenerContext context)
    {
        var response = context.Response;
        response.ContentType = "application/json";
        response.ContentLength64 = ResponseBody.Length;
        await response.OutputStream.WriteAsync(ResponseBody);
        response.Close();
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
