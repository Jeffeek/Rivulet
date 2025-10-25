# Rivulet.Core

**Safe, async-first parallel operators with bounded concurrency, retries, and backpressure for I/O-heavy workloads.**

Transform collections in parallel while maintaining control over concurrency, errors, and resource usage.

## Installation

```bash
dotnet add package Rivulet.Core
```

## Quick Start

### Parallel Transformation

```csharp
using Rivulet.Core;

var urls = new[] { "https://api.example.com/1", "https://api.example.com/2", /* ... */ };

var results = await urls.SelectParallelAsync(
    async (url, ct) =>
    {
        using var response = await httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    },
    new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 32,
        MaxRetries = 3,
        IsTransient = ex => ex is HttpRequestException or TaskCanceledException,
        ErrorMode = ErrorMode.CollectAndContinue
    });
```

### Streaming Results

Process results as they complete instead of waiting for all:

```csharp
await foreach (var result in source.SelectParallelStreamAsync(
    async (item, ct) => await ProcessAsync(item, ct),
    new ParallelOptionsRivulet { MaxDegreeOfParallelism = 16 }))
{
    // Handle result immediately as it completes
    Console.WriteLine(result);
}
```

### Parallel Side Effects

Execute actions in parallel without collecting results:

```csharp
await items.ForEachParallelAsync(
    async (item, ct) => await SaveToDbAsync(item, ct),
    new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 10,
        ErrorMode = ErrorMode.FailFast
    });
```

## Key Features

- ✅ **Bounded Concurrency** - Control max parallel operations with backpressure
- ✅ **Retry Policies** - Automatic retries with exponential backoff for transient errors
- ✅ **Error Handling Modes** - FailFast, CollectAndContinue, or BestEffort
- ✅ **Streaming Support** - Process results incrementally via `IAsyncEnumerable<T>`
- ✅ **Cancellation** - Full `CancellationToken` support throughout
- ✅ **Lifecycle Hooks** - OnStart, OnComplete, OnError, OnThrottle callbacks
- ✅ **Per-Item Timeouts** - Enforce timeouts for individual operations
- ✅ **Works with both** `IEnumerable<T>` and `IAsyncEnumerable<T>`

## Configuration Options

```csharp
new ParallelOptionsRivulet
{
    // Concurrency control
    MaxDegreeOfParallelism = 32,        // Max concurrent operations (default: CPU cores)
    ChannelCapacity = 1024,              // Backpressure buffer size (streaming only)

    // Error handling
    ErrorMode = ErrorMode.CollectAndContinue,  // How to handle failures
    OnErrorAsync = async (index, ex) => { /* ... */ return true; },

    // Retry policy
    MaxRetries = 3,                      // Number of retry attempts
    IsTransient = ex => ex is HttpRequestException,  // Which errors to retry
    BaseDelay = TimeSpan.FromMilliseconds(100),     // Exponential backoff base

    // Timeouts
    PerItemTimeout = TimeSpan.FromSeconds(30),  // Timeout per item

    // Lifecycle hooks
    OnStartItemAsync = async (index) => { /* ... */ },
    OnCompleteItemAsync = async (index) => { /* ... */ },
    OnThrottleAsync = async (count) => { /* ... */ },
    OnDrainAsync = async (count) => { /* ... */ }
}
```

## Error Modes

- **FailFast** - Stop immediately on first error and throw
- **CollectAndContinue** - Continue processing, collect all errors, throw `AggregateException` at end
- **BestEffort** - Continue processing, return successful results only, suppress errors

## Framework Support

- .NET 8.0
- .NET 9.0

## Documentation & Source

- **GitHub Repository**: [https://github.com/Jeffeek/Rivulet](https://github.com/Jeffeek/Rivulet)
- **Report Issues**: [https://github.com/Jeffeek/Rivulet/issues](https://github.com/Jeffeek/Rivulet/issues)
- **License**: MIT

## Performance Tips

1. **Choose appropriate parallelism** - Start with `MaxDegreeOfParallelism = 32` for I/O-bound work
2. **Use streaming** for large datasets - `SelectParallelStreamAsync` reduces memory usage
3. **Set per-item timeouts** - Prevent hung operations from blocking the pipeline
4. **Configure backpressure** - Adjust `ChannelCapacity` based on memory constraints
5. **Handle transient errors** - Use `IsTransient` and `MaxRetries` for flaky APIs

---

**Made with ❤️ by Jeffeek** | [NuGet](https://www.nuget.org/packages/Rivulet.Core/) | [GitHub](https://github.com/Jeffeek/Rivulet)
