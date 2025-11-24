using Rivulet.Core;

Console.WriteLine("=== Rivulet.Http Sample ===\n");

using var httpClient = new HttpClient();

// Sample 1: Parallel HTTP GET requests
Console.WriteLine("1. SelectParallelAsync - Parallel HTTP GET requests");

var urls = new[]
{
    "https://httpbin.org/get",
    "https://httpbin.org/headers",
    "https://httpbin.org/ip",
    "https://httpbin.org/user-agent"
};

var responses = await urls.SelectParallelAsync(
    async (url, ct) =>
    {
        using var response = await httpClient.GetAsync(url, ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        return (url, statusCode: (int)response.StatusCode, contentLength: content.Length);
    },
    new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 4,
        MaxRetries = 3,
        IsTransient = ex => ex is HttpRequestException
    });

Console.WriteLine($"✓ Fetched {responses.Count} URLs successfully");
foreach (var (url, status, length) in responses)
{
    Console.WriteLine($"  {url}: {status} ({length} bytes)");
}
Console.WriteLine();

// Sample 2: POST requests with JSON payload
Console.WriteLine("2. POST requests with JSON - Send data to API");

var postData = Enumerable.Range(1, 3)
    .Select(i => new { id = i, title = $"Post {i}", body = $"Content {i}" })
    .ToList();

var postResults = await postData.SelectParallelAsync(
    async (data, ct) =>
    {
        var json = System.Text.Json.JsonSerializer.Serialize(data);
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var response = await httpClient.PostAsync("https://httpbin.org/post", content, ct);
        return (data.id, statusCode: (int)response.StatusCode);
    },
    new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 2
    });

Console.WriteLine($"✓ Posted {postResults.Count} items");
foreach (var (id, status) in postResults)
{
    Console.WriteLine($"  Post {id}: HTTP {status}");
}
Console.WriteLine();

// Sample 3: Download files
Console.WriteLine("3. Download files - Parallel file downloads");

var fileUrls = new[]
{
    ("https://httpbin.org/json", Path.Join(Path.GetTempPath(), "sample1.json")),
    ("https://httpbin.org/uuid", Path.Join(Path.GetTempPath(), "sample2.json")),
    ("https://httpbin.org/base64/aGVsbG8=", Path.Join(Path.GetTempPath(), "sample3.txt"))
};

var downloadResults = await fileUrls.SelectParallelAsync(
    async (item, ct) =>
    {
        var (url, filePath) = item;
        using var response = await httpClient.GetAsync(url, ct);
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        await File.WriteAllBytesAsync(filePath, bytes, ct);
        return (url, filePath, bytes.Length);
    },
    new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 3
    });

Console.WriteLine($"✓ Downloaded {downloadResults.Count} files");
foreach (var (_, path, size) in downloadResults)
{
    Console.WriteLine($"  {Path.GetFileName(path)}: {size} bytes");
}
Console.WriteLine();

// Cleanup
foreach (var (_, path) in fileUrls)
{
    if (File.Exists(path))
        File.Delete(path);
}

// Sample 4: Retry on failure
Console.WriteLine("4. HTTP requests with retry - Handle transient failures");

var unreliableUrls = new[]
{
    "https://httpbin.org/status/500",  // Will fail
    "https://httpbin.org/status/200"   // Will succeed
};

var retryResults = await unreliableUrls.SelectParallelAsync(
    async (url, ct) =>
    {
        using var response = await httpClient.GetAsync(url, ct);
        // Don't throw on non-success - just return the status
        return (url, statusCode: (int)response.StatusCode, success: response.IsSuccessStatusCode);
    },
    new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 2,
        MaxRetries = 2,
        ErrorMode = ErrorMode.BestEffort,
        BaseDelay = TimeSpan.FromMilliseconds(500)
    });

Console.WriteLine($"✓ Processed {retryResults.Count} requests with retry");
foreach (var (url, status, success) in retryResults)
{
    Console.WriteLine($"  {url}: HTTP {status} {(success ? "✓" : "✗")}");
}
Console.WriteLine();

Console.WriteLine("=== Sample Complete ===");
Console.WriteLine("\nKey Features:");
Console.WriteLine("  - Parallel HTTP operations with bounded concurrency");
Console.WriteLine("  - Automatic retries on transient failures");
Console.WriteLine("  - Progress tracking and error handling");
Console.WriteLine("  - Works with any HTTP operations (GET, POST, etc.)");
