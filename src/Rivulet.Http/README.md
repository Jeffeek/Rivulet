# Rivulet.Http

**Parallel HTTP operations with automatic retries, resilient downloads, and HttpClientFactory integration.**

Built on top of Rivulet.Core, this package provides HTTP-aware parallel operators that automatically handle transient failures, respect rate limits, and support resumable downloads.

## Installation

```bash
dotnet add package Rivulet.Http
```

Requires `Rivulet.Core` (automatically included).

## Quick Start

### Parallel HTTP GET Requests

Fetch multiple URLs in parallel with automatic retry for transient HTTP errors:

```csharp
using Rivulet.Http;

var urls = new[]
{
    new Uri("https://api.example.com/users/1"),
    new Uri("https://api.example.com/users/2"),
    new Uri("https://api.example.com/users/3")
};

var responses = await urls.GetParallelAsync(
    httpClient,
    new HttpOptions
    {
        ParallelOptions = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 10,
            MaxRetries = 3
        }
    });

foreach (var response in responses)
{
    var content = await response.Content.ReadAsStringAsync();
    Console.WriteLine(content);
    response.Dispose();
}
```

### Get String Content Directly

Automatically read response content as strings:

```csharp
var urls = new[]
{
    new Uri("https://api.example.com/data/1"),
    new Uri("https://api.example.com/data/2")
};

var contents = await urls.GetStringParallelAsync(httpClient);

foreach (var content in contents)
{
    Console.WriteLine(content);
}
```

### Parallel POST Requests

Send multiple POST requests in parallel:

```csharp
var requests = users.Select(user => (
    uri: new Uri("https://api.example.com/users"),
    content: new StringContent(
        JsonSerializer.Serialize(user),
        Encoding.UTF8,
        "application/json")
));

var responses = await requests.PostParallelAsync(
    httpClient,
    new HttpOptions
    {
        ParallelOptions = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 5,
            ErrorMode = ErrorMode.CollectAndContinue
        }
    });
```

## HttpClientFactory Integration

Use named or typed HttpClient instances with IHttpClientFactory:

```csharp
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddHttpClient("api", client =>
{
    client.BaseAddress = new Uri("https://api.example.com");
    client.DefaultRequestHeaders.Add("Authorization", "Bearer token");
});

var provider = services.BuildServiceProvider();
var factory = provider.GetRequiredService<IHttpClientFactory>();

var urls = new[]
{
    new Uri("/endpoint1", UriKind.Relative),
    new Uri("/endpoint2", UriKind.Relative)
};

var results = await urls.GetStringParallelAsync(
    factory,
    clientName: "api",
    new HttpOptions
    {
        ParallelOptions = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 10
        }
    });
```

## Resilient Downloads with Resume Support

Download files in parallel with automatic resume on failure:

```csharp
var downloads = new[]
{
    (uri: new Uri("https://example.com/file1.zip"), path: "downloads/file1.zip"),
    (uri: new Uri("https://example.com/file2.zip"), path: "downloads/file2.zip"),
    (uri: new Uri("https://example.com/file3.zip"), path: "downloads/file3.zip")
};

var results = await downloads.DownloadParallelAsync(
    httpClient,
    new StreamingDownloadOptions
    {
        EnableResume = true,
        ValidateContentLength = true,
        OnProgressAsync = (uri, downloaded, total) =>
        {
            var percent = total.HasValue ? (downloaded * 100.0 / total.Value) : 0;
            Console.WriteLine($"{uri}: {downloaded:N0}/{total:N0} bytes ({percent:F1}%)");
            return ValueTask.CompletedTask;
        },
        HttpOptions = new HttpOptions
        {
            ParallelOptions = new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 3,
                MaxRetries = 5
            }
        }
    });

foreach (var (uri, filePath, bytesDownloaded) in results)
{
    Console.WriteLine($"Downloaded {uri} to {filePath}: {bytesDownloaded:N0} bytes");
}
```

### Stream Download to Custom Destination

Download directly to any Stream:

```csharp
using var memoryStream = new MemoryStream();

var bytesDownloaded = await HttpStreamingExtensions.DownloadToStreamAsync(
    new Uri("https://example.com/data.json"),
    memoryStream,
    httpClient,
    new StreamingDownloadOptions
    {
        BufferSize = 8192,
        OnProgressAsync = (uri, downloaded, total) =>
        {
            Console.WriteLine($"Downloaded: {downloaded:N0} bytes");
            return ValueTask.CompletedTask;
        }
    });

memoryStream.Position = 0;
var data = await JsonSerializer.DeserializeAsync<MyData>(memoryStream);
```

## Automatic Retry Handling

Rivulet.Http automatically retries transient HTTP errors:

- **408** Request Timeout
- **429** Too Many Requests
- **500** Internal Server Error
- **502** Bad Gateway
- **503** Service Unavailable
- **504** Gateway Timeout

### Respect Retry-After Headers

Automatically respects `Retry-After` headers from rate-limited APIs:

```csharp
var options = new HttpOptions
{
    RespectRetryAfterHeader = true, // Default: true
    ParallelOptions = new ParallelOptionsRivulet
    {
        MaxRetries = 5,
        BaseDelay = TimeSpan.FromSeconds(1)
    }
};

var responses = await urls.GetParallelAsync(httpClient, options);
```

When a server returns `429 Too Many Requests` or `503 Service Unavailable` with a `Retry-After` header, Rivulet.Http will wait the specified duration before retrying instead of using the configured backoff strategy.

## Error Handling

### HTTP-Specific Error Callbacks

Get notified of HTTP errors with status codes:

