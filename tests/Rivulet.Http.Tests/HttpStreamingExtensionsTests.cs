using System.Net;
using Rivulet.Base.Tests;

namespace Rivulet.Http.Tests;

public sealed class HttpStreamingExtensionsTests
{
    private static HttpClient CreateTestClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        => TestHttpClientFactory.CreateTestClient(handler);

    [Fact]
    public async Task DownloadToStreamAsync_WithValidUri_ShouldDownloadContent()
    {
        var uri = new Uri("http://test.local/file.txt");
        const string expectedContent = "Test file content";

        using var httpClient = CreateTestClient(static (_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(expectedContent) };
            response.Content.Headers.ContentLength = expectedContent.Length;
            return Task.FromResult(response);
        });

        using var destination = new MemoryStream();

        var bytesDownloaded = await HttpStreamingExtensions.DownloadToStreamAsync(
            uri,
            destination,
            httpClient);

        bytesDownloaded.ShouldBe(expectedContent.Length);
        destination.Position = 0;
        using var reader = new StreamReader(destination);
        var downloadedContent = await reader.ReadToEndAsync();
        downloadedContent.ShouldBe(expectedContent);
    }

    [Fact]
    public async Task DownloadToStreamAsync_WithNullUri_ShouldThrowArgumentNullException()
    {
        using var httpClient = new HttpClient();
        using var destination = new MemoryStream();

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await HttpStreamingExtensions.DownloadToStreamAsync(
            null!,
            destination,
            httpClient));
    }

    [Fact]
    public async Task DownloadToStreamAsync_WithNullDestination_ShouldThrowArgumentNullException()
    {
        var uri = new Uri("http://test.local/file.txt");
        using var httpClient = new HttpClient();

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await HttpStreamingExtensions.DownloadToStreamAsync(
            uri,
            null!,
            httpClient));
    }

    [Fact]
    public async Task DownloadToStreamAsync_WithProgressCallback_ShouldReportProgress()
    {
        var uri = new Uri("http://test.local/largefile.bin");
        const int fileSize = 100000;
        var fileContent = new byte[fileSize];
        new Random().NextBytes(fileContent);

        var progressReports = new List<(long downloaded, long? total)>();

        using var httpClient = CreateTestClient((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(fileContent) };
            response.Content.Headers.ContentLength = fileSize;
            return Task.FromResult(response);
        });

        using var destination = new MemoryStream();

        var options = new StreamingDownloadOptions
        {
            BufferSize = 8192,
            ProgressInterval = TimeSpan.FromMilliseconds(1),
            OnProgressAsync = (_, downloaded, total) =>
            {
                progressReports.Add((downloaded, total));
                return ValueTask.CompletedTask;
            }
        };

        var bytesDownloaded = await HttpStreamingExtensions.DownloadToStreamAsync(
            uri,
            destination,
            httpClient,
            options);

        bytesDownloaded.ShouldBe(fileSize);
        progressReports.ShouldNotBeEmpty();
        progressReports.Last().downloaded.ShouldBe(fileSize);
        progressReports.Last().total.ShouldBe(fileSize);
    }

    [Fact]
    public async Task DownloadToStreamAsync_WithContentLengthMismatch_ShouldThrowHttpRequestException()
    {
        var uri = new Uri("http://test.local/corrupted.txt");
        const string actualContent = "Short content";
        const long claimedLength = 1000L;

        using var httpClient = CreateTestClient(static (_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(actualContent) };
            response.Content.Headers.ContentLength = claimedLength; // Lie about content length
            return Task.FromResult(response);
        });

        using var destination = new MemoryStream();

        var options = new StreamingDownloadOptions { ValidateContentLength = true };

        var exception = await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await HttpStreamingExtensions.DownloadToStreamAsync(
                uri,
                destination,
                httpClient,
                options));

        exception.Message.ShouldContain("Content length mismatch");
    }

    [Fact]
    public async Task DownloadParallelAsync_WithValidDownloads_ShouldDownloadAllFiles()
    {
        var tempDir = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var downloads = new[]
            {
                (uri: new("http://test.local/file1.txt"), destinationPath: Path.Join(tempDir, "file1.txt")),
                (uri: new Uri("http://test.local/file2.txt"), destinationPath: Path.Join(tempDir, "file2.txt"))
            };

            using var httpClient = CreateTestClient(static (request, _) =>
            {
                var content = $"Content for {request.RequestUri!.AbsolutePath}";
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) };
                response.Content.Headers.ContentLength = content.Length;
                return Task.FromResult(response);
            });

            var results = await downloads.DownloadParallelAsync(httpClient);

            results.Count.ShouldBe(2);
            results.ShouldAllBe(static r => r.bytesDownloaded > 0);

            File.Exists(Path.Join(tempDir, "file1.txt")).ShouldBeTrue();
            File.Exists(Path.Join(tempDir, "file2.txt")).ShouldBeTrue();

            var content1 = await File.ReadAllTextAsync(Path.Join(tempDir, "file1.txt"));
            content1.ShouldContain("file1.txt");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task DownloadParallelAsync_WithExistingFileAndOverwriteFalse_ShouldThrowIOException()
    {
        var tempDir = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var filePath = Path.Join(tempDir, "existing.txt");
            await File.WriteAllTextAsync(filePath, "Existing content");

            var downloads = new[] { (uri: new Uri("http://test.local/file.txt"), destinationPath: filePath) };

            using var httpClient = CreateTestClient(static (_, _) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = new StringContent("New content") };
                return Task.FromResult(response);
            });

            var options = new StreamingDownloadOptions { OverwriteExisting = false, EnableResume = false };

            await Assert.ThrowsAsync<IOException>(async () =>
                await downloads.DownloadParallelAsync(httpClient, options));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task DownloadParallelAsync_WithResumeSupport_ShouldResumePartialDownload()
    {
        var tempDir = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var filePath = Path.Join(tempDir, "partial.txt");
            const string partialContent = "Partial ";
            await File.WriteAllTextAsync(filePath, partialContent);

            const string fullContent = "Partial content";
            const string remainingContent = "content";

            var downloads = new[] { (uri: new Uri("http://test.local/file.txt"), destinationPath: filePath) };

            var headCalled = false;
            var getRangeCalled = false;

            using var httpClient = CreateTestClient((request, _) =>
            {
                if (request.Method == HttpMethod.Head)
                {
                    headCalled = true;
                    var headResponse = new HttpResponseMessage(HttpStatusCode.OK);
                    headResponse.Headers.AcceptRanges.Add("bytes");
                    return Task.FromResult(headResponse);
                }

                if (request.Headers.Range != null)
                {
                    getRangeCalled = true;
                    var rangeResponse = new HttpResponseMessage(HttpStatusCode.PartialContent)
                    {
                        Content = new StringContent(remainingContent)
                    };
                    rangeResponse.Content.Headers.ContentLength = remainingContent.Length;
                    return Task.FromResult(rangeResponse);
                }

                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(fullContent) };
                response.Content.Headers.ContentLength = fullContent.Length;
                return Task.FromResult(response);
            });

            var options = new StreamingDownloadOptions { EnableResume = true };

            var results = await downloads.DownloadParallelAsync(httpClient, options);

            results.Count.ShouldBe(1);
            headCalled.ShouldBeTrue();
            getRangeCalled.ShouldBeTrue();

            var downloadedContent = await File.ReadAllTextAsync(filePath);
            downloadedContent.ShouldBe(fullContent);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task DownloadParallelAsync_WithCompletedFile_ShouldReturnExistingSize()
    {
        var tempDir = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var filePath = Path.Join(tempDir, "complete.txt");
            const string content = "Complete file";
            await File.WriteAllTextAsync(filePath, content);

            var downloads = new[] { (uri: new Uri("http://test.local/file.txt"), destinationPath: filePath) };

            using var httpClient = CreateTestClient(static (request, _) =>
            {
                if (request.Method == HttpMethod.Head)
                {
                    var headResponse = new HttpResponseMessage(HttpStatusCode.OK);
                    headResponse.Headers.AcceptRanges.Add("bytes");
                    return Task.FromResult(headResponse);
                }

                if (request.Headers.Range != null)
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.RequestedRangeNotSatisfiable));

                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) };
                return Task.FromResult(response);
            });

            var options = new StreamingDownloadOptions { EnableResume = true };

            var results = await downloads.DownloadParallelAsync(httpClient, options);

            results.Count.ShouldBe(1);
            results[0].bytesDownloaded.ShouldBe(content.Length);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task DownloadParallelAsync_WithCallbacks_ShouldInvokeAllCallbacks()
    {
        var tempDir = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var filePath = Path.Join(tempDir, "callback.txt");
            var downloads = new[] { (uri: new Uri("http://test.local/file.txt"), destinationPath: filePath) };

            using var httpClient = CreateTestClient(static (_, _) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Content") };
                response.Content.Headers.ContentLength = 7;
                return Task.FromResult(response);
            });

            var progressCalled = false;
            var completeCalled = false;

            var options = new StreamingDownloadOptions
            {
                OnProgressAsync = (_, _, _) =>
                {
                    progressCalled = true;
                    return ValueTask.CompletedTask;
                },
                OnCompleteAsync = (_, _, _) =>
                {
                    completeCalled = true;
                    return ValueTask.CompletedTask;
                },
                ProgressInterval = TimeSpan.FromMilliseconds(1)
            };

            await downloads.DownloadParallelAsync(httpClient, options);

            progressCalled.ShouldBeTrue();
            completeCalled.ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}