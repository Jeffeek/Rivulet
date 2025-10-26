<div align="center">
  <img src="assets/logo.png" alt="Rivulet.Core Logo" width="600">
</div>

<div align="center">

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

</div>

---

**Safe, async-first parallel operators with bounded concurrency, retries, cancellation, and streaming backpressure for I/O-heavy workloads.**

- Async-first (`ValueTask`), works with `IEnumerable<T>` and `IAsyncEnumerable<T>`
- Bounded concurrency with backpressure (Channels)
- Retry policy with transient detection and configurable backoff strategies (Exponential, ExponentialJitter, DecorrelatedJitter, Linear, LinearJitter)
- Per-item timeouts, cancellation, lifecycle hooks
- Flexible error modes: FailFast, CollectAndContinue, BestEffort
- Ordered output mode for sequence-sensitive operations

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

### Ordered Output

Maintain input order when sequence matters:

```csharp
// Results returned in same order as input, despite parallel processing
var results = await items.SelectParallelAsync(
    async (item, ct) => await ProcessAsync(item, ct),
    new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 32,
        OrderedOutput = true  // Ensures results match input order
    });

// Streaming with ordered output
await foreach (var result in source.SelectParallelStreamAsync(
    async (x, ct) => await TransformAsync(x, ct),
    new ParallelOptionsRivulet { OrderedOutput = true }))
{
    // Results arrive in input order
}
```

### Backoff Strategies

Choose from multiple backoff strategies to optimize retry behavior:

```csharp
// Exponential backoff with jitter - recommended for rate-limited APIs
// Reduces thundering herd by randomizing retry delays
var results = await requests.SelectParallelAsync(
    async (req, ct) => await apiClient.SendAsync(req, ct),
    new ParallelOptionsRivulet
    {
        MaxRetries = 4,
        BaseDelay = TimeSpan.FromMilliseconds(100),
        BackoffStrategy = BackoffStrategy.ExponentialJitter,  // Random(0, BaseDelay * 2^attempt)
        IsTransient = ex => ex is HttpRequestException
    });

// Decorrelated jitter - best for preventing synchronization across multiple clients
var results = await tasks.SelectParallelAsync(
    async (task, ct) => await ProcessAsync(task, ct),
    new ParallelOptionsRivulet
    {
        MaxRetries = 3,
        BackoffStrategy = BackoffStrategy.DecorrelatedJitter,  // Random based on previous delay
        IsTransient = ex => ex is TimeoutException
    });

// Linear backoff - gentler, predictable increase
var results = await items.SelectParallelAsync(
    async (item, ct) => await SaveAsync(item, ct),
    new ParallelOptionsRivulet
    {
        MaxRetries = 5,
        BaseDelay = TimeSpan.FromSeconds(1),
        BackoffStrategy = BackoffStrategy.Linear,  // BaseDelay * attempt
        IsTransient = ex => ex is InvalidOperationException
    });
```

Available strategies:
- **Exponential** *(default)*: `BaseDelay * 2^(attempt-1)` - Predictable exponential growth
- **ExponentialJitter**: `Random(0, BaseDelay * 2^(attempt-1))` - Reduces thundering herd
- **DecorrelatedJitter**: `Random(BaseDelay, PreviousDelay * 3)` - Prevents client synchronization
- **Linear**: `BaseDelay * attempt` - Gentler, linear growth
- **LinearJitter**: `Random(0, BaseDelay * attempt)` - Linear with randomization

### Progress Reporting

Track progress with real-time metrics for long-running operations:

```csharp
// Monitor ETL job progress with ETA
var records = await database.GetRecordsAsync().SelectParallelAsync(
    async (record, ct) => await TransformAndLoadAsync(record, ct),
    new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 20,
        Progress = new ProgressOptions
        {
            ReportInterval = TimeSpan.FromSeconds(5),
            OnProgress = progress =>
            {
                Console.WriteLine($"Progress: {progress.ItemsCompleted}/{progress.TotalItems}");
                Console.WriteLine($"Rate: {progress.ItemsPerSecond:F1} items/sec");
                Console.WriteLine($"ETA: {progress.EstimatedTimeRemaining}");
                Console.WriteLine($"Errors: {progress.ErrorCount}");
                return ValueTask.CompletedTask;
            }
        }
    });

// Streaming progress (total unknown)
await foreach (var result in stream.SelectParallelStreamAsync(
    async (item, ct) => await ProcessAsync(item, ct),
    new ParallelOptionsRivulet
    {
        Progress = new ProgressOptions
        {
            ReportInterval = TimeSpan.FromSeconds(10),
            OnProgress = progress =>
            {
                // No ETA or percent for streams - total is unknown
                Console.WriteLine($"Processed: {progress.ItemsCompleted}");
                Console.WriteLine($"Rate: {progress.ItemsPerSecond:F1} items/sec");
                return ValueTask.CompletedTask;
            }
        }
    }))
{
    // Process results as they arrive
}
```

Progress metrics:
- **ItemsStarted**: Total items that began processing
- **ItemsCompleted**: Successfully completed items
- **TotalItems**: Total count (known for arrays/lists, null for streams)
- **ErrorCount**: Failed items across all retries
- **Elapsed**: Time since operation started
- **ItemsPerSecond**: Processing rate
- **EstimatedTimeRemaining**: ETA (when total is known)
- **PercentComplete**: 0-100% (when total is known)

### Batching Operations

Process items in batches for bulk operations like database inserts, batch API calls, or file operations:

```csharp
// Bulk database inserts - batch 100 records at a time
var results = await records.BatchParallelAsync(
    batchSize: 100,
    async (batch, ct) =>
    {
        // Insert entire batch in a single database call
        await db.BulkInsertAsync(batch, ct);
        return batch.Count;
    },
    new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 4, // Process 4 batches in parallel
        MaxRetries = 3,
        IsTransient = ex => ex is SqlException
    });

// Batch API calls with timeout - flush partial batches after delay
var apiResults = await items.BatchParallelAsync(
    batchSize: 50,
    async (batch, ct) =>
    {
        // Call API with batch of items
        return await apiClient.ProcessBatchAsync(batch, ct);
    },
    batchTimeout: TimeSpan.FromSeconds(2) // Flush batch after 2 seconds even if not full
);

// Streaming batches from async source
await foreach (var result in dataStream.BatchParallelStreamAsync(
    batchSize: 100,
    async (batch, ct) =>
    {
        await ProcessBatchAsync(batch, ct);
        return batch.Count;
    },
    new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 8,
        OrderedOutput = true, // Maintain batch order
        Progress = new ProgressOptions
        {
            ReportInterval = TimeSpan.FromSeconds(5),
            OnProgress = progress =>
            {
                Console.WriteLine($"Batches processed: {progress.ItemsCompleted}");
                return ValueTask.CompletedTask;
            }
        }
    }))
{
    // Process batch results as they complete
    Console.WriteLine($"Batch completed with {result} items");
}
```

**Key Features:**
- **Size-based batching**: Groups items into batches of specified size
- **Timeout-based flushing**: Optional timeout to flush incomplete batches (async streams only)
- **Parallel batch processing**: Process multiple batches concurrently with bounded parallelism
- **All existing features**: Works with retries, error handling, progress tracking, ordered output
- **Efficient for bulk operations**: Reduces API calls, database round-trips, and I/O overhead

**Use Cases:**
- Bulk database inserts/updates/deletes
- Batch API calls to external services
- File processing in chunks
- Message queue batch processing
- ETL pipelines with staged operations

### Runtime Metrics & EventCounters

Monitor parallel operations with built-in metrics via .NET EventCounters and optional callbacks for custom monitoring systems:

```csharp
// Zero-cost monitoring with EventCounters (always enabled)
// Monitor with: dotnet-counters monitor --process-id <PID> --counters Rivulet.Core
var results = await items.SelectParallelAsync(ProcessAsync, options);

// Custom metrics callback for Prometheus, DataDog, Application Insights
var options = new ParallelOptionsRivulet
{
    MaxDegreeOfParallelism = 32,
    Metrics = new MetricsOptions
    {
        SampleInterval = TimeSpan.FromSeconds(10),
        OnMetricsSample = async snapshot =>
        {
            // Export to your monitoring system
            await prometheus.RecordMetrics(new
            {
                active_workers = snapshot.ActiveWorkers,
                items_completed = snapshot.ItemsCompleted,
                throughput = snapshot.ItemsPerSecond,
                error_rate = snapshot.ErrorRate,
                total_retries = snapshot.TotalRetries
            });
        }
    }
};

var results = await urls.SelectParallelAsync(
    async (url, ct) => await httpClient.GetAsync(url, ct),
    options);
```

**Available Metrics:**
- **ActiveWorkers**: Current number of active worker tasks
- **QueueDepth**: Items waiting in the input channel queue
- **ItemsStarted**: Total items that began processing
- **ItemsCompleted**: Total items completed successfully
- **TotalRetries**: Cumulative retry attempts across all items
- **TotalFailures**: Total failed items (after all retries)
- **ThrottleEvents**: Backpressure events when queue is full
- **ItemsPerSecond**: Current throughput rate
- **ErrorRate**: Failure rate (TotalFailures / ItemsStarted)
- **Elapsed**: Time since operation started

**EventCounters (zero-cost monitoring):**
```bash
# Monitor in real-time with dotnet-counters
dotnet-counters monitor --process-id <PID> --counters Rivulet.Core

# Available counters:
# - items-started
# - items-completed
# - total-retries
# - total-failures
# - throttle-events
# - drain-events
```

**Key Features:**
- **Zero-cost when not monitored**: EventCounters have minimal overhead
- **Thread-safe**: Uses lock-free Interlocked operations
- **Callback isolation**: Exceptions in callbacks don't break operations
- **Integrates with all operators**: SelectParallelAsync, SelectParallelStreamAsync, ForEachParallelAsync, BatchParallel*

**Use Cases:**
- Production monitoring and alerting
- Performance tuning and capacity planning
- Debugging throughput issues
- SLA compliance verification
- Auto-scaling triggers

## Development Scripts

The repository includes PowerShell scripts to streamline development and release workflows:

### Build.ps1
Build, restore, and test the solution locally.

```powershell
# Debug build with tests (default)
.\Build.ps1

# Release build with tests
.\Build.ps1 -Configuration Release

# Skip tests
.\Build.ps1 -SkipTests
```

### NugetPackage.ps1
Build and inspect NuGet packages locally before releasing.

```powershell
# Build with test version
.\NugetPackage.ps1

# Build with specific version
.\NugetPackage.ps1 -Version "1.2.3"
```

Creates package in `./test-packages` and extracts contents to `./test-extract` for verification.

### SmartCommit.ps1
Generate high-quality commit messages using AI (Claude, Gemini, or OpenAI).

```powershell
# Quick setup - set API key for your preferred provider
$env:ANTHROPIC_API_KEY = "your-key"  # For Claude
$env:GOOGLE_API_KEY = "your-key"      # For Gemini
$env:OPENAI_API_KEY = "your-key"      # For OpenAI

# Auto-detect provider from environment
.\SmartCommit.ps1

# Or specify provider explicitly
.\SmartCommit.ps1 -Provider Claude
.\SmartCommit.ps1 -Provider Gemini
.\SmartCommit.ps1 -Provider OpenAI
```

**Advanced**: Create `.smartcommit.config.json` (see `.smartcommit.config.example.json`) to configure:
- Default provider
- API keys (alternative to environment variables)
- Model versions (claude-3-5-sonnet, gemini-2.0-flash, gpt-4o, etc.)

This script:
- Analyzes your staged changes using git diff
- Calls your chosen AI provider to generate a meaningful commit message
- Shows the suggested message and allows you to:
  - **[y]** Accept and commit
  - **[r]** Request revision with feedback (e.g., "make it shorter", "add more detail")
  - **[n]** Cancel
- Iteratively refines the message based on your feedback
- Commits changes when you accept

**Get API keys**:
- Claude: [console.anthropic.com](https://console.anthropic.com/)
- Gemini: [aistudio.google.com/apikey](https://aistudio.google.com/apikey)
- OpenAI: [platform.openai.com/api-keys](https://platform.openai.com/api-keys)

### Release.ps1
Create release branch, tag, and trigger automated publishing.

```powershell
# Create release for version 1.0.0 (creates branch release/1.0.x, tag v1.0.0)
.\Release.ps1 -Version "1.0.0"

# Create patch release 1.0.1 (uses existing branch release/1.0.x, tag v1.0.1)
.\Release.ps1 -Version "1.0.1"

# Create pre-release (creates branch release/2.0.x, tag v2.0.0-beta)
.\Release.ps1 -Version "2.0.0-beta"
```

**Branching Strategy**:
- Branches: `release/{major}.{minor}.x` (e.g., `release/1.0.x` for all 1.0.* versions)
- Tags: `v{full.version}` (e.g., `v1.0.0`, `v1.0.1`, `v1.0.2`)
- Master branch for active development
- Patch releases reuse the same release branch

This script:
- Creates/switches to `release/{major}.{minor}.x` branch
- Displays release information (commit details, author, version, repository)
- **Asks for confirmation (y/Y) before proceeding**
- Creates git tag `v{version}` and pushes to GitHub
- Triggers the release workflow that builds, tests, and publishes to NuGet.org

The confirmation step shows:
- Version and tag information
- Current commit hash, author, date, and message
- List of actions that will be performed
- Allows you to cancel before any changes are pushed

### Rate Limiting with Token Bucket

Control the maximum rate of operations using the token bucket algorithm, perfect for respecting API rate limits or smoothing traffic bursts:

```csharp
// Limit to 100 requests/sec with burst capacity of 200
var results = await apiUrls.SelectParallelAsync(
    async (url, ct) => await httpClient.GetAsync(url, ct),
    new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 32,
        RateLimit = new RateLimitOptions
        {
            TokensPerSecond = 100,  // Sustained rate
            BurstCapacity = 200     // Allow brief bursts
        }
    });

// Heavy operations consume more tokens
var results = await heavyTasks.SelectParallelAsync(
    async (task, ct) => await ProcessHeavyAsync(task, ct),
    new ParallelOptionsRivulet
    {
        RateLimit = new RateLimitOptions
        {
            TokensPerSecond = 50,
            BurstCapacity = 50,
            TokensPerOperation = 5  // Each operation costs 5 tokens
        }
    });
```

**Key Features:**
- **Token bucket algorithm**: Allows controlled bursts while maintaining average rate
- **Configurable rates**: Set tokens per second and burst capacity
- **Weighted operations**: Different operations can consume different token amounts
- **Works with all operators**: SelectParallel*, ForEachParallel*, BatchParallel*
- **Combines with retries**: Rate limiting applies to retry attempts too

**Use Cases:**
- Respect API rate limits (e.g., 1000 requests/hour)
- Smooth traffic bursts to downstream services
- Prevent resource exhaustion
- Control database connection usage
- Implement fair resource sharing

### Circuit Breaker

Protect your application from cascading failures when a downstream service is unhealthy. The circuit breaker monitors for failures and, once a threshold is reached, opens the circuit to fail operations fast without waiting for timeouts. This gives the unhealthy service time to recover.

```csharp
// Protect against a flaky API
var results = await urls.SelectParallelAsync(
    async (url, ct) => await httpClient.GetAsync(url, ct),
    new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 32,
        CircuitBreaker = new CircuitBreakerOptions
        {
            FailureThreshold = 0.5, // Open if 50% of operations fail
            OpenTimeout = TimeSpan.FromSeconds(30), // Wait 30s before trying again
            SamplingDuration = TimeSpan.FromSeconds(20), // In a 20s window
            MinimumThroughput = 10, // With at least 10 operations
            OnStateChange = (from, to) => Console.WriteLine($"Circuit changed from {from} to {to}")
        }
    });
```

**States:**
- **Closed**: Normal operation. Operations are executed.
- **Open**: Failures have exceeded the threshold. All operations fail immediately with `CircuitBreakerOpenException`. Retries are paused.
- **Half-Open**: After the `OpenTimeout` expires, the circuit allows a limited number of trial operations. If they succeed, the circuit closes. If they fail, it re-opens.

**Key Features:**
- **Automatic failure detection**: Monitors operations and opens the circuit based on failure rates.
- **Configurable thresholds**: Customize failure/success rates, timeouts, and sampling windows.
- **State change notifications**: Get callbacks when the circuit state changes.
- **Resiliency**: Prevents an unhealthy service from overwhelming your application.

### Roadmap

- Adaptive concurrency
