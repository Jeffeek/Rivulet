using Polly;
using Polly.Retry;
using Polly.CircuitBreaker;
using Rivulet.Core;
using Rivulet.Polly;

Console.WriteLine("=== Rivulet.Polly Sample ===\n");

using var httpClient = new HttpClient();

// Sample 1: SelectParallelWithPolicyAsync - Direct Polly policy integration
Console.WriteLine("1. SelectParallelWithPolicyAsync - Polly retry integration");

var retryPipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(1),
        BackoffType = DelayBackoffType.Exponential
    })
    .Build();

var urls = new[] { "https://httpbin.org/status/200", "https://httpbin.org/status/500" };

var results = await urls.SelectParallelWithPolicyAsync(
    async (url, ct) =>
    {
        var response = await httpClient.GetAsync(url, ct);
        return (url, statusCode: (int)response.StatusCode);
    },
    retryPipeline,
    new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 2,
        ErrorMode = ErrorMode.BestEffort
    });

Console.WriteLine($"✓ Processed {results.Count} URLs with Polly retry\n");

// Sample 2: Convert Rivulet options to Polly pipeline
Console.WriteLine("2. ToPollyPipeline - Convert Rivulet resilience settings to Polly");

var rivuletOptions = new ParallelOptionsRivulet
{
    MaxRetries = 3,
    IsTransient = ex => ex is HttpRequestException,
    BackoffStrategy = Rivulet.Core.Resilience.BackoffStrategy.ExponentialJitter,
    BaseDelay = TimeSpan.FromMilliseconds(500),
    PerItemTimeout = TimeSpan.FromSeconds(5)
};

var convertedPipeline = rivuletOptions.ToPollyPipeline();

var slowUrls = new[] { "https://httpbin.org/delay/1", "https://httpbin.org/get" };

var convertedResults = await slowUrls.SelectParallelWithPolicyAsync(
    async (url, ct) =>
    {
        var response = await httpClient.GetAsync(url, ct);
        return await response.Content.ReadAsStringAsync(ct);
    },
    convertedPipeline,
    new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 2,
        ErrorMode = ErrorMode.BestEffort
    });

Console.WriteLine($"✓ Processed {convertedResults.Count} URLs with converted pipeline\n");

// Sample 3: Circuit breaker with Polly
Console.WriteLine("3. Circuit breaker - Fail-fast protection");

var circuitBreakerPipeline = new ResiliencePipelineBuilder()
    .AddCircuitBreaker(new CircuitBreakerStrategyOptions
    {
        FailureRatio = 0.5,
        SamplingDuration = TimeSpan.FromSeconds(10),
        MinimumThroughput = 2,
        BreakDuration = TimeSpan.FromSeconds(5)
    })
    .Build();

var flakyUrls = Enumerable.Range(1, 10)
    .Select(i => $"https://httpbin.org/status/{(i % 3 == 0 ? 500 : 200)}")
    .ToArray();

try
{
    var cbResults = await flakyUrls.SelectParallelWithPolicyAsync(
        async (url, ct) =>
        {
            var response = await httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            return url;
        },
        circuitBreakerPipeline,
        new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 3,
            ErrorMode = ErrorMode.BestEffort
        });

    Console.WriteLine($"✓ Processed {cbResults.Count} URLs with circuit breaker\n");
}
catch (Exception ex)
{
    Console.WriteLine($"⚠ Circuit breaker triggered: {ex.GetType().Name}\n");
}

// Sample 4: Hedging pattern - Reduce tail latency
Console.WriteLine("4. SelectParallelWithHedgingAsync - Parallel requests for lowest latency");

var hedgingUrls = new[] { "https://httpbin.org/delay/2", "https://httpbin.org/get" };

var hedgingResults = await hedgingUrls.SelectParallelWithHedgingAsync(
    async (url, ct) =>
    {
        var response = await httpClient.GetAsync(url, ct);
        return await response.Content.ReadAsStringAsync(ct);
    },
    maxHedgedAttempts: 2,
    hedgingDelay: TimeSpan.FromMilliseconds(500),
    new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 2
    });

Console.WriteLine($"✓ Processed {hedgingResults.Count} URLs with hedging (reduced tail latency)\n");

// Sample 5: Result-based retry - Retry on specific HTTP status codes
Console.WriteLine("5. SelectParallelWithResultRetryAsync - Retry based on result value");

var statusUrls = new[] { "https://httpbin.org/status/503", "https://httpbin.org/status/200" };

var resultRetryResults = await statusUrls.SelectParallelWithResultRetryAsync(
    async (url, ct) =>
    {
        var response = await httpClient.GetAsync(url, ct);
        return response;
    },
    shouldRetry: response => !response.IsSuccessStatusCode,
    maxRetries: 2,
    delayBetweenRetries: TimeSpan.FromMilliseconds(500),
    new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 2,
        ErrorMode = ErrorMode.BestEffort
    });

Console.WriteLine($"✓ Processed {resultRetryResults.Count} URLs with result-based retry\n");

// Sample 6: Combined pipeline - Multiple resilience strategies
Console.WriteLine("6. Combined pipeline - Timeout + Retry + Circuit Breaker");

var combinedPipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
    .AddTimeout(TimeSpan.FromSeconds(10))
    .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
    {
        MaxRetryAttempts = 2,
        Delay = TimeSpan.FromMilliseconds(500),
        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .HandleResult(r => !r.IsSuccessStatusCode)
    })
    .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
    {
        FailureRatio = 0.5,
        SamplingDuration = TimeSpan.FromSeconds(10),
        MinimumThroughput = 2,
        BreakDuration = TimeSpan.FromSeconds(5),
        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .HandleResult(r => !r.IsSuccessStatusCode)
    })
    .Build();

var testUrls = new[] { "https://httpbin.org/get", "https://httpbin.org/delay/1" };

var combinedResults = await testUrls.SelectParallelWithPolicyAsync(
    async (url, ct) => await httpClient.GetAsync(url, ct),
    combinedPipeline,
    new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 2
    });

Console.WriteLine($"✓ Processed {combinedResults.Count} URLs with combined pipeline\n");

Console.WriteLine("=== Sample Complete ===");
Console.WriteLine("\nKey Features:");
Console.WriteLine("  - SelectParallelWithPolicyAsync: Apply any Polly ResiliencePipeline");
Console.WriteLine("  - ToPollyPipeline: Convert Rivulet settings to Polly");
Console.WriteLine("  - Hedging: Reduce tail latency with parallel requests");
Console.WriteLine("  - Result-based retry: Retry based on result values, not just exceptions");
Console.WriteLine("  - Combined pipelines: Stack multiple resilience strategies");
