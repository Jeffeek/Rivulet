using System.Net;
using System.Text;
using Rivulet.Base.Tests;
using Rivulet.Core;

namespace Rivulet.Http.Tests;

public class HttpParallelExtensionsTests
{
    private static HttpClient CreateTestClient(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        => TestHttpClientFactory.CreateTestClient(handler);

    [Fact]
    public async Task GetParallelAsync_WithValidUris_ShouldReturnResponses()
    {
        var uris = new[] { new Uri("http://test.local/1"), new Uri("http://test.local/2"), new Uri("http://test.local/3") };

        using var httpClient = CreateTestClient((request, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"Response for {request.RequestUri}") };
            return Task.FromResult(response);
        });

        var results = await uris.GetParallelAsync(httpClient);

        results.Count.ShouldBe(3);
        results.ShouldAllBe(r => r.StatusCode == HttpStatusCode.OK);

        foreach (var response in results) response.Dispose();
    }

    [Fact]
    public async Task GetParallelAsync_WithNullUris_ShouldThrowArgumentNullException()
    {
        IEnumerable<Uri> uris = null!;
        using var httpClient = new HttpClient();

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await uris.GetParallelAsync(httpClient));
    }

    [Fact]
    public async Task GetParallelAsync_WithNullHttpClient_ShouldThrowArgumentNullException()
    {
        var uris = new[] { new Uri("http://test.local") };

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await uris.GetParallelAsync((HttpClient)null!));
    }

    [Fact]
    public async Task GetStringParallelAsync_WithValidUris_ShouldReturnStrings()
    {
        var uris = new[] { new Uri("http://test.local/1"), new Uri("http://test.local/2") };

        using var httpClient = CreateTestClient((request, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"Content-{request.RequestUri!.AbsolutePath}")
            };
            return Task.FromResult(response);
        });

        var results = await uris.GetStringParallelAsync(httpClient);

        results.Count.ShouldBe(2);
        results.ShouldContain("Content-/1");
        results.ShouldContain("Content-/2");
    }

    [Fact]
    public async Task GetByteArrayParallelAsync_WithValidUris_ShouldReturnByteArrays()
    {
        var uris = new[] { new Uri("http://test.local/1"), new Uri("http://test.local/2") };

        using var httpClient = CreateTestClient((request, _) =>
        {
            var content = Encoding.UTF8.GetBytes($"Binary-{request.RequestUri!.AbsolutePath}");
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) };
            return Task.FromResult(response);
        });

        var results = await uris.GetByteArrayParallelAsync(httpClient);

        results.Count.ShouldBe(2);
        Encoding.UTF8.GetString(results[0]).ShouldContain("Binary-/");
        Encoding.UTF8.GetString(results[1]).ShouldContain("Binary-/");
    }

    [Fact]
    public async Task PostParallelAsync_WithValidRequests_ShouldReturnResponses()
    {
        var requests = new[]
        {
            (uri: new("http://test.local/post1"), content: new("data1")),
            (uri: new Uri("http://test.local/post2"), content: new StringContent("data2"))
        };

        using var httpClient = CreateTestClient(async (request, ct) =>
        {
            var requestBody = await request.Content!.ReadAsStringAsync(ct);
            var response = new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent($"Created with {requestBody}") };
            return response;
        });

        var results = await requests.PostParallelAsync(httpClient);

        results.Count.ShouldBe(2);
        results.ShouldAllBe(r => r.StatusCode == HttpStatusCode.Created);

        foreach (var response in results) response.Dispose();
    }

    [Fact]
    public async Task PutParallelAsync_WithValidRequests_ShouldReturnResponses()
    {
        var requests = new[]
        {
            (uri: new("http://test.local/put1"), content: new StringContent("update1")),
            (uri: new Uri("http://test.local/put2"), content: (HttpContent)new StringContent("update2"))
        };

        using var httpClient = CreateTestClient((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Updated") };
            return Task.FromResult(response);
        });

        var results = await requests.PutParallelAsync(httpClient);

        results.Count.ShouldBe(2);
        results.ShouldAllBe(r => r.StatusCode == HttpStatusCode.OK);

        foreach (var response in results) response.Dispose();
    }

    [Fact]
    public async Task DeleteParallelAsync_WithValidUris_ShouldReturnResponses()
    {
        var uris = new[] { new Uri("http://test.local/delete1"), new Uri("http://test.local/delete2") };

        using var httpClient = CreateTestClient(static (_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.NoContent);
            return Task.FromResult(response);
        });

        var results = await uris.DeleteParallelAsync(httpClient);

        results.Count.ShouldBe(2);
        results.ShouldAllBe(static r => r.StatusCode == HttpStatusCode.NoContent);

        foreach (var response in results) response.Dispose();
    }

    [Fact]
    public async Task GetParallelAsync_WithTransientError_ShouldRetry()
    {
        var uris = new[] { new Uri("http://test.local/retry") };
        var attemptCount = 0;

        using var httpClient = CreateTestClient((_, _) =>
        {
            attemptCount++;
            if (attemptCount < 2) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Success") });
        });

        var options = new HttpOptions { ParallelOptions = new() { MaxRetries = 3, BaseDelay = TimeSpan.FromMilliseconds(10) } };

        var results = await uris.GetParallelAsync(httpClient, options);

        attemptCount.ShouldBe(2); // Initial attempt + 1 retry
        results.Count.ShouldBe(1);
        results[0].StatusCode.ShouldBe(HttpStatusCode.OK);

        // Cleanup
        foreach (var response in results) response.Dispose();
    }

    [Fact]
    public async Task GetParallelAsync_WithNonRetriableError_ShouldNotRetry()
    {
        var uris = new[] { new Uri("http://test.local/notfound") };
        var attemptCount = 0;

        using var httpClient = CreateTestClient((_, _) =>
        {
            attemptCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        var options = new HttpOptions { ParallelOptions = new() { MaxRetries = 3, ErrorMode = ErrorMode.BestEffort } };

        await uris.GetParallelAsync(httpClient, options);

        attemptCount.ShouldBe(1); // Only initial attempt, no retries
    }

    [Fact]
    public async Task GetParallelAsync_WithHttpErrorCallback_ShouldInvokeCallback()
    {
        var uris = new[] { new Uri("http://test.local/error") };
        Uri? callbackUri = null;
        HttpStatusCode? callbackStatus = null;

        using var httpClient = CreateTestClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var options = new HttpOptions
        {
            OnHttpErrorAsync = (uri, status, _) =>
            {
                callbackUri = uri;
                callbackStatus = status;
                return ValueTask.CompletedTask;
            },
            ParallelOptions = new() { ErrorMode = ErrorMode.BestEffort, MaxRetries = 0 }
        };

        await uris.GetParallelAsync(httpClient, options);

        callbackUri.ShouldNotBeNull();
        callbackUri!.ToString().ShouldContain("/error");
        callbackStatus.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetParallelAsync_WithCancellation_ShouldThrowOperationCanceledException()
    {
        var uris = Enumerable.Range(1, 10).Select(static i => new Uri($"http://test.local/{i}"));
        using var cts = new CancellationTokenSource();

        using var httpClient = CreateTestClient(async (_, ct) =>
        {
            await Task.Delay(100, CancellationToken.None); // Use CancellationToken.None to avoid canceling delay
            ct.ThrowIfCancellationRequested();
            return new(HttpStatusCode.OK);
        });

        // Cancel after a short delay
        cts.CancelAfter(50);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await uris.GetParallelAsync(httpClient, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task GetParallelAsync_WithCustomParallelism_ShouldRespectConcurrencyLimit()
    {
        var uris = Enumerable.Range(1, 20).Select(static i => new Uri($"http://test.local/{i}")).ToList();
        var maxConcurrent = 0;
        var currentConcurrent = 0;
        var lockObj = new object();

        using var httpClient = CreateTestClient(async (_, _) =>
        {
            lock (lockObj)
            {
                currentConcurrent++;
                maxConcurrent = Math.Max(maxConcurrent, currentConcurrent);
            }

            await Task.Delay(50, CancellationToken.None);

            lock (lockObj) currentConcurrent--;

            return new(HttpStatusCode.OK) { Content = new StringContent("OK") };
        });

        var options = new HttpOptions { ParallelOptions = new() { MaxDegreeOfParallelism = 5 } };

        var results = await uris.GetParallelAsync(httpClient, options);

        results.Count.ShouldBe(20);
        maxConcurrent.ShouldBeLessThanOrEqualTo(5);

        foreach (var response in results) response.Dispose();
    }
}