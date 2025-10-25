using Rivulet.Core;

var http = new HttpClient();
var urls = Enumerable.Range(0, 1000).Select(i => $"https://example.org/?q={i}").ToArray();

// Materialize results
var list = await urls.SelectParallelAsync(
    async (url, ct) =>
    {
        using var resp = await http.GetAsync(url, ct);
        return (url, status: (int)resp.StatusCode);
    },
    new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 32,
        MaxRetries = 3,
        IsTransient = ex => ex is HttpRequestException or TaskCanceledException,
        BaseDelay = TimeSpan.FromMilliseconds(200),
        ErrorMode = ErrorMode.CollectAndContinue
    });

Console.WriteLine($"Got {list.Count} results");

// Streaming pipeline
await foreach (var res in urls.ToAsyncEnumerable().SelectParallelStreamAsync(
                   async (url, ct) =>
                   {
                       await Task.Delay(10, ct);
                       return url.Length;
                   },
                   new ParallelOptionsRivulet { MaxDegreeOfParallelism = 16 }))
{
    // consume incrementally
    Console.WriteLine(res);
}