```csharp
var options = new HttpOptions
{
    OnHttpErrorAsync = (uri, statusCode, exception) =>
    {
        Console.WriteLine($"Error fetching {uri}: {statusCode} - {exception.Message}");
        return ValueTask.CompletedTask;
    },
    ParallelOptions = new ParallelOptionsRivulet
    {
        ErrorMode = ErrorMode.CollectAndContinue,
        MaxRetries = 3
    }
};

var responses = await urls.GetParallelAsync(httpClient, options);
```

### Custom Retriable Status Codes

Customize which HTTP status codes should trigger retries:

```csharp
var options = new HttpOptions
{
    RetriableStatusCodes = new HashSet<HttpStatusCode>
    {
        HttpStatusCode.TooManyRequests,
        HttpStatusCode.ServiceUnavailable,
        HttpStatusCode.RequestTimeout
    },
    ParallelOptions = new ParallelOptionsRivulet
    {
        MaxRetries = 3,
        BackoffStrategy = BackoffStrategy.ExponentialJitter
    }
};
```

## Advanced Features

### Rate Limiting

Control request rate to respect API limits:

```csharp
var options = new HttpOptions
{
    ParallelOptions = new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 10,
        RateLimit = new RateLimitOptions
        {
            TokensPerSecond = 100,
            BurstCapacity = 200
        }
    }
};

var responses = await urls.GetParallelAsync(httpClient, options);
```

### Circuit Breaker

Prevent cascading failures with circuit breaker pattern:

```csharp
var options = new HttpOptions
{
    ParallelOptions = new ParallelOptionsRivulet
    {
        CircuitBreaker = new CircuitBreakerOptions
        {
            FailureThreshold = 5,
            SuccessThreshold = 2,
            OpenTimeout = TimeSpan.FromSeconds(30)
        }
    }
};
```

### Progress Tracking

Monitor download progress for long-running operations:

```csharp
var options = new StreamingDownloadOptions
{
    ProgressInterval = TimeSpan.FromSeconds(1),
    OnProgressAsync = (uri, downloaded, total) =>
    {
        var percent = total.HasValue ? (downloaded * 100.0 / total.Value) : 0;
        Console.WriteLine($"Progress: {percent:F1}%");
        return ValueTask.CompletedTask;
    },
    OnResumeAsync = (uri, offset) =>
    {
        Console.WriteLine($"Resuming download from byte {offset:N0}");
        return ValueTask.CompletedTask;
    },
    OnCompleteAsync = (uri, path, bytes) =>
    {
        Console.WriteLine($"Download complete: {bytes:N0} bytes saved to {path}");
        return ValueTask.CompletedTask;
    }
};
```

## HTTP Methods Supported

All standard HTTP methods with parallel operators:

- **GET**: `GetParallelAsync`, `GetStringParallelAsync`, `GetByteArrayParallelAsync`
- **POST**: `PostParallelAsync`
- **PUT**: `PutParallelAsync`
- **DELETE**: `DeleteParallelAsync`

All methods support both `HttpClient` and `IHttpClientFactory`.

## Configuration Options

### HttpOptions

HTTP-specific configuration:

```csharp
var options = new HttpOptions
{
    RequestTimeout = TimeSpan.FromSeconds(30),
    RespectRetryAfterHeader = true,
    RetriableStatusCodes = new HashSet<HttpStatusCode> { /* ... */ },
    BufferSize = 81920,
    OnHttpErrorAsync = async (uri, status, ex) => { /* ... */ },
    ParallelOptions = new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 10,
        MaxRetries = 3,
        BaseDelay = TimeSpan.FromMilliseconds(100),
        BackoffStrategy = BackoffStrategy.ExponentialJitter,
        ErrorMode = ErrorMode.CollectAndContinue
    }
};
```

### StreamingDownloadOptions

Download-specific configuration:

```csharp
var options = new StreamingDownloadOptions
{
    EnableResume = true,
    ValidateContentLength = true,
    OverwriteExisting = false,
    BufferSize = 81920,
    ProgressInterval = TimeSpan.FromSeconds(1),
    HttpOptions = new HttpOptions { /* ... */ }
};
```

## Best Practices

1. **Dispose HttpResponseMessage**: Always dispose responses when using `GetParallelAsync`, `PostParallelAsync`, etc.
2. **Use String/ByteArray Variants**: Use `GetStringParallelAsync` or `GetByteArrayParallelAsync` for automatic disposal
3. **Set MaxDegreeOfParallelism**: Always limit concurrency to avoid overwhelming servers
4. **Enable Resume for Large Files**: Use `EnableResume = true` for downloads that might be interrupted
5. **Respect Rate Limits**: Use `RespectRetryAfterHeader = true` and configure `RateLimit` for APIs
6. **Use HttpClientFactory**: Prefer `IHttpClientFactory` over creating HttpClient instances manually

## Performance

Rivulet.Http is designed for high-throughput scenarios:

- **Bounded Concurrency**: Prevents resource exhaustion
- **Backpressure**: Channel-based buffering prevents memory issues
- **Zero Allocations**: Uses `ValueTask<T>` in hot paths
- **Streaming Downloads**: Efficient memory usage for large files
- **Resume Support**: Saves bandwidth by resuming interrupted downloads

## Examples

See the [samples directory](../../samples) for complete working examples including:

- API scraping with rate limiting
- Bulk data export/import
- Image downloading with progress tracking
- Resilient file synchronization

## License

MIT License - see LICENSE file for details

---

**Made with ❤️ by Jeffeek** | [NuGet](https://www.nuget.org/packages/Rivulet.Http/) | [GitHub](https://github.com/Jeffeek/Rivulet)