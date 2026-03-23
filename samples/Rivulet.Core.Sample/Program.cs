using Rivulet.Core;

// ReSharper disable ArrangeObjectCreationWhenTypeNotEvident

Console.WriteLine("=== Rivulet.Core Sample ===\n");

// Sample 1: SelectParallelAsync - Process items and collect results
Console.WriteLine("1. SelectParallelAsync - HTTP requests with retry logic");
using var http = new HttpClient();
var urls = Enumerable
    .Range(0, 50)
    .Select(static i => $"https://httpbin.org/status/200?id={i}")
    .ToArray();

var results = await urls.SelectParallelAsync(
    async (url, ct) =>
    {
        using var resp = await http.GetAsync(url, ct);
        return (url, status: (int)resp.StatusCode);
    },
    new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 32,
        MaxRetries = 3,
        IsTransient = static ex => ex is HttpRequestException or TaskCanceledException,
        BaseDelay = TimeSpan.FromMilliseconds(200),
        ErrorMode = ErrorMode.CollectAndContinue
    });

Console.WriteLine($"✓ Processed {results.Count} URLs\n");

// Sample 2: SelectParallelStreamAsync - Stream results as they complete
Console.WriteLine("2. SelectParallelStreamAsync - Stream processing");
var numbers = Enumerable.Range(1, 20);
var streamResults = await numbers.ToAsyncEnumerable()
    .SelectParallelStreamAsync(static async (num, ct) =>
        {
            await Task.Delay(Random.Shared.Next(10, 50), ct);
            return num * num;
        },
        new ParallelOptionsRivulet { MaxDegreeOfParallelism = 5, OrderedOutput = false })
    .ToListAsync();

Console.WriteLine($"✓ Streamed {streamResults.Count} results\n");

// Sample 3: ForEachParallelAsync - Process items without collecting results
Console.WriteLine("3. ForEachParallelAsync - Fire and forget");
var items = Enumerable.Range(1, 10).ToAsyncEnumerable();

await items.ForEachParallelAsync(static async (item, ct) =>
    {
        await Task.Delay(100, ct);
        Console.WriteLine($"  Processed item {item}");
    },
    new ParallelOptionsRivulet { MaxDegreeOfParallelism = 3 });

Console.WriteLine("✓ All items processed\n");

// Sample 4: BatchParallelAsync - Process items in batches
Console.WriteLine("4. BatchParallelAsync - Batch processing");
var batchItems = Enumerable.Range(1, 100);

var batches = await batchItems.BatchParallelAsync(
    10,
    static async (batch, ct) =>
    {
        await Task.Delay(50, ct);
        return batch.Sum();
    },
    new ParallelOptionsRivulet { MaxDegreeOfParallelism = 4 });

Console.WriteLine($"✓ Processed {batches.Count} batches, total sum: {batches.Sum()}\n");

// Sample 5: Error handling modes
Console.WriteLine("5. Error handling - FailFast mode");
var faultyItems = Enumerable.Range(1, 20);

try
{
    await faultyItems.SelectParallelAsync(static async (item, ct) =>
        {
            await Task.Delay(10, ct);
            return item == 10 ? throw new InvalidOperationException($"Simulated error at item {item}") : item;
        },
        new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 5,
            ErrorMode = ErrorMode.FailFast
        });
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"✓ Caught expected error: {ex.Message}\n");
}

Console.WriteLine("=== All samples completed successfully ===");
