using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Rivulet.Http.Internal;

namespace Rivulet.Http.Tests.Internal;

[
    SuppressMessage("ReSharper", "ArgumentsStyleLiteral"),
    SuppressMessage("ReSharper", "ArgumentsStyleNamedExpression")
]
public sealed class HttpHelperTests
{
    [Fact]
    public void InitializeOptions_WithNullOptions_ShouldCreateDefaultOptions()
    {
        var (options, parallelOptions) = HttpHelper.InitializeOptions(null);

        options.ShouldNotBeNull();
        options.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(30));
        parallelOptions.ShouldNotBeNull();
    }

    [Fact]
    public void InitializeOptions_WithProvidedOptions_ShouldUseProvidedOptions()
    {
        var customOptions = new HttpOptions
        {
            RequestTimeout = TimeSpan.FromSeconds(60),
            BufferSize = 16384
        };

        var (options, parallelOptions) = HttpHelper.InitializeOptions(customOptions);

        options.ShouldBe(customOptions);
        options.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(60));
        options.BufferSize.ShouldBe(16384);
        parallelOptions.ShouldNotBeNull();
    }

    [Fact]
    public void InitializeOptions_ShouldReturnMergedParallelOptions()
    {
        var customOptions = new HttpOptions
        {
            ParallelOptions = new()
            {
                MaxDegreeOfParallelism = 10,
                MaxRetries = 5
            }
        };

        var (_, parallelOptions) = HttpHelper.InitializeOptions(customOptions);

        parallelOptions.MaxDegreeOfParallelism.ShouldBe(10);
        parallelOptions.MaxRetries.ShouldBe(5);
    }

    [Fact]
    public void CreateClient_WithNullClientName_ShouldCreateDefaultClient()
    {
        var services = new ServiceCollection();
        services.AddHttpClient();
        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IHttpClientFactory>();

        var client = HttpHelper.CreateClient(factory, null);

        client.ShouldNotBeNull();
    }

    [Fact]
    public void CreateClient_WithEmptyClientName_ShouldCreateDefaultClient()
    {
        var services = new ServiceCollection();
        services.AddHttpClient();
        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IHttpClientFactory>();

        var client = HttpHelper.CreateClient(factory, string.Empty);

        client.ShouldNotBeNull();
    }

    [Fact]
    public void CreateClient_WithNamedClient_ShouldCreateNamedClient()
    {
        var services = new ServiceCollection();
        services.AddHttpClient("TestClient",
            static client =>
            {
                client.BaseAddress = new Uri("https://api.example.com");
                client.Timeout = TimeSpan.FromSeconds(120);
            });
        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IHttpClientFactory>();

        var client = HttpHelper.CreateClient(factory, "TestClient");

        client.ShouldNotBeNull();
        client.BaseAddress?.ToString().ShouldBe("https://api.example.com/");
        client.Timeout.ShouldBe(TimeSpan.FromSeconds(120));
    }

    [Fact]
    public void InitializeProgressTracking_WithDefaultParameters_ShouldInitializeCorrectly()
    {
        var options = new StreamingDownloadOptions
        {
            BufferSize = 8192,
            ProgressInterval = TimeSpan.FromSeconds(1)
        };

        var (bytesDownloaded, buffer, lastProgressReport, progressIntervalTicks) =
            HttpHelper.InitializeProgressTracking(options);

        bytesDownloaded.ShouldBe(0);
        buffer.Length.ShouldBe(8192);
        lastProgressReport.ShouldBeGreaterThan(0);
        progressIntervalTicks.ShouldBe(TimeSpan.FromSeconds(1).Ticks);
    }

    [Fact]
    public void InitializeProgressTracking_WithExistingFileSize_ShouldSetBytesDownloaded()
    {
        var options = new StreamingDownloadOptions
        {
            BufferSize = 16384,
            ProgressInterval = TimeSpan.FromMilliseconds(500)
        };

        var (bytesDownloaded, buffer, lastProgressReport, progressIntervalTicks) =
            HttpHelper.InitializeProgressTracking(options, existingFileSize: 1024);

        bytesDownloaded.ShouldBe(1024);
        buffer.Length.ShouldBe(16384);
        lastProgressReport.ShouldBeGreaterThan(0);
        progressIntervalTicks.ShouldBe(TimeSpan.FromMilliseconds(500).Ticks);
    }

    [Fact]
    public void InitializeProgressTracking_WithCustomBufferSize_ShouldCreateCorrectBuffer()
    {
        var options = new StreamingDownloadOptions
        {
            BufferSize = 32768
        };

        var (_, buffer, _, _) = HttpHelper.InitializeProgressTracking(options);

        buffer.Length.ShouldBe(32768);
    }

    [Fact]
    public async Task ReportProgressIfNeededAsync_WithNullCallback_ShouldReturnSameLastProgressReport()
    {
        var options = new StreamingDownloadOptions
        {
            OnProgressAsync = null
        };
        var uri = new Uri("https://example.com/file.zip");
        var lastProgressReport = System.Diagnostics.Stopwatch.GetTimestamp();

        var newLastProgressReport = await HttpHelper.ReportProgressIfNeededAsync(
            options,
            uri,
            bytesDownloaded: 1000,
            totalBytes: 10000,
            lastProgressReport,
            progressIntervalTicks: TimeSpan.FromSeconds(1).Ticks);

        newLastProgressReport.ShouldBe(lastProgressReport);
    }

    [Fact]
    public async Task ReportProgressIfNeededAsync_WithCallbackAndShortElapsedTime_ShouldNotInvokeCallback()
    {
        var callbackInvoked = false;
        var options = new StreamingDownloadOptions
        {
            OnProgressAsync = (_, _, _) =>
            {
                callbackInvoked = true;
                return ValueTask.CompletedTask;
            },
            ProgressInterval = TimeSpan.FromSeconds(10) // Very long interval
        };
        var uri = new Uri("https://example.com/file.zip");
        var lastProgressReport = System.Diagnostics.Stopwatch.GetTimestamp();

        var newLastProgressReport = await HttpHelper.ReportProgressIfNeededAsync(
            options,
            uri,
            bytesDownloaded: 1000,
            totalBytes: 10000,
            lastProgressReport,
            progressIntervalTicks: options.ProgressInterval.Ticks);

        callbackInvoked.ShouldBeFalse();
        newLastProgressReport.ShouldBe(lastProgressReport);
    }

    [Fact]
    public async Task ReportProgressIfNeededAsync_WithCallbackAndSufficientElapsedTime_ShouldInvokeCallback()
    {
        var callbackInvoked = false;
        Uri? reportedUri = null;
        long reportedBytes = 0;
        long? reportedTotalBytes = null;

        var options = new StreamingDownloadOptions
        {
            OnProgressAsync = (uri, bytes, totalBytes) =>
            {
                callbackInvoked = true;
                reportedUri = uri;
                reportedBytes = bytes;
                reportedTotalBytes = totalBytes;
                return ValueTask.CompletedTask;
            },
            ProgressInterval = TimeSpan.FromMilliseconds(10)
        };
        var uri = new Uri("https://example.com/file.zip");
        var lastProgressReport = System.Diagnostics.Stopwatch.GetTimestamp() - TimeSpan.FromSeconds(1).Ticks * System.Diagnostics.Stopwatch.Frequency / TimeSpan.TicksPerSecond;

        var newLastProgressReport = await HttpHelper.ReportProgressIfNeededAsync(
            options,
            uri,
            bytesDownloaded: 5000,
            totalBytes: 10000,
            lastProgressReport,
            progressIntervalTicks: options.ProgressInterval.Ticks);

        callbackInvoked.ShouldBeTrue();
        reportedUri.ShouldBe(uri);
        reportedBytes.ShouldBe(5000);
        reportedTotalBytes.ShouldBe(10000);
        newLastProgressReport.ShouldBeGreaterThan(lastProgressReport);
    }

    [Fact]
    public async Task ReportProgressIfNeededAsync_WithNullTotalBytes_ShouldStillInvokeCallback()
    {
        var callbackInvoked = false;
        long? reportedTotalBytes = -1; // Sentinel value

        var options = new StreamingDownloadOptions
        {
            OnProgressAsync = (_, _, totalBytes) =>
            {
                callbackInvoked = true;
                reportedTotalBytes = totalBytes;
                return ValueTask.CompletedTask;
            },
            ProgressInterval = TimeSpan.FromMilliseconds(10)
        };
        var uri = new Uri("https://example.com/file.zip");
        var lastProgressReport = System.Diagnostics.Stopwatch.GetTimestamp() - TimeSpan.FromSeconds(1).Ticks * System.Diagnostics.Stopwatch.Frequency / TimeSpan.TicksPerSecond;

        await HttpHelper.ReportProgressIfNeededAsync(
            options,
            uri,
            bytesDownloaded: 5000,
            totalBytes: null,
            lastProgressReport,
            progressIntervalTicks: options.ProgressInterval.Ticks);

        callbackInvoked.ShouldBeTrue();
        reportedTotalBytes.ShouldBeNull();
    }

    [Fact]
    public async Task ReportProgressIfNeededAsync_WithAsyncCallback_ShouldAwaitCompletion()
    {
        var callbackCompleted = false;

        var options = new StreamingDownloadOptions
        {
            OnProgressAsync = async (_, _, _) =>
            {
                await Task.Delay(100);
                callbackCompleted = true;
            },
            ProgressInterval = TimeSpan.FromMilliseconds(10)
        };
        var uri = new Uri("https://example.com/file.zip");
        var lastProgressReport = System.Diagnostics.Stopwatch.GetTimestamp() - TimeSpan.FromSeconds(1).Ticks * System.Diagnostics.Stopwatch.Frequency / TimeSpan.TicksPerSecond;

        await HttpHelper.ReportProgressIfNeededAsync(
            options,
            uri,
            bytesDownloaded: 1000,
            totalBytes: 10000,
            lastProgressReport,
            progressIntervalTicks: options.ProgressInterval.Ticks);

        callbackCompleted.ShouldBeTrue();
    }
}
