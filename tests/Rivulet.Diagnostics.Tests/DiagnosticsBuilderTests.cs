using System.Collections.Concurrent;
using Rivulet.Core;

namespace Rivulet.Diagnostics.Tests;

/// <summary>
///     Tests that use EventSource and manipulate Console.Out must run serially.
///     EventSource is a process-wide singleton, so parallel tests interfere with each other.
/// </summary>
[Collection(TestCollections.SerialEventSource)]
public sealed class DiagnosticsBuilderTests : IDisposable
{
    private readonly TextWriter _originalOutput;
    private readonly StringWriter _stringWriter;
    private readonly string _testFilePath;

    public DiagnosticsBuilderTests()
    {
        _testFilePath = Path.Join(Path.GetTempPath(), $"rivulet-test-{Guid.NewGuid()}.log");
        _stringWriter = new();
        _originalOutput = Console.Out;
        Console.SetOut(_stringWriter);
    }

    public void Dispose()
    {
        Console.SetOut(_originalOutput);
        _stringWriter.Dispose();

        TestCleanupHelper.RetryDeleteFile(_testFilePath);
    }

    [Fact]
    public async Task DiagnosticsBuilder_ShouldConfigureMultipleListeners()
    {
        var aggregatedMetrics = new ConcurrentBag<IReadOnlyList<AggregatedMetrics>>();

        await using (new DiagnosticsBuilder()
                         .AddConsoleListener(false)
                         .AddFileListener(_testFilePath)
                         .AddMetricsAggregator(TimeSpan.FromSeconds(2), metrics => aggregatedMetrics.Add(metrics))
                         .Build())
        {
            // Operations must run long enough for EventCounter polling (1 second interval)
            // 10 items * 100ms / 2 parallelism = 500ms of operation time
            await Enumerable.Range(1, 10)
                .SelectParallelAsync(static async (x, ct) =>
                    {
                        await Task.Delay(100, ct);
                        return x * 2;
                    },
                    new() { MaxDegreeOfParallelism = 2 },
                    cancellationToken: TestContext.Current.CancellationToken);

            // Wait for at least 2x the aggregation interval to ensure timer fires reliably
            await Task.Delay(2000, CancellationToken.None);
        } // Dispose to flush all listeners

        // Wait for file handle to be released and final aggregations to complete
        await Task.Delay(500, CancellationToken.None);

        // Note: Console output timing is unreliable in parallel tests due to async flushing,
        // so we verify metrics through file output and aggregated metrics instead

        File.Exists(_testFilePath).ShouldBeTrue();
        var fileContent = await File.ReadAllTextAsync(_testFilePath);
        fileContent.ShouldContain("Items Started");

        aggregatedMetrics.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task DiagnosticsBuilder_ShouldSupportPrometheusExporter()
    {
        await using var diagnostics = new DiagnosticsBuilder()
            .AddPrometheusExporter(out var exporter)
            .Build();

        // Operations must run long enough for EventCounter polling (1 second interval)
        // 10 items * 100ms / 2 parallelism = 500ms of operation time
        await Enumerable.Range(1, 10)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(static async (x, ct) =>
                {
                    await Task.Delay(100, ct);
                    return x * 2;
                },
                new() { MaxDegreeOfParallelism = 2 })
            .ToListAsync();

        // Wait for EventCounters to fire - increased for CI/CD reliability
        await Task.Delay(2000, CancellationToken.None);

        var prometheusText = exporter.Export();
        prometheusText.ShouldContain("rivulet_items_started");
    }

    [Fact]
    public async Task DiagnosticsBuilder_ShouldSupportStructuredLogWithFilePath()
    {
        var logFile = Path.Join(Path.GetTempPath(), $"rivulet-test-{Guid.NewGuid()}.json");

        await using (new DiagnosticsBuilder()
                         .AddStructuredLogListener(logFile)
                         .Build())
        {
            // Operations must run long enough for EventCounter polling (1 second interval)
            // 10 items * 100ms / 2 parallelism = 1000ms of operation time
            await Enumerable.Range(1, 10)
                .ToAsyncEnumerable()
                .SelectParallelStreamAsync(static async (x, ct) =>
                    {
                        await Task.Delay(100, ct);
                        return x;
                    },
                    new() { MaxDegreeOfParallelism = 2 })
                .ToListAsync();

            // Wait for EventCounters to fire - increased for CI/CD reliability
            await Task.Delay(2000, CancellationToken.None);
        }

        await Task.Delay(200, CancellationToken.None);

        File.Exists(logFile).ShouldBeTrue();
        TestCleanupHelper.RetryDeleteFile(logFile);
    }

    [Fact]
    public async Task DiagnosticsBuilder_ShouldSupportHealthCheck()
    {
        await using var diagnostics = new DiagnosticsBuilder()
            .AddPrometheusExporter(out var exporter)
            .AddHealthCheck(exporter, new() { ErrorRateThreshold = 0.5, FailureCountThreshold = 100 })
            .Build();

        // Operations must run long enough for EventCounter polling (1 second interval)
        // 10 items * 100ms / 2 parallelism = 500ms of operation time
        await Enumerable.Range(1, 10)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(static async (x, ct) =>
                {
                    await Task.Delay(100, ct);
                    return x;
                },
                new() { MaxDegreeOfParallelism = 2 })
            .ToListAsync();

        // Wait for EventCounters to be polled and metrics to be available - increased for CI/CD reliability
        await Task.Delay(2000, CancellationToken.None);

        var prometheusText = exporter.Export();
        prometheusText.ShouldContain("rivulet_items_started");
    }

    [Fact]
    public void DiagnosticsBuilder_ShouldNotThrow_WhenDisposed()
    {
        var diagnostics = new DiagnosticsBuilder()
            .AddConsoleListener()
            .Build();

        var act = () => diagnostics.Dispose();
        act.ShouldNotThrow();
    }

    [Fact]
    public async Task DiagnosticsBuilder_ShouldSupportStructuredLogWithAction()
    {
        var loggedLines = new ConcurrentBag<string>();

        await using var diagnostics = new DiagnosticsBuilder()
            .AddStructuredLogListener(loggedLines.Add)
            .Build();

        // Operations must run long enough for EventCounter polling (1 second interval)
        // 10 items * 100ms / 2 parallelism = 500ms of operation time
        await Enumerable.Range(1, 10)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(static async (x, ct) =>
                {
                    await Task.Delay(100, ct);
                    return x;
                },
                new() { MaxDegreeOfParallelism = 2 })
            .ToListAsync();

        // Wait for EventCounters to fire - increased for CI/CD reliability
        await Task.Delay(2000, CancellationToken.None);

        loggedLines.ShouldNotBeEmpty();
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
            .SelectParallelAsync(static (x, _) => ValueTask.FromResult(x), new(), cancellationToken: TestContext.Current.CancellationToken);
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
            .AddFileListener(logFile)                  // IAsyncDisposable
            .AddStructuredLogListener(loggedLines.Add) // IAsyncDisposable
            .AddConsoleListener()                      // IDisposable
            .Build();

        // Run operation
        var task = Enumerable.Range(1, 3)
            .SelectParallelAsync(static (x, _) => ValueTask.FromResult(x), new(), cancellationToken: TestContext.Current.CancellationToken);
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
            .SelectParallelAsync(static (x, _) => ValueTask.FromResult(x), new(), cancellationToken: TestContext.Current.CancellationToken);

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
            .SelectParallelAsync(static (x, _) => ValueTask.FromResult(x), new(), cancellationToken: TestContext.Current.CancellationToken);
#pragma warning disable xUnit1031
        task.Wait();
#pragma warning restore xUnit1031

        // First dispose
        diagnostics.Dispose();

        // Second dispose should not throw
        var act = () => diagnostics.Dispose();
        act.ShouldNotThrow();
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
        await act.ShouldNotThrowAsync();

        TestCleanupHelper.RetryDeleteFile(logFile);
    }

    [Fact]
    public void DiagnosticsBuilder_WithEmptyBuilder_ShouldBuildSuccessfully()
    {
        var diagnostics = new DiagnosticsBuilder().Build();
        diagnostics.ShouldNotBeNull();

        var act = () => diagnostics.Dispose();
        act.ShouldNotThrow();
    }

    [Fact]
    public Task DiagnosticsBuilder_WithEmptyBuilder_ShouldDisposeAsyncSuccessfully()
    {
        var diagnostics = new DiagnosticsBuilder().Build();
        diagnostics.ShouldNotBeNull();

        var act = async () => await diagnostics.DisposeAsync();
        return act.ShouldNotThrowAsync();
    }

    [Fact]
    public void DiagnosticsBuilder_ShouldChainAllMethods()
    {
        var logFile = Path.Join(Path.GetTempPath(), $"rivulet-chain-{Guid.NewGuid()}.log");
        var structuredFile = Path.Join(Path.GetTempPath(), $"rivulet-chain-{Guid.NewGuid()}.json");
        var loggedLines = new List<string>();

        var diagnostics = new DiagnosticsBuilder()
            .AddConsoleListener()
            .AddFileListener(logFile)
            .AddStructuredLogListener(structuredFile)
            .AddStructuredLogListener(loggedLines.Add)
            .AddMetricsAggregator(TimeSpan.FromSeconds(5), static _ => { })
            .AddPrometheusExporter(out var exporter)
            .AddHealthCheck(exporter)
            .Build();

        diagnostics.ShouldNotBeNull();
        exporter.ShouldNotBeNull();

        diagnostics.Dispose();
        TestCleanupHelper.RetryDeleteFile(logFile);
        TestCleanupHelper.RetryDeleteFile(structuredFile);
    }

    [Fact]
    public void DiagnosticsBuilder_WithColoredConsoleListener_ShouldNotThrow()
    {
        var diagnostics = new DiagnosticsBuilder()
            .AddConsoleListener()
            .Build();

        diagnostics.ShouldNotBeNull();
        diagnostics.Dispose();
    }

    [Fact]
    public void DiagnosticsBuilder_WithCustomMaxFileSize_ShouldAcceptParameter()
    {
        var logFile = Path.Join(Path.GetTempPath(), $"rivulet-custom-{Guid.NewGuid()}.log");
        var diagnostics = new DiagnosticsBuilder()
            .AddFileListener(logFile, 5 * 1024 * 1024) // 5 MB
            .Build();

        diagnostics.ShouldNotBeNull();
        diagnostics.Dispose();
        TestCleanupHelper.RetryDeleteFile(logFile);
    }

    [Fact]
    public void DiagnosticsBuilder_WithHealthCheckOptions_ShouldAcceptOptions()
    {
        var diagnostics = new DiagnosticsBuilder()
            .AddPrometheusExporter(out var exporter)
            .AddHealthCheck(exporter, new() { ErrorRateThreshold = 0.2, FailureCountThreshold = 50 })
            .Build();

        diagnostics.ShouldNotBeNull();
        diagnostics.Dispose();
    }

    [Fact]
    public void DiagnosticsBuilder_WithHealthCheckNoOptions_ShouldUseDefaults()
    {
        using var diagnostics = new DiagnosticsBuilder()
            .AddPrometheusExporter(out var exporter)
            .AddHealthCheck(exporter)
            .Build();

        diagnostics.ShouldNotBeNull();
    }
}
