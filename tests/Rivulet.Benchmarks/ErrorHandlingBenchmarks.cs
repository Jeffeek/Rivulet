using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Rivulet.Core;
using Rivulet.Core.Resilience;

namespace Rivulet.Benchmarks;

[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MarkdownExporter]
public class ErrorHandlingBenchmarks
{
    private const int ItemCount = 500;
    private IEnumerable<int> _source = null!;

    [GlobalSetup]
    public void Setup()
    {
        _source = Enumerable.Range(1, ItemCount);
    }

    [Benchmark(Baseline = true, Description = "No retries - Success path")]
    public async Task<List<int>> NoRetries_SuccessPath()
    {
        return await _source.SelectParallelAsync(
            (x, _) => new ValueTask<int>(x * 2),
            new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 8,
                MaxRetries = 0
            });
    }

    [Benchmark(Description = "With retries - 10% transient failures")]
    public async Task<List<int>> WithRetries_TransientFailures()
    {
        var attempts = new System.Collections.Concurrent.ConcurrentDictionary<int, int>();

        return await _source.SelectParallelAsync(
            (x, _) =>
            {
                var attemptCount = attempts.AddOrUpdate(x, 1, (_, count) => count + 1);

                // 10% of items fail on first attempt
                if (x % 10 == 0 && attemptCount == 1)
                    throw new InvalidOperationException("Transient error");

                return new ValueTask<int>(x * 2);
            },
            new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 8,
                MaxRetries = 3,
                BaseDelay = TimeSpan.FromMilliseconds(1),
                BackoffStrategy = BackoffStrategy.Exponential,
                IsTransient = ex => ex is InvalidOperationException
            });
    }

    [Benchmark(Description = "ErrorMode.FailFast")]
    public async Task<List<int>> ErrorMode_FailFast()
    {
        try
        {
            return await _source.SelectParallelAsync(
                (x, _) => new ValueTask<int>(x * 2),
                new ParallelOptionsRivulet
                {
                    MaxDegreeOfParallelism = 8,
                    ErrorMode = ErrorMode.FailFast
                });
        }
        catch
        {
            return [];
        }
    }

    [Benchmark(Description = "ErrorMode.BestEffort")]
    public async Task<List<int>> ErrorMode_BestEffort()
    {
        return await _source.SelectParallelAsync(
            (x, _) => new ValueTask<int>(x * 2),
            new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 8,
                ErrorMode = ErrorMode.BestEffort
            });
    }

    [Benchmark(Description = "Backoff.Exponential")]
    public async Task<List<int>> Backoff_Exponential()
    {
        var attempts = new System.Collections.Concurrent.ConcurrentDictionary<int, int>();

        return await _source.SelectParallelAsync(
            (x, _) =>
            {
                var attemptCount = attempts.AddOrUpdate(x, 1, (_, count) => count + 1);

                if (x % 20 == 0 && attemptCount <= 2)
                    throw new InvalidOperationException("Transient error");

                return new ValueTask<int>(x * 2);
            },
            new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 8,
                MaxRetries = 3,
                BaseDelay = TimeSpan.FromMilliseconds(1),
                BackoffStrategy = BackoffStrategy.Exponential,
                IsTransient = ex => ex is InvalidOperationException
            });
    }

    [Benchmark(Description = "Backoff.ExponentialJitter")]
    public async Task<List<int>> Backoff_ExponentialJitter()
    {
        var attempts = new System.Collections.Concurrent.ConcurrentDictionary<int, int>();

        return await _source.SelectParallelAsync(
            (x, _) =>
            {
                var attemptCount = attempts.AddOrUpdate(x, 1, (_, count) => count + 1);

                if (x % 20 == 0 && attemptCount <= 2)
                    throw new InvalidOperationException("Transient error");

                return new ValueTask<int>(x * 2);
            },
            new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 8,
                MaxRetries = 3,
                BaseDelay = TimeSpan.FromMilliseconds(1),
                BackoffStrategy = BackoffStrategy.ExponentialJitter,
                IsTransient = ex => ex is InvalidOperationException
            });
    }
}
