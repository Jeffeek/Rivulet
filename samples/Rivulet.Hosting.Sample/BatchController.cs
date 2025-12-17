using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Rivulet.Core;

namespace Rivulet.Hosting.Sample;

// Properties are accessed via JSON serialization when returned from API endpoints
// ReSharper disable once MemberCanBeFileLocal
[
    SuppressMessage("ReSharper", "MemberCanBeInternal"),
    SuppressMessage("ReSharper", "NotAccessedPositionalProperty.Global")
]
public sealed record FetchResult(
    string Url,
    int StatusCode,
    bool Success,
    string? Error = null
);

[ApiController, Route("api/[controller]"), SuppressMessage("ReSharper", "ArrangeObjectCreationWhenTypeNotEvident")]
public class BatchController(
    IOptions<ParallelOptionsRivulet> options,
    ILogger<BatchController> logger
) : ControllerBase
{
    private readonly ParallelOptionsRivulet _options = options.Value;

    /// <summary>
    ///     Process a batch of numbers in parallel
    /// </summary>
    /// <param name="numbers">Array of numbers to process</param>
    /// <returns>Squared numbers</returns>
    [HttpPost("square")]
    public async Task<ActionResult<int[]>> SquareNumbers([FromBody] int[] numbers)
    {
        if (numbers.Length == 0) return BadRequest("Numbers array cannot be empty");

        logger.LogInformation("Processing {Count} numbers", numbers.Length);

        var results = await numbers.SelectParallelAsync(static async (num, ct) =>
            {
                // Simulate some async work
                await Task.Delay(10, ct);
                return num * num;
            },
            _options);

        return Ok(results.ToArray());
    }

    /// <summary>
    ///     Simulate fetching data from multiple URLs
    /// </summary>
    /// <param name="urls">Array of URLs to fetch</param>
    /// <returns>Status codes for each URL</returns>
    [HttpPost("fetch")]
    public async Task<ActionResult<object[]>> FetchUrls([FromBody] string[] urls)
    {
        if (urls.Length == 0) return BadRequest("URLs array cannot be empty");

        logger.LogInformation("Fetching {Count} URLs", urls.Length);

        using var httpClient = new HttpClient();

        var fetchOptions = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = _options.MaxDegreeOfParallelism,
            MaxRetries = 2,
            IsTransient = static ex => ex is HttpRequestException,
            ErrorMode = ErrorMode.CollectAndContinue
        };

        var results = await urls.SelectParallelAsync(
            async (url, ct) =>
            {
                try
                {
                    var response = await httpClient.GetAsync(url, ct);
                    return new FetchResult(url, (int)response.StatusCode, response.IsSuccessStatusCode);
                }
                catch (Exception ex)
                {
                    return new FetchResult(url, 0, false, ex.Message);
                }
            },
            fetchOptions);

        return Ok(results.ToArray());
    }

    /// <summary>
    ///     Process items in batches
    /// </summary>
    /// <param name="request">Batch processing request</param>
    /// <returns>Batch sums</returns>
    [HttpPost("batch-sum")]
    public async Task<ActionResult<int[]>> BatchSum([FromBody] BatchSumRequest request)
    {
        if (request.Numbers.Length == 0) return BadRequest("Numbers array cannot be empty");

        if (request.BatchSize <= 0) return BadRequest("Batch size must be greater than 0");

        logger.LogInformation(
            "Processing {Count} numbers in batches of {BatchSize}",
            request.Numbers.Length,
            request.BatchSize);

        var results = await request.Numbers.BatchParallelAsync(
            request.BatchSize,
            static async (batch, ct) =>
            {
                await Task.Delay(50, ct);
                return batch.Sum();
            },
            _options);

        return Ok(results.ToArray());
    }
}

public sealed record BatchSumRequest(int[] Numbers, int BatchSize);