# Rivulet.Hosting

Integration package for using Rivulet with Microsoft.Extensions.Hosting, ASP.NET Core, and the .NET Generic Host.

## Features

- Dependency Injection integration
- Configuration binding for `ParallelOptionsRivulet`
- Base classes for parallel background services
- Health checks for monitoring parallel operations
- Support for ASP.NET Core and Worker Services

## Installation

```bash
dotnet add package Rivulet.Hosting
```

## Quick Start

### 1. Configure Services

```csharp
using Rivulet.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Register Rivulet with configuration from appsettings.json
builder.Services.AddRivulet(builder.Configuration);

// Or configure manually
builder.Services.AddRivulet(options =>
{
    options.MaxDegreeOfParallelism = 10;
    options.RetryPolicy = new RetryPolicyOptions
    {
        MaxRetries = 3,
        BackoffType = BackoffType.Exponential
    };
});

var app = builder.Build();
app.Run();
```

### 2. Configuration Binding (appsettings.json)

```json
{
  "Rivulet": {
    "MaxDegreeOfParallelism": 10,
    "MaxRetries": 3,
    "BaseDelay": "00:00:00.100",
    "BackoffStrategy": "ExponentialJitter",
    "PerItemTimeout": "00:00:30",
    "ErrorMode": "CollectAndContinue",
    "CircuitBreaker": {
      "FailureThreshold": 5,
      "SuccessThreshold": 2,
      "OpenTimeout": "00:00:30",
      "SamplingDuration": "00:01:00"
    },
    "RateLimit": {
      "TokensPerSecond": 100,
      "BurstCapacity": 200
    },
    "AdaptiveConcurrency": {
      "MinConcurrency": 1,
      "MaxConcurrency": 100,
      "TargetLatency": "00:00:00.100",
      "MinSuccessRate": 0.95
    }
  }
}
```

### 3. Named Configurations

Register multiple configurations for different use cases:

```csharp
// Register named configurations
builder.Services.AddRivulet("HighThroughput", builder.Configuration);
builder.Services.AddRivulet("LowLatency", builder.Configuration);

// In appsettings.json
{
  "Rivulet": {
    "HighThroughput": {
      "MaxDegreeOfParallelism": 50,
      "RateLimit": {
        "TokensPerSecond": 500,
        "BurstCapacity": 1000
      }
    },
    "LowLatency": {
      "MaxDegreeOfParallelism": 5,
      "AdaptiveConcurrency": {
        "MinConcurrency": 1,
        "MaxConcurrency": 10,
        "TargetLatency": "00:00:00.100"
      }
    }
  }
}

// Use named options
public class MyService
{
    private readonly ParallelOptionsRivulet _options;

    public MyService(IOptionsSnapshot<ParallelOptionsRivulet> options)
    {
        _options = options.Get("HighThroughput");
    }
}
```

## Background Services

### ParallelBackgroundService

Simple background service for processing items one at a time:

```csharp
public class DataProcessorService : ParallelBackgroundService<DataItem>
{
    private readonly IDataRepository _repository;

    public DataProcessorService(
        ILogger<DataProcessorService> logger,
        IDataRepository repository,
        IOptions<ParallelOptionsRivulet> options)
        : base(logger, options.Value)
    {
        _repository = repository;
    }

    protected override async IAsyncEnumerable<DataItem> GetItemsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in _repository.GetPendingItemsAsync(cancellationToken))
        {
            yield return item;
        }
    }

    protected override async Task ProcessItemAsync(DataItem item, CancellationToken cancellationToken)
    {
        // Process single item
        await _repository.ProcessAsync(item, cancellationToken);
    }
}

// Register the service
builder.Services.AddHostedService<DataProcessorService>();
```

### ParallelWorkerService

Advanced background service with parallel processing and result handling:

```csharp
public class ImageProcessingWorker : ParallelWorkerService<ImageJob, ProcessedImage>
{
    private readonly IImageService _imageService;
    private readonly IStorageService _storage;

    public ImageProcessingWorker(
        ILogger<ImageProcessingWorker> logger,
        IImageService imageService,
        IStorageService storage,
        IOptions<ParallelOptionsRivulet> options)
        : base(logger, options.Value)
    {
        _imageService = imageService;
        _storage = storage;
    }

    protected override async IAsyncEnumerable<ImageJob> GetSourceItems(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Poll for new jobs every 5 seconds
        while (!cancellationToken.IsCancellationRequested)
        {
            var jobs = await _imageService.GetPendingJobsAsync(cancellationToken);

            foreach (var job in jobs)
            {
                yield return job;
            }

            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }
    }

    protected override async Task<ProcessedImage> ProcessAsync(
        ImageJob job,
        CancellationToken cancellationToken)
    {
        // Download and process image
        var imageData = await _storage.DownloadAsync(job.ImageUrl, cancellationToken);
        var processed = await _imageService.ProcessImageAsync(imageData, job.Options, cancellationToken);

        return new ProcessedImage
        {
            JobId = job.Id,
            Data = processed,
            ProcessedAt = DateTime.UtcNow
        };
    }

    protected override async Task OnResultAsync(
        ProcessedImage result,
        CancellationToken cancellationToken)
    {
        // Save processed image
        await _storage.UploadAsync(result.Data, $"processed/{result.JobId}", cancellationToken);
        await _imageService.MarkCompletedAsync(result.JobId, cancellationToken);
    }
}

// Register the service
builder.Services.AddHostedService<ImageProcessingWorker>();
```

## Health Checks

Monitor your parallel operations with built-in health checks:

```csharp
using Rivulet.Diagnostics;

// Register PrometheusExporter and health checks
builder.Services.AddSingleton<PrometheusExporter>();
builder.Services.AddHealthChecks()
    .AddCheck<RivuletHealthCheck>(
        "rivulet",
        tags: new[] { "ready" });

// Configure health check options
builder.Services.Configure<RivuletHealthCheckOptions>(options =>
{
    options.ErrorRateThreshold = 0.1;      // 10% error rate triggers degraded status
    options.FailureCountThreshold = 100;   // 100 failures triggers unhealthy status
});

// Health check automatically monitors metrics from Rivulet operations
// No manual recording needed - metrics are captured via EventCounters

// Expose health check endpoint
app.MapHealthChecks("/health");

// Example output:
// Healthy: "Rivulet operations healthy: 950/1000 completed, 50 failures"
// Degraded: "Error rate (15.00%) exceeds threshold (10.00%)"
// Unhealthy: "Failure count (150) exceeds threshold (100)"
```

## ASP.NET Core Integration

Use Rivulet in your ASP.NET Core controllers and minimal APIs:

```csharp
// In a controller
[ApiController]
[Route("api/[controller]")]
public class DataController : ControllerBase
{
    private readonly ParallelOptionsRivulet _options;

    public DataController(IOptions<ParallelOptionsRivulet> options)
    {
        _options = options.Value;
    }

    [HttpPost("process")]
    public async Task<IActionResult> ProcessItems([FromBody] List<DataItem> items)
    {
        var results = await items
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(
                async (item, ct) => await ProcessItemAsync(item, ct),
                _options,
                HttpContext.RequestAborted)
            .ToListAsync(HttpContext.RequestAborted);

        return Ok(results);
    }
}

// In minimal APIs
app.MapPost("/api/batch", async (
    List<DataItem> items,
    IOptions<ParallelOptionsRivulet> options,
    CancellationToken ct) =>
{
    var results = await items
        .ToAsyncEnumerable()
        .SelectParallelStreamAsync(
            async (item, token) => await ProcessItemAsync(item, token),
            options.Value,
            ct)
        .ToListAsync(ct);

    return Results.Ok(results);
});
```

## Worker Service Example

Complete example of a .NET Worker Service:

