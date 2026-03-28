# Rivulet.Polly

<!-- DESCRIPTION_START -->
Integration between Rivulet parallel processing and [Polly](https://github.com/App-vNext/Polly) resilience policies.
<!-- DESCRIPTION_END -->

<!-- KEY_FEATURES_START -->
## Features

- **Use Polly policies with Rivulet** - Apply any Polly policy to parallel operations
- **Convert Rivulet to Polly** - Use Rivulet configuration as standalone Polly policies
- **Advanced resilience patterns** - Hedging, result-based retry, and more
- **Battle-tested** - Built on Polly's production-proven resilience library
<!-- KEY_FEATURES_END -->

<!-- FEATURES_START -->
## API

- **SelectParallelWithPolicyAsync** - Apply Polly policies to parallel operations
- **SelectParallelWithHedgingAsync** - Hedging pattern (parallel redundant requests)
- **SelectParallelWithResultRetryAsync** - Result-based retry with Polly policies
- **ToPollyRetryPipeline** - Convert Rivulet retry to Polly pipeline
<!-- FEATURES_END -->

## Installation

```bash
dotnet add package Rivulet.Polly
```

## Quick Start

### Use Polly Policies with Rivulet

```csharp
using Polly;
using Rivulet.Polly;

// Create a Polly retry policy
var retryPolicy = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(1),
        BackoffType = DelayBackoffType.Exponential
    })
    .Build();

// Apply it to parallel processing
var results = await items.SelectParallelWithPolicyAsync(
    async (item, ct) => await ProcessAsync(item, ct),
    retryPolicy,
    new ParallelOptionsRivulet { MaxDegreeOfParallelism = 10 });
```

### Convert Rivulet Options to Polly

```csharp
var rivuletOptions = new ParallelOptionsRivulet
{
    MaxRetries = 3,
    IsTransient = ex => ex is HttpRequestException,
    BackoffStrategy = BackoffStrategy.ExponentialJitter,
    BaseDelay = TimeSpan.FromMilliseconds(100)
};

// Convert to Polly pipeline for standalone use
var pollyPipeline = rivuletOptions.ToPollyRetryPipeline();

// Use anywhere
var result = await pollyPipeline.ExecuteAsync(async ct => await CallApiAsync(ct));
```

### Advanced Features

#### Hedging (Reduce Tail Latency)

```csharp
// Send hedged requests if first is slow
var results = await urls.SelectParallelWithHedgingAsync(
    async (url, ct) => await httpClient.GetAsync(url, ct),
    maxHedgedAttempts: 2,
    hedgingDelay: TimeSpan.FromMilliseconds(100));
```

#### Result-Based Retry

```csharp
// Retry based on result value, not just exceptions
var results = await urls.SelectParallelWithResultRetryAsync(
    async (url, ct) => await httpClient.GetAsync(url, ct),
    shouldRetry: response => response.StatusCode == HttpStatusCode.TooManyRequests,
    maxRetries: 3);
```

## Framework Support

- .NET 8.0
- .NET 9.0

## Documentation & Source

- **GitHub Repository**: [https://github.com/Jeffeek/Rivulet](https://github.com/Jeffeek/Rivulet)
- **Report Issues**: [https://github.com/Jeffeek/Rivulet/issues](https://github.com/Jeffeek/Rivulet/issues)
- **License**: MIT

## License

MIT License - see LICENSE file for details

---

**Made with ❤️ by Jeffeek** | [NuGet](https://www.nuget.org/packages/Rivulet.Polly/) | [GitHub](https://github.com/Jeffeek/Rivulet)
