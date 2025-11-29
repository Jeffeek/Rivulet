using System.Net;
using Rivulet.Base.Tests;

namespace Rivulet.Http.Tests;

/// <summary>
/// Additional tests to improve branch coverage for HTTP parallel extensions.
/// </summary>
public class HttpParallelExtensionsAdditionalTests
{
    private static HttpClient CreateTestClient(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        => TestHttpClientFactory.CreateTestClient(handler);

    [Fact]
    public async Task GetParallelAsync_WithRetryAfterHeader_ShouldRespectRetryDelay()
    {
        var uris = new[] { new Uri("http://test.local/rate-limited") };
        var attemptCount = 0;

        using var httpClient = CreateTestClient((_, _) =>
        {
            attemptCount++;
            if (attemptCount != 1)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("Success after retry")
                });

            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new(TimeSpan.FromMilliseconds(50));
            return Task.FromResult(response);
        });

        var options = new HttpOptions
        {
            RespectRetryAfterHeader = true,
            ParallelOptions = new()
            {
                MaxRetries = 3,
                BaseDelay = TimeSpan.FromMilliseconds(10)
            }
        };

        var results = await uris.GetParallelAsync(httpClient, options);

        attemptCount.Should().Be(2); // Initial + 1 retry
        results.Should().HaveCount(1);
        results[0].StatusCode.Should().Be(HttpStatusCode.OK);

        foreach (var response in results)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task GetParallelAsync_WithRetryAfterHeaderDisabled_ShouldNotWait()
    {
        var uris = new[] { new Uri("http://test.local/rate-limited") };
        var attemptCount = 0;

        using var httpClient = CreateTestClient((_, _) =>
        {
            attemptCount++;
            if (attemptCount != 1)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new(TimeSpan.FromSeconds(10));
            return Task.FromResult(response);

        });

        var options = new HttpOptions
        {
            RespectRetryAfterHeader = false,
            ParallelOptions = new()
            {
                MaxRetries = 3,
                BaseDelay = TimeSpan.FromMilliseconds(10)
            }
        };

        var results = await uris.GetParallelAsync(httpClient, options);

        attemptCount.Should().Be(2);
        results[0].StatusCode.Should().Be(HttpStatusCode.OK);

        foreach (var response in results)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task GetParallelAsync_WithRetryAfterDate_ShouldHandle()
    {

        var uris = new[] { new Uri("http://test.local/rate-limited") };
        var attemptCount = 0;

        using var httpClient = CreateTestClient((_, _) =>
        {
            attemptCount++;
            if (attemptCount != 1)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

            var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            response.Headers.RetryAfter = new(DateTimeOffset.UtcNow.AddSeconds(1));
            return Task.FromResult(response);

        });

        var options = new HttpOptions
        {
            RespectRetryAfterHeader = true,
            ParallelOptions = new()
            {
                MaxRetries = 3,
                BaseDelay = TimeSpan.FromMilliseconds(10)
            }
        };

        var results = await uris.GetParallelAsync(httpClient, options);

        attemptCount.Should().Be(2);
        results[0].StatusCode.Should().Be(HttpStatusCode.OK);

        foreach (var response in results)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task GetParallelAsync_WithHttpRequestException_ShouldInvokeErrorCallback()
    {
        var uris = new[] { new Uri("http://test.local/error") };
        Uri? callbackUri = null;
        HttpStatusCode? callbackStatus = null;

        using var httpClient = CreateTestClient((_, _) => throw new HttpRequestException("Network error", null, HttpStatusCode.BadGateway));

        var options = new HttpOptions
        {
            OnHttpErrorAsync = (uri, status, _) =>
            {
                callbackUri = uri;
                callbackStatus = status;
                return ValueTask.CompletedTask;
            },
            ParallelOptions = new()
            {
                ErrorMode = Core.ErrorMode.BestEffort,
                MaxRetries = 0
            }
        };

        await uris.GetParallelAsync(httpClient, options);

        callbackUri.Should().NotBeNull();
        callbackStatus.Should().Be(HttpStatusCode.BadGateway);
    }

    [Fact]
    public async Task GetParallelAsync_WithNonRetriable404_ShouldThrowAfterCallback()
    {
        var uris = new[] { new Uri("http://test.local/notfound") };
        var callbackInvoked = false;

        using var httpClient = CreateTestClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("Not found")
        }));