```csharp
// Program.cs
using Rivulet.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Configure Rivulet
builder.Services.AddRivulet(builder.Configuration);

// Register background services
builder.Services.AddHostedService<DataSyncWorker>();
builder.Services.AddHostedService<NotificationWorker>();

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<RivuletOperationHealthCheck>("rivulet");

var host = builder.Build();
host.Run();

// DataSyncWorker.cs
public class DataSyncWorker : ParallelWorkerService<SyncJob, SyncResult>
{
    private readonly IDataService _dataService;

    public DataSyncWorker(
        ILogger<DataSyncWorker> logger,
        IDataService dataService,
        IOptions<ParallelOptionsRivulet> options)
        : base(logger, options.Value)
    {
        _dataService = dataService;
    }

    protected override async IAsyncEnumerable<SyncJob> GetSourceItems(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var jobs = await _dataService.GetPendingSyncJobsAsync(cancellationToken);

            foreach (var job in jobs)
            {
                yield return job;
            }

            if (jobs.Count == 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }
        }
    }

    protected override async Task<SyncResult> ProcessAsync(
        SyncJob job,
        CancellationToken cancellationToken)
    {
        var data = await _dataService.FetchDataAsync(job.Source, cancellationToken);
        await _dataService.SyncToDestinationAsync(job.Destination, data, cancellationToken);

        return new SyncResult
        {
            JobId = job.Id,
            RecordsSynced = data.Count,
            CompletedAt = DateTime.UtcNow
        };
    }

    protected override async Task OnResultAsync(
        SyncResult result,
        CancellationToken cancellationToken)
    {
        await _dataService.UpdateJobStatusAsync(result.JobId, "Completed", cancellationToken);
    }
}
```

## Best Practices

### 1. Use Dependency Injection

Always inject `IOptions<ParallelOptionsRivulet>` to access configuration:

```csharp
public class MyService
{
    private readonly ParallelOptionsRivulet _options;

    public MyService(IOptions<ParallelOptionsRivulet> options)
    {
        _options = options.Value;
    }
}
```

### 2. Graceful Shutdown

Background services automatically handle cancellation. Always respect the `CancellationToken`:

```csharp
protected override async Task ProcessItemAsync(DataItem item, CancellationToken cancellationToken)
{
    // Check cancellation frequently
    cancellationToken.ThrowIfCancellationRequested();

    await LongRunningOperationAsync(item, cancellationToken);
}
```

### 3. Error Handling

Use health checks and logging for monitoring:

```csharp
protected override async Task<Result> ProcessAsync(Job job, CancellationToken cancellationToken)
{
    try
    {
        var result = await ProcessJobAsync(job, cancellationToken);
        _healthCheck.RecordSuccess();
        return result;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to process job {JobId}", job.Id);
        _healthCheck.RecordFailure();
        throw;
    }
}
```

### 4. Configuration Management

Use different configurations for different environments:

```json
{
  "Rivulet": {
    "MaxDegreeOfParallelism": 10,
    "MaxRetries": 3,
    "BaseDelay": "00:00:00.100"
  }
}

// appsettings.Production.json
{
  "Rivulet": {
    "MaxDegreeOfParallelism": 50,
    "MaxRetries": 5,
    "BaseDelay": "00:00:00.100",
    "RateLimit": {
      "TokensPerSecond": 1000,
      "BurstCapacity": 2000
    }
  }
}
```

### 5. Resource Management

Configure appropriate parallelism based on workload:

- **CPU-bound**: `MaxDegreeOfParallelism = Environment.ProcessorCount`
- **I/O-bound**: `MaxDegreeOfParallelism = Environment.ProcessorCount * 2` or higher
- **Rate-limited**: Use `RateLimit` options to respect external API limits

## API Reference

### ServiceCollectionExtensions

- `AddRivulet(IConfiguration)` - Register from configuration
- `AddRivulet(Action<ParallelOptionsRivulet>)` - Register with action
- `AddRivulet(string, IConfiguration)` - Register named configuration

### ParallelBackgroundService<T>

- `GetItemsAsync(CancellationToken)` - Override to provide data source
- `ProcessItemAsync(T, CancellationToken)` - Override to process items

### ParallelWorkerService<TSource, TResult>

- `GetSourceItems(CancellationToken)` - Override to provide source stream
- `ProcessAsync(TSource, CancellationToken)` - Override to process items
- `OnResultAsync(TResult, CancellationToken)` - Override to handle results

### RivuletHealthCheck (from Rivulet.Diagnostics)

- `CheckHealthAsync(HealthCheckContext, CancellationToken)` - Check health status based on metrics
- Automatically monitors Rivulet operations via EventCounters
- Requires PrometheusExporter dependency for metric collection

## License

MIT License - see LICENSE file for details.

## Contributing

Contributions are welcome! Please see the main Rivulet repository for guidelines.

## Links

- [GitHub Repository](https://github.com/Jeffeek/Rivulet)
- [Documentation](https://github.com/Jeffeek/Rivulet/wiki)
- [NuGet Package](https://www.nuget.org/packages/Rivulet.Hosting)
