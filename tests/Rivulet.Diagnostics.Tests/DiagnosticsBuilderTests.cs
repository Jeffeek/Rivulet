using Rivulet.Core;

namespace Rivulet.Diagnostics.Tests;

/// <summary>
/// Tests that manipulate Console.Out must run serially to avoid interference on Windows.
/// Parallel execution can cause ObjectDisposedException in FluentAssertions when one test
/// disposes the console TextWriter while another test is using it.
/// </summary>
[Collection("Serial Console Tests")]
public class DiagnosticsBuilderTests : IDisposable
{
    private readonly string _testFilePath;
    private readonly StringWriter _stringWriter;
    private readonly TextWriter _originalOutput;

    public DiagnosticsBuilderTests()
    {
        _testFilePath = Path.Join(Path.GetTempPath(), $"rivulet-test-{Guid.NewGuid()}.log");
        _stringWriter = new();
        _originalOutput = Console.Out;
        Console.SetOut(_stringWriter);
    }

    [Fact]
    public async Task DiagnosticsBuilder_ShouldConfigureMultipleListeners()
    {
        var aggregatedMetrics = new System.Collections.Concurrent.ConcurrentBag<IReadOnlyList<AggregatedMetrics>>();

        await using (new DiagnosticsBuilder()
                         .AddConsoleListener(useColors: false)
                         .AddFileListener(_testFilePath)
                         .AddMetricsAggregator(TimeSpan.FromSeconds(2), metrics => aggregatedMetrics.Add(metrics))
                         .Build())
        {
            await Enumerable.Range(1, 10)
                .SelectParallelAsync(async (x, ct) =>
                {
                    await Task.Delay(10, ct);
                    return x * 2;
                }, new()
                {
                    MaxDegreeOfParallelism = 2
                });

            // Wait for at least 2x the aggregation interval to ensure timer fires reliably
            // Increased from 2500ms to 4000ms to handle CI/CD timing variability
            await Task.Delay(4000);
        } // Dispose to flush all listeners

        // Wait for file handle to be released and final aggregations to complete
        await Task.Delay(200);

        // Note: Console output timing is unreliable in parallel tests due to async flushing,
        // so we verify metrics through file output and aggregated metrics instead

        File.Exists(_testFilePath).Should().BeTrue();
        var fileContent = await File.ReadAllTextAsync(_testFilePath);
        fileContent.Should().Contain("Items Started");

        aggregatedMetrics.Should().NotBeEmpty();
    }

