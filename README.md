# Rivulet.Core

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
---
[![NuGet](https://img.shields.io/nuget/v/Rivulet.Core)](https://www.nuget.org/packages/Rivulet.Core/)
![NuGet Downloads](https://img.shields.io/nuget/dt/Rivulet.Core)
---
![CI/CD Pipeline (build+test)](https://img.shields.io/github/actions/workflow/status/Jeffeek/Rivulet/github-workflow.yml?label=build)
![CI/CD Pipeline (release)](https://img.shields.io/github/actions/workflow/status/Jeffeek/Rivulet/release.yml?label=release)
---
[![Codecov](https://codecov.io/gh/Jeffeek/Rivulet/branch/master/graph/badge.svg)](https://codecov.io/gh/Jeffeek/Rivulet)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
---

Safe, async-first parallel operators with bounded concurrency, retries, cancellation, and streaming backpressure for I/O-heavy workloads.

- Async-first (`ValueTask`), works with `IEnumerable<T>` and `IAsyncEnumerable<T>`
- Bounded concurrency with backpressure (Channels)
- Retry policy with transient detection and exponential backoff
- Per-item timeouts, cancellation, lifecycle hooks
- Flexible error modes: FailFast, CollectAndContinue, BestEffort

### Install
```dotnet add package Rivulet.Core```

### Quick start
```csharp
var results = await urls.SelectParallelAsync(
    async (url, ct) =>
    {
        using var resp = await http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        return (url, (int)resp.StatusCode);
    },
    new ParallelOptionsRivulet {
        MaxDegreeOfParallelism = 32,
        MaxRetries = 3,
        IsTransient = ex => ex is HttpRequestException or TaskCanceledException,
        ErrorMode = ErrorMode.CollectAndContinue
    });
```

### Streaming

```csharp
await foreach (var r in source.SelectParallelStreamAsync(
    async (x, ct) => await ComputeAsync(x, ct),
    new ParallelOptionsRivulet { MaxDegreeOfParallelism = 16 }))
{
    // consume incrementally
}
```

### Roadmap

- Metrics via EventCounters
- Built-in retry policies (Jitter)
- Ordered output mode (optional)
- Batching operator
