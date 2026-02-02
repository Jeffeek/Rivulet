# Rivulet.Pipeline

Multi-stage pipeline composition for Rivulet with fluent API, per-stage concurrency, backpressure management between stages, and streaming support.

## Features

- **Fluent Builder API** - Type-safe pipeline construction with IntelliSense support
- **Per-Stage Concurrency** - Different parallelism levels for each processing stage
- **Backpressure Management** - Automatic flow control between stages using channels
- **Streaming & Buffered Modes** - Memory-efficient streaming or materialized results
- **Full Rivulet.Core Integration** - Retries, circuit breakers, rate limiting, metrics

## Quick Start

```csharp
using Rivulet.Pipeline;

var pipeline = PipelineBuilder.Create<string>("MyPipeline")
    .SelectParallel(
        async (url, ct) => await httpClient.GetStringAsync(url, ct),
        new StageOptions { ParallelOptions = new() { MaxDegreeOfParallelism = 32 } })
    .SelectParallel(
        (html, ct) => ValueTask.FromResult(ParseHtml(html)),
        new StageOptions { ParallelOptions = new() { MaxDegreeOfParallelism = 16 } })
    .Batch(100)
    .SelectParallel(
        async (batch, ct) => { await db.BulkInsertAsync(batch, ct); return batch.Count; },
        new StageOptions { ParallelOptions = new() { MaxDegreeOfParallelism = 4 } })
    .Build();

// Execute with streaming (memory efficient)
await foreach (var result in pipeline.ExecuteStreamAsync(urls.ToAsyncEnumerable()))
{
    Console.WriteLine($"Batch saved: {result} records");
}

// Or materialize all results
var results = await pipeline.ExecuteAsync(urls);
```

## Stage Types

| Stage | Description |
|-------|-------------|
| `SelectParallel` | Transform each item in parallel |
| `WhereParallel` | Filter items in parallel |
| `Batch` | Group items into fixed-size batches |
| `BatchSelectParallel` | Batch and transform in one stage |
| `SelectManyParallel` | Flatten collections in parallel |
| `Tap` | Execute side effects without transformation |
| `Buffer` | Decouple stages with explicit buffering |
| `Throttle` | Rate limit items flowing through |

## Pipeline-Wide Configuration

```csharp
var pipeline = PipelineBuilder.Create<string>("ResilientPipeline")
    .SelectParallel(ProcessAsync)
    .WithRetries(3, BackoffStrategy.ExponentialJitter)
    .WithCircuitBreaker(5, TimeSpan.FromSeconds(30))
    .WithRateLimit(100, burstCapacity: 200)
    .WithProgress(p => Console.WriteLine($"Progress: {p.ItemsProcessed}"))
    .Build();
```

## Documentation

See the [full documentation](https://rivulet2.readthedocs.io/) for detailed guides and API reference.