    [Fact]
    public async Task DiagnosticsBuilder_ShouldSupportPrometheusExporter()
    {
        await using var diagnostics = new DiagnosticsBuilder()
            .AddPrometheusExporter(out var exporter)
            .Build();

        await Enumerable.Range(1, 10)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x * 2;
            }, new()
            {
                MaxDegreeOfParallelism = 2
            })
            .ToListAsync();

        await Task.Delay(1500);

        var prometheusText = exporter.Export();
        prometheusText.Should().Contain("rivulet_items_started");
    }

    [Fact]
    public async Task DiagnosticsBuilder_ShouldSupportStructuredLogWithFilePath()
    {
        var logFile = Path.Join(Path.GetTempPath(), $"rivulet-test-{Guid.NewGuid()}.json");

        await using (new DiagnosticsBuilder()
                         .AddStructuredLogListener(logFile)
                         .Build())
        {
            await Enumerable.Range(1, 5)
                .ToAsyncEnumerable()
                .SelectParallelStreamAsync(async (x, ct) =>
                {
                    await Task.Delay(10, ct);
                    return x;
                }, new()
                {
                    MaxDegreeOfParallelism = 2
                })
                .ToListAsync();

            await Task.Delay(1500);
        }

        await Task.Delay(100);

        File.Exists(logFile).Should().BeTrue();
        TestCleanupHelper.RetryDeleteFile(logFile);
    }

    [Fact]
    public async Task DiagnosticsBuilder_ShouldSupportHealthCheck()
    {
        await using var diagnostics = new DiagnosticsBuilder()
            .AddPrometheusExporter(out var exporter)
            .AddHealthCheck(exporter, new()
            {
                ErrorRateThreshold = 0.5,
                FailureCountThreshold = 100
            })
            .Build();

        await Enumerable.Range(1, 5)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x;
            }, new()
            {
                MaxDegreeOfParallelism = 2
            })
            .ToListAsync();

        // Wait for EventCounters to be polled and metrics to be available
        // EventCounters have a default polling interval of ~1 second
        // Wait 3000ms to ensure at least 2-3 polling cycles have occurred
        // This ensures metrics are captured and available for export
        await Task.Delay(3000);

        var prometheusText = exporter.Export();
        prometheusText.Should().Contain("rivulet_items_started");
    }

    [Fact]
    public void DiagnosticsBuilder_ShouldNotThrow_WhenDisposed()
    {
        var diagnostics = new DiagnosticsBuilder()
            .AddConsoleListener()
            .Build();

        var act = () => diagnostics.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task DiagnosticsBuilder_ShouldSupportStructuredLogWithAction()
    {
        var loggedLines = new System.Collections.Concurrent.ConcurrentBag<string>();

        await using var diagnostics = new DiagnosticsBuilder()
            .AddStructuredLogListener(loggedLines.Add)
            .Build();

        await Enumerable.Range(1, 5)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x;
            }, new()
            {
                MaxDegreeOfParallelism = 2
            })
            .ToListAsync();

        await Task.Delay(1500);

        loggedLines.Should().NotBeEmpty();
    }

    [Fact]
    public void DiagnosticsBuilder_WithSyncDispose_ShouldDisposeAllListenersCleanly()
    {
        var logFile = Path.Join(Path.GetTempPath(), $"rivulet-sync-dispose-{Guid.NewGuid()}.json");
        var diagnostics = new DiagnosticsBuilder()
            .AddFileListener(logFile)
            .AddConsoleListener()
            .Build();

        var task = Enumerable.Range(1, 3)
            .SelectParallelAsync((x, _) => ValueTask.FromResult(x), new());
#pragma warning disable xUnit1031
        task.Wait();
#pragma warning restore xUnit1031

        diagnostics.Dispose();

        // Should not throw
        TestCleanupHelper.RetryDeleteFile(logFile);
    }

    [Fact]
    public void DiagnosticsBuilder_WithMixedListeners_ShouldDisposeAsyncAndSyncListeners()
    {
        var logFile = Path.Join(Path.GetTempPath(), $"rivulet-mixed-{Guid.NewGuid()}.json");
        var loggedLines = new List<string>();

        var diagnostics = new DiagnosticsBuilder()
            .AddFileListener(logFile) // IAsyncDisposable
            .AddStructuredLogListener(loggedLines.Add) // IAsyncDisposable
            .AddConsoleListener() // IDisposable
            .Build();

        // Run operation
        var task = Enumerable.Range(1, 3)
            .SelectParallelAsync((x, _) => ValueTask.FromResult(x), new());
#pragma warning disable xUnit1031
        task.Wait();
#pragma warning restore xUnit1031

        // Dispose sync first to test dual disposal logic
        diagnostics.Dispose();

        // Should not throw and should handle both types
        TestCleanupHelper.RetryDeleteFile(logFile);
    }

    [Fact]
    public async Task DiagnosticsBuilder_WithAsyncDispose_ShouldDisposeAllListenersCleanly()
    {
        var logFile = Path.Join(Path.GetTempPath(), $"rivulet-async-dispose-{Guid.NewGuid()}.json");
        var diagnostics = new DiagnosticsBuilder()
            .AddFileListener(logFile)
            .AddStructuredLogListener(logFile + ".structured")
            .Build();

        await Enumerable.Range(1, 3)
            .SelectParallelAsync((x, _) => ValueTask.FromResult(x), new());

        await diagnostics.DisposeAsync();

        // Should not throw
        TestCleanupHelper.RetryDeleteFile(logFile);
        TestCleanupHelper.RetryDeleteFile(logFile + ".structured");
    }

    [Fact]
    public void DiagnosticsBuilder_WithDoubleDispose_ShouldNotThrow()
    {
        var diagnostics = new DiagnosticsBuilder()
            .AddConsoleListener()
            .Build();

        // Run minimal operation
        var task = Enumerable.Range(1, 1)
            .SelectParallelAsync((x, _) => ValueTask.FromResult(x), new());
#pragma warning disable xUnit1031
        task.Wait();
#pragma warning restore xUnit1031

        // First dispose
        diagnostics.Dispose();

        // Second dispose should not throw
        var act = () => diagnostics.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task DiagnosticsBuilder_WithDoubleAsyncDispose_ShouldNotThrow()
    {
        var logFile = Path.Join(Path.GetTempPath(), $"rivulet-double-{Guid.NewGuid()}.json");
        var diagnostics = new DiagnosticsBuilder()
            .AddFileListener(logFile)
            .Build();

        // First async dispose
        await diagnostics.DisposeAsync();

        // Second async dispose should not throw
        var act = async () => await diagnostics.DisposeAsync();
        await act.Should().NotThrowAsync();

        TestCleanupHelper.RetryDeleteFile(logFile);
    }

    public void Dispose()
    {
        Console.SetOut(_originalOutput);
        _stringWriter.Dispose();

        TestCleanupHelper.RetryDeleteFile(_testFilePath);
    }

    [Fact]
    public void DiagnosticsBuilder_WithEmptyBuilder_ShouldBuildSuccessfully()
    {
        var diagnostics = new DiagnosticsBuilder().Build();
        diagnostics.Should().NotBeNull();

        var act = () => diagnostics.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task DiagnosticsBuilder_WithEmptyBuilder_ShouldDisposeAsyncSuccessfully()
    {
        var diagnostics = new DiagnosticsBuilder().Build();
        diagnostics.Should().NotBeNull();

        var act = async () => await diagnostics.DisposeAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void DiagnosticsBuilder_ShouldChainAllMethods()
    {
        var logFile = Path.Join(Path.GetTempPath(), $"rivulet-chain-{Guid.NewGuid()}.log");
        var structuredFile = Path.Join(Path.GetTempPath(), $"rivulet-chain-{Guid.NewGuid()}.json");
        var loggedLines = new List<string>();

        var diagnostics = new DiagnosticsBuilder()
            .AddConsoleListener(useColors: true)
            .AddFileListener(logFile)
            .AddStructuredLogListener(structuredFile)
            .AddStructuredLogListener(loggedLines.Add)
            .AddMetricsAggregator(TimeSpan.FromSeconds(5), _ => { })
            .AddPrometheusExporter(out var exporter)
            .AddHealthCheck(exporter)
            .Build();

        diagnostics.Should().NotBeNull();
        exporter.Should().NotBeNull();

        diagnostics.Dispose();
        TestCleanupHelper.RetryDeleteFile(logFile);
        TestCleanupHelper.RetryDeleteFile(structuredFile);
    }

    [Fact]
    public void DiagnosticsBuilder_WithColoredConsoleListener_ShouldNotThrow()
    {
        var diagnostics = new DiagnosticsBuilder()
            .AddConsoleListener(useColors: true)
            .Build();

        diagnostics.Should().NotBeNull();
        diagnostics.Dispose();
    }

    [Fact]
    public void DiagnosticsBuilder_WithCustomMaxFileSize_ShouldAcceptParameter()
    {
        var logFile = Path.Join(Path.GetTempPath(), $"rivulet-custom-{Guid.NewGuid()}.log");
        var diagnostics = new DiagnosticsBuilder()
            .AddFileListener(logFile, maxFileSizeBytes: 5 * 1024 * 1024) // 5 MB
            .Build();

        diagnostics.Should().NotBeNull();
        diagnostics.Dispose();
        TestCleanupHelper.RetryDeleteFile(logFile);
    }

    [Fact]
    public void DiagnosticsBuilder_WithHealthCheckOptions_ShouldAcceptOptions()
    {
        var diagnostics = new DiagnosticsBuilder()
            .AddPrometheusExporter(out var exporter)
            .AddHealthCheck(exporter, new()
            {
                ErrorRateThreshold = 0.2,
                FailureCountThreshold = 50
            })
            .Build();

        diagnostics.Should().NotBeNull();
        diagnostics.Dispose();
    }

    [Fact]
    public void DiagnosticsBuilder_WithHealthCheckNoOptions_ShouldUseDefaults()
    {
        var diagnostics = new DiagnosticsBuilder()
            .AddPrometheusExporter(out var exporter)
            .AddHealthCheck(exporter)
            .Build();

        diagnostics.Should().NotBeNull();
        diagnostics.Dispose();
    }
}