        var options = new HttpOptions
        {
            OnHttpErrorAsync = (_, _, _) =>
            {
                callbackInvoked = true;
                return ValueTask.CompletedTask;
            },
            ParallelOptions = new()
            {
                ErrorMode = Core.ErrorMode.BestEffort,
                MaxRetries = 0
            }
        };

        await uris.GetParallelAsync(httpClient, options);

        callbackInvoked.Should().BeTrue();
    }

    [Fact]
    public async Task DownloadToStreamAsync_WithoutContentLength_ShouldStillDownload()
    {
        var uri = new Uri("http://test.local/no-length.txt");
        var expectedContent = "Content without length";

        using var httpClient = CreateTestClient((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(expectedContent)
            };
            // Explicitly remove Content-Length header
            response.Content.Headers.ContentLength = null;
            return Task.FromResult(response);
        });

        using var destination = new MemoryStream();

        var options = new StreamingDownloadOptions
        {
            ValidateContentLength = false
        };

        var bytesDownloaded = await HttpStreamingExtensions.DownloadToStreamAsync(
            uri,
            destination,
            httpClient,
            options);

        bytesDownloaded.Should().Be(expectedContent.Length);
        destination.Position = 0;
        using var reader = new StreamReader(destination);
        var downloadedContent = await reader.ReadToEndAsync();
        downloadedContent.Should().Be(expectedContent);
    }

    [Fact]
    public async Task DownloadParallelAsync_WithOverwriteEnabled_ShouldOverwriteExisting()
    {
        var tempDir = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var filePath = Path.Join(tempDir, "overwrite.txt");
            await File.WriteAllTextAsync(filePath, "Old content");

            var downloads = new[]
            {
                (uri: new Uri("http://test.local/file.txt"), destinationPath: filePath)
            };

            using var httpClient = CreateTestClient((_, _) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("New content")
                };
                response.Content.Headers.ContentLength = 11;
                return Task.FromResult(response);
            });

            var options = new StreamingDownloadOptions
            {
                OverwriteExisting = true,
                EnableResume = false
            };

            var results = await downloads.DownloadParallelAsync(httpClient, options);

            results.Should().HaveCount(1);
            var newContent = await File.ReadAllTextAsync(filePath);
            newContent.Should().Be("New content");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task DownloadParallelAsync_WithServerNotSupportingResume_ShouldStartOver()
    {

        var tempDir = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var filePath = Path.Join(tempDir, "partial-no-resume.txt");
            const string partialContent = "Partial";
            await File.WriteAllTextAsync(filePath, partialContent);
            const string fullContent = "Partial is wrong - start over";

            var downloads = new[]
            {
                (uri: new Uri("http://test.local/file.txt"), destinationPath: filePath)
            };

            var headCalled = false;

            using var httpClient = CreateTestClient((request, _) =>
            {
                if (request.Method == HttpMethod.Head)
                {
                    headCalled = true;
                    var headResponse = new HttpResponseMessage(HttpStatusCode.OK);
                    // No Accept-Ranges header means no resume support
                    return Task.FromResult(headResponse);
                }

                // Server doesn't support range, so always return full content
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(fullContent)
                };
                response.Content.Headers.ContentLength = fullContent.Length;
                return Task.FromResult(response);
            });

            var options = new StreamingDownloadOptions
            {
                EnableResume = true
            };

            var results = await downloads.DownloadParallelAsync(httpClient, options);

            results.Should().HaveCount(1);
            headCalled.Should().BeTrue();
            var downloadedContent = await File.ReadAllTextAsync(filePath);
            downloadedContent.Should().Be(fullContent);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
