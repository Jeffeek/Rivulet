using System.Collections.Concurrent;
using System.Text.Json;
using Rivulet.Core;

namespace Rivulet.Diagnostics.Tests;

[Collection(TestCollections.SerialEventSource)]
public class RivuletStructuredLogListenerTests : IDisposable
{
    private readonly string _testFilePath = Path.Join(Path.GetTempPath(), $"rivulet-test-{Guid.NewGuid()}.json");

    public void Dispose() => TestCleanupHelper.RetryDeleteFile(_testFilePath);

    [Fact]
    public void StructuredLogListener_ShouldThrow_WhenFilePathIsNull()
    {
        var act = () => new RivuletStructuredLogListener((string)null!);
        act.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe("filePath");
    }

    [Fact]
    public void StructuredLogListener_ShouldThrow_WhenLogActionIsNull()
    {
        var act = () => new RivuletStructuredLogListener((Action<string>)null!);
        act.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe("logAction");
    }

    [Fact]
    public async Task StructuredLogListener_ShouldCreateDirectory_WhenNotExists()
    {
        var directory = Path.Join(Path.GetTempPath(), $"rivulet-test-dir-{Guid.NewGuid()}");
        var filePath = Path.Join(directory, "test.json");

        await using (new RivuletStructuredLogListener(filePath))
        {
            // Operations must run long enough for EventCounter polling (1 second interval)
            // 5 items * 200ms / 2 parallelism = 500ms of operation time
            await Enumerable.Range(1, 5)
                .ToAsyncEnumerable()
                .SelectParallelStreamAsync(static async (x, ct) =>
                    {
                        await Task.Delay(200, ct);
                        return x;
                    },
                    new() { MaxDegreeOfParallelism = 2 })
                .ToListAsync();

            // Wait for EventCounters to fire - increased for CI/CD reliability
            await Task.Delay(1500);
        }

        await Task.Delay(200);

        Directory.Exists(directory).ShouldBeTrue();
        File.Exists(filePath).ShouldBeTrue();

        TestCleanupHelper.RetryDeleteDirectory(directory);
    }

    [Fact]
    public async Task StructuredLogListener_ShouldWriteJsonToFile_WhenOperationsRun()
    {
        await using (new RivuletStructuredLogListener(_testFilePath))
        {
            // Use longer operation (300ms per item) to ensure EventCounters poll DURING execution
            // EventCounters have ~1 second polling interval, so operation needs to run for 2+ seconds
            // 5 items * 200ms / 2 parallelism = 500ms of operation time
            await Enumerable.Range(1, 5)
                .ToAsyncEnumerable()
                .SelectParallelStreamAsync(static async (x, ct) =>
                    {
                        await Task.Delay(200, ct);
                        return x * 2;
                    },
                    new() { MaxDegreeOfParallelism = 2 })
                .ToListAsync();

            // Wait for EventCounters to poll and write metrics after operation completes
            await Task.Delay(1500);
        } // Dispose listener to flush and close file

        // Wait for file handle to be fully released
        await Task.Delay(200);

        File.Exists(_testFilePath).ShouldBeTrue();
        var lines = await File.ReadAllLinesAsync(_testFilePath);
        lines.ShouldNotBeEmpty();

        foreach (var line in lines)
        {
            var act = () => JsonDocument.Parse(line);
            act.ShouldNotThrow();
        }
    }

    [Fact]
    public async Task StructuredLogListener_ShouldInvokeAction_WhenUsingCustomAction()
    {
        // Use ConcurrentBag to avoid collection modification exceptions on Windows
        // when EventListeners add items from background threads during enumeration
        var loggedLines = new ConcurrentBag<string>();
        await using var listener = new RivuletStructuredLogListener(loggedLines.Add);

        // Operations must run long enough for EventCounter polling (1 second interval)
        // 5 items * 200ms / 2 parallelism = 500ms of operation time
        await Enumerable.Range(1, 5)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(static async (x, ct) =>
                {
                    await Task.Delay(200, ct);
                    return x * 2;
                },
                new() { MaxDegreeOfParallelism = 2 })
            .ToListAsync();

        // Wait for EventCounters to fire - increased for CI/CD reliability
        await Task.Delay(1500);

        loggedLines.ShouldNotBeEmpty();

        foreach (var act in loggedLines.Select<string, Func<JsonDocument>>(line => () => JsonDocument.Parse(line))) act.ShouldNotThrow();
    }

    [Fact]
    public async Task StructuredLogListener_ShouldContainRequiredFields_InJsonOutput()
    {
        var loggedLines = new ConcurrentBag<string>();
        await using var listener = new RivuletStructuredLogListener(loggedLines.Add);

        // Operations must run long enough for EventCounter polling (1 second interval)
        // 5 items * 200ms / 2 parallelism = 500ms of operation time
        await Enumerable.Range(1, 5)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(static async (x, ct) =>
                {
                    await Task.Delay(200, ct);
                    return x;
                },
                new() { MaxDegreeOfParallelism = 2 })
            .ToListAsync();

        // Wait for EventCounters to fire - increased for CI/CD reliability
        await Task.Delay(1500);

        loggedLines.ShouldNotBeEmpty();

        var firstLog = JsonDocument.Parse(loggedLines.First());
        firstLog.RootElement.TryGetProperty("timestamp", out _).ShouldBeTrue();
        firstLog.RootElement.TryGetProperty("source", out _).ShouldBeTrue();
        firstLog.RootElement.TryGetProperty("metric", out var metric).ShouldBeTrue();
        metric.TryGetProperty("name", out _).ShouldBeTrue();
        metric.TryGetProperty("displayName", out _).ShouldBeTrue();
        metric.TryGetProperty("value", out _).ShouldBeTrue();
    }

    [Fact]
    public void StructuredLogListener_WithSyncDispose_ShouldDisposeCleanly()
    {
        var testFile = Path.Join(Path.GetTempPath(), $"rivulet-sync-dispose-{Guid.NewGuid()}.json");
        var listener = new RivuletStructuredLogListener(testFile);

        var task = Enumerable.Range(1, 3)
            .SelectParallelAsync(static (x, _) => ValueTask.FromResult(x), new());
#pragma warning disable xUnit1031
        task.Wait();
#pragma warning restore xUnit1031

        // Call sync Dispose explicitly to test that path
        listener.Dispose();

        // Should not throw and file should be properly closed
        // Clean up
        TestCleanupHelper.RetryDeleteFile(testFile);
    }

    [Fact]
    public void StructuredLogListener_WithLogAction_AndSyncDispose_ShouldDisposeCleanly()
    {
        var loggedLines = new ConcurrentBag<string>();
        var listener = new RivuletStructuredLogListener(loggedLines.Add);

        var task = Enumerable.Range(1, 3)
            .SelectParallelAsync(static (x, _) => ValueTask.FromResult(x), new());
#pragma warning disable xUnit1031
        task.Wait();
#pragma warning restore xUnit1031

        // Call sync Dispose explicitly (tests the _filePath == null path)
        listener.Dispose();

        // Should not throw even though there's no file writer
        loggedLines.ShouldNotBeNull();
    }
}