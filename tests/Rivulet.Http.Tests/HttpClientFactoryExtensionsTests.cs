using System.Net;
using Microsoft.Extensions.DependencyInjection;

namespace Rivulet.Http.Tests;

public class HttpClientFactoryExtensionsTests
{
    private static IHttpClientFactory CreateTestFactory(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        var services = new ServiceCollection();
        services.AddHttpClient("test")
            .ConfigurePrimaryHttpMessageHandler(() => new TestHttpMessageHandler(handler));

        services.AddHttpClient(string.Empty)
            .ConfigurePrimaryHttpMessageHandler(() => new TestHttpMessageHandler(handler));

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IHttpClientFactory>();
    }

    [Fact]
    public async Task GetParallelAsync_WithNamedClient_ShouldUseCorrectClient()
    {

        var uris = new[]
        {
            new Uri("http://test.local/1"),
            new Uri("http://test.local/2")
        };

        var factory = CreateTestFactory((request, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"Response for {request.RequestUri}")
            };
            return Task.FromResult(response);
        });

        var results = await uris.GetParallelAsync(factory, "test");

        results.Should().HaveCount(2);
        results.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);

        foreach (var response in results)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task GetParallelAsync_WithDefaultClient_ShouldWork()
    {

        var uris = new[]
        {
            new Uri("http://test.local/1"),
            new Uri("http://test.local/2")
        };

        var factory = CreateTestFactory((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("OK")
            };
            return Task.FromResult(response);
        });

        var results = await uris.GetParallelAsync(factory);

        results.Should().HaveCount(2);
        results.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);

        foreach (var response in results)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task GetParallelAsync_WithNullFactory_ShouldThrowArgumentNullException()
    {
        var uris = new[] { new Uri("http://test.local") };

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await uris.GetParallelAsync((IHttpClientFactory)null!));
    }

    [Fact]
    public async Task GetStringParallelAsync_WithNamedClient_ShouldReturnStrings()
    {
        var uris = new[]
        {
            new Uri("http://test.local/1"),
            new Uri("http://test.local/2")
        };

        var factory = CreateTestFactory((request, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"String-{request.RequestUri!.AbsolutePath}")
            };
            return Task.FromResult(response);
        });

        var results = await uris.GetStringParallelAsync(factory, "test");

        results.Should().HaveCount(2);
        results.Should().Contain("String-/1");
        results.Should().Contain("String-/2");
    }

    [Fact]
    public async Task GetByteArrayParallelAsync_WithNamedClient_ShouldReturnByteArrays()
    {
        var uris = new[]
        {
            new Uri("http://test.local/1"),
            new Uri("http://test.local/2")
        };

        var factory = CreateTestFactory((request, _) =>
        {
            var content = System.Text.Encoding.UTF8.GetBytes($"Bytes-{request.RequestUri!.AbsolutePath}");
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content)
            };
            return Task.FromResult(response);
        });

        var results = await uris.GetByteArrayParallelAsync(factory, "test");

        results.Should().HaveCount(2);
        System.Text.Encoding.UTF8.GetString(results[0]).Should().Contain("Bytes-/");
        System.Text.Encoding.UTF8.GetString(results[1]).Should().Contain("Bytes-/");
    }

    [Fact]
    public async Task PostParallelAsync_WithNamedClient_ShouldWork()
    {
        var requests = new[]
        {
            (uri: new("http://test.local/post1"), content: new("data1")),
            (uri: new Uri("http://test.local/post2"), content: new StringContent("data2"))
        };

        var factory = CreateTestFactory((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("Created")
            };
            return Task.FromResult(response);
        });

        var results = await requests.PostParallelAsync(factory, "test");

        results.Should().HaveCount(2);
        results.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Created);

        foreach (var response in results)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task PutParallelAsync_WithNamedClient_ShouldWork()
    {
        var requests = new[]
        {
            (uri: new("http://test.local/put1"), content: new StringContent("update1")),
            (uri: new Uri("http://test.local/put2"), content: (HttpContent)new StringContent("update2"))
        };

        var factory = CreateTestFactory((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("Updated")
            };
            return Task.FromResult(response);
        });

        var results = await requests.PutParallelAsync(factory, "test");

        results.Should().HaveCount(2);
        results.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);

        foreach (var response in results)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task DeleteParallelAsync_WithNamedClient_ShouldWork()
    {
        var uris = new[]
        {
            new Uri("http://test.local/delete1"),
            new Uri("http://test.local/delete2")
        };

        var factory = CreateTestFactory((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.NoContent);
            return Task.FromResult(response);
        });

        var results = await uris.DeleteParallelAsync(factory, "test");

        results.Should().HaveCount(2);
        results.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.NoContent);

        foreach (var response in results)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task DownloadParallelAsync_WithNamedClient_ShouldDownloadFiles()
    {
        var tempDir = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var downloads = new[]
            {
                (uri: new Uri("http://test.local/file1.txt"), destinationPath: Path.Join(tempDir, "file1.txt"))
            };

            var factory = CreateTestFactory((request, _) =>
            {
                var content = $"Content for {request.RequestUri!.AbsolutePath}";
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(content)
                };
                response.Content.Headers.ContentLength = content.Length;
                return Task.FromResult(response);
            });

            var results = await downloads.DownloadParallelAsync(factory, "test");

            results.Should().HaveCount(1);
            results[0].bytesDownloaded.Should().BeGreaterThan(0);
            File.Exists(Path.Join(tempDir, "file1.txt")).Should().BeTrue();
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task GetParallelAsync_WithHttpOptions_ShouldPassOptionsCorrectly()
    {
        var uris = new[]
        {
            new Uri("http://test.local/1"),
            new Uri("http://test.local/2")
        };

        var attemptCount = 0;
        var factory = CreateTestFactory((_, _) =>
        {
            attemptCount++;
            if (attemptCount < 3)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("Success")
            });
        });

        var options = new HttpOptions
        {
            ParallelOptions = new()
            {
                MaxRetries = 3,
                BaseDelay = TimeSpan.FromMilliseconds(10)
            }
        };

        var results = await uris.GetParallelAsync(factory, "test", options);

        attemptCount.Should().BeGreaterThan(2); // At least initial + retries
        results.Should().HaveCount(2);

        // Cleanup
        foreach (var response in results)
        {
            response.Dispose();
        }
    }

    // Test helper class
    private class TestHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return handler(request, cancellationToken);
        }
    }
}
