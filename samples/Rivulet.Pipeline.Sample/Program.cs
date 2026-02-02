using Rivulet.Core;
using Rivulet.Core.Resilience;
using Rivulet.Pipeline;

// ReSharper disable ArgumentsStyleLiteral
// ReSharper disable ArgumentsStyleStringLiteral

Console.WriteLine("=== Rivulet.Pipeline Sample ===\n");

// ============================================================================
// Example 1: Basic multi-stage pipeline
// ============================================================================
Console.WriteLine("--- Example 1: Basic Multi-Stage Pipeline ---");

var basicPipeline = PipelineBuilder.Create<int>("BasicPipeline")
    .SelectParallel(static (x, _) => ValueTask.FromResult(x * 2),
        name: "Double")
    .SelectParallel(static (x, _) => ValueTask.FromResult(x + 1),
        name: "Increment")
    .WhereParallel(static x => x % 3 == 0,
        name: "FilterDivisibleBy3")
    .Build();

var basicResults = await basicPipeline.ExecuteAsync(Enumerable.Range(1, 20));
Console.WriteLine($"Results: [{string.Join(", ", basicResults)}]\n");

// ============================================================================
// Example 2: Pipeline with different concurrency per stage
// ============================================================================
Console.WriteLine("--- Example 2: Different Concurrency Per Stage ---");

var concurrencyPipeline = PipelineBuilder.Create<string>("ConcurrencyDemo")
    .SelectParallel(static async (url, ct) =>
        {
            // Simulate HTTP fetch (high concurrency - I/O bound)
            await Task.Delay(50, ct);
            return $"Content from {url}";
        },
        new StageOptions
        {
            ParallelOptions = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 8 }
        },
        name: "FetchUrls")
    .SelectParallel(static (content, _) =>
        {
            // Simulate CPU-bound parsing (lower concurrency)
            // ReSharper disable once ConvertToLambdaExpression
            return ValueTask.FromResult(content.Length);
        },
        new StageOptions
        {
            ParallelOptions = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 4 }
        },
        name: "ParseContent")
    .Build();

var urls = Enumerable.Range(1, 10).Select(static i => $"https://example.com/page{i}");
var lengths = await concurrencyPipeline.ExecuteAsync(urls);
Console.WriteLine($"Content lengths: [{string.Join(", ", lengths)}]\n");

// ============================================================================
// Example 3: Batching pipeline
// ============================================================================
Console.WriteLine("--- Example 3: Batching Pipeline ---");

var batchPipeline = PipelineBuilder.Create<int>("BatchPipeline")
    .SelectParallel(static (x, _) => ValueTask.FromResult(x * 10),
        name: "Multiply")
    .Batch(5, name: "BatchBy5")
    .SelectParallel(static (batch, _) =>
        {
            var sum = batch.Sum();
            Console.WriteLine($"  Processing batch of {batch.Count} items, sum = {sum}");
            return ValueTask.FromResult(sum);
        },
        name: "SumBatch")
    .Build();

var batchResults = await batchPipeline.ExecuteAsync(Enumerable.Range(1, 17));
Console.WriteLine($"Batch sums: [{string.Join(", ", batchResults)}]\n");

// ============================================================================
// Example 4: Pipeline with retries and circuit breaker
// ============================================================================
Console.WriteLine("--- Example 4: Pipeline with Retries ---");

var failureCount = 0;
var resilientPipeline = PipelineBuilder.Create<int>("ResilientPipeline")
    .SelectParallel(
        async (x, ct) =>
        {
            // Simulate transient failures
            if (Interlocked.Increment(ref failureCount) % 3 == 0)
                throw new InvalidOperationException($"Transient failure for item {x}");

            await Task.Delay(10, ct);
            return x * 2;
        },
        new StageOptions
        {
            ParallelOptions = new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 4,
                MaxRetries = 3,
                BaseDelay = TimeSpan.FromMilliseconds(10),
                BackoffStrategy = BackoffStrategy.ExponentialJitter,
                IsTransient = static ex => ex is InvalidOperationException,
                ErrorMode = ErrorMode.BestEffort
            }
        },
        name: "ProcessWithRetries")
    .Build();

var resilientResults = await resilientPipeline.ExecuteAsync(Enumerable.Range(1, 10));
Console.WriteLine($"Results (with retries): [{string.Join(", ", resilientResults)}]\n");

// ============================================================================
// Example 5: Streaming pipeline
// ============================================================================
Console.WriteLine("--- Example 5: Streaming Pipeline ---");

var streamingPipeline = PipelineBuilder.Create<int>("StreamingPipeline")
    .SelectParallel(static async (x, ct) =>
        {
            await Task.Delay(20, ct);
            return x * 3;
        },
        name: "TripleWithDelay")
    .Tap(static (x, _) =>
        {
            Console.WriteLine($"  Processed: {x}");
            return ValueTask.CompletedTask;
        },
        name: "LogResult")
    .Build();

Console.WriteLine("Streaming results as they complete:");
await foreach (var result in streamingPipeline.ExecuteStreamAsync(Enumerable.Range(1, 5).ToAsyncEnumerable()))
    Console.WriteLine($"Received: {result}");

Console.WriteLine();

// ============================================================================
// Example 6: SelectMany (flatten) pipeline
// ============================================================================
Console.WriteLine("--- Example 6: SelectMany (Flatten) Pipeline ---");

var flattenPipeline = PipelineBuilder.Create<int>("FlattenPipeline")
    .SelectManyParallel(static x => Enumerable.Range(1, x), // 1 -> [1], 2 -> [1,2], 3 -> [1,2,3], etc.
        name: "ExpandToRange")
    .SelectParallel(static x => x * x,
        name: "Square")
    .Build();

var flatResults = await flattenPipeline.ExecuteAsync(Enumerable.Range(1, 4));
Console.WriteLine($"Flattened and squared: [{string.Join(", ", flatResults)}]\n");

// ============================================================================
// Example 7: Pipeline with throttling
// ============================================================================
Console.WriteLine("--- Example 7: Pipeline with Throttling ---");

var throttledPipeline = PipelineBuilder.Create<int>("ThrottledPipeline")
    .Throttle(5, burstCapacity: 5, name: "Throttle5PerSecond") // 5 items/second
    .SelectParallel(static (x, _) =>
        {
            Console.WriteLine($"  Processing {x} at {DateTime.Now:HH:mm:ss.fff}");
            return ValueTask.FromResult(x);
        },
        name: "Process")
    .Build();

Console.WriteLine("Processing with rate limit (5/sec):");
var sw = System.Diagnostics.Stopwatch.StartNew();
var throttledResults = await throttledPipeline.ExecuteAsync(Enumerable.Range(1, 10));
Console.WriteLine($"Processed {throttledResults.Count} items in {sw.ElapsedMilliseconds}ms\n");

// ============================================================================
// Example 8: Pipeline with callbacks
// ============================================================================
Console.WriteLine("--- Example 8: Pipeline with Callbacks ---");

var callbackPipeline = PipelineBuilder.Create<int>(new PipelineOptions
    {
        Name = "CallbackPipeline",
        OnPipelineStartAsync = static ctx =>
        {
            Console.WriteLine($"  Pipeline '{ctx.PipelineName}' started (ExecutionId: {ctx.ExecutionId})");
            return ValueTask.CompletedTask;
        },
        OnPipelineCompleteAsync = static (_, result) =>
        {
            Console.WriteLine($"  Pipeline completed: {result.ItemsCompleted} items in {result.Elapsed.TotalMilliseconds:F0}ms");
            Console.WriteLine($"  Throughput: {result.ItemsPerSecond:F1} items/sec");
            return ValueTask.CompletedTask;
        },
        OnStageStartAsync = static (name, index) =>
        {
            Console.WriteLine($"    Stage '{name}' (index {index}) starting...");
            return ValueTask.CompletedTask;
        }
    })
    .SelectParallel(static async (x, ct) =>
        {
            await Task.Delay(10, ct);
            return x * 2;
        },
        name: "DoubleStage")
    .SelectParallel(static (x, _) => ValueTask.FromResult(x + 1),
        name: "IncrementStage")
    .Build();

_ = await callbackPipeline.ExecuteAsync(Enumerable.Range(1, 5));
Console.WriteLine();

Console.WriteLine("=== All Examples Complete ===");
