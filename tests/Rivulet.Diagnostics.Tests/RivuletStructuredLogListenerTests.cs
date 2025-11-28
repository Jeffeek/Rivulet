using Rivulet.Core;
using System.Text.Json;

namespace Rivulet.Diagnostics.Tests;

public class RivuletStructuredLogListenerTests : IDisposable
{
    private readonly string _testFilePath = Path.Join(Path.GetTempPath(), $"rivulet-test-{Guid.NewGuid()}.json");

    [Fact]
    public void StructuredLogListener_ShouldThrow_WhenFilePathIsNull()
    {
        var act = () => new RivuletStructuredLogListener((string)null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("filePath");
    }

    [Fact]
    public void StructuredLogListener_ShouldThrow_WhenLogActionIsNull()
    {
        var act = () => new RivuletStructuredLogListener((Action<string>)null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logAction");
    }

    [Fact]
    public async Task StructuredLogListener_ShouldCreateDirectory_WhenNotExists()
    {
        var directory = Path.Join(Path.GetTempPath(), $"rivulet-test-dir-{Guid.NewGuid()}");
        var filePath = Path.Join(directory, "test.json");

        await using (new RivuletStructuredLogListener(filePath))
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

            // Wait for EventCounters to fire - increased for CI/CD reliability
            await Task.Delay(2000);
        }

        await Task.Delay(200);

        Directory.Exists(directory).Should().BeTrue();
        File.Exists(filePath).Should().BeTrue();

        TestCleanupHelper.RetryDeleteDirectory(directory);
    }

    [Fact]
    public async Task StructuredLogListener_ShouldWriteJsonToFile_WhenOperationsRun()
    {
        await using (new RivuletStructuredLogListener(_testFilePath))
        {
            // Use longer operation (200ms per item) to ensure EventCounters poll DURING execution
            // EventCounters have ~1 second polling interval, so operation needs to run for 1-2+ seconds
            // 10 items * 200ms / 2 parallelism = 1000ms (1 second) of operation time
            await Enumerable.Range(1, 10)
                .ToAsyncEnumerable()
                .SelectParallelStreamAsync(async (x, ct) =>
                {
                    await Task.Delay(200, ct);
                    return x * 2;
                }, new()
                {
                    MaxDegreeOfParallelism = 2
                })
                .ToListAsync();

            // Wait for EventCounters to poll and write metrics after operation completes
            // Polling interval is ~1 second but can be delayed under load
            await Task.Delay(2000); // Reduced from 5000ms for faster tests
        } // Dispose listener to flush and close file

        // Wait for file handle to be fully released
        await Task.Delay(500); // Reduced from 5000ms for faster tests

        File.Exists(_testFilePath).Should().BeTrue();
        var lines = await File.ReadAllLinesAsync(_testFilePath);
        lines.Should().NotBeEmpty();

        lines.Select(line => (Action)(() => JsonDocument.Parse(line)))
             .Should()
             .AllSatisfy(act => act.Should().NotThrow());
    }

    [Fact]
    public async Task StructuredLogListener_ShouldInvokeAction_WhenUsingCustomAction()
    {
        // Use ConcurrentBag to avoid collection modification exceptions on Windows
        // when EventListeners add items from background threads during enumeration
        var loggedLines = new System.Collections.Concurrent.ConcurrentBag<string>();
        await using var listener = new RivuletStructuredLogListener(loggedLines.Add);

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

        // Wait for EventCounters to fire - increased for CI/CD reliability
        await Task.Delay(2000);

        loggedLines.Should().NotBeEmpty();

        foreach (var act in loggedLines.Select(line => (Func<JsonDocument>?)(() => JsonDocument.Parse(line))))
        {
            act.Should().NotThrow();
        }
    }

    [Fact]
    public async Task StructuredLogListener_ShouldContainRequiredFields_InJsonOutput()
    {
        var loggedLines = new System.Collections.Concurrent.ConcurrentBag<string>();
        await using var listener = new RivuletStructuredLogListener(loggedLines.Add);

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

        var firstLog = JsonDocument.Parse(loggedLines.First());
        firstLog.RootElement.TryGetProperty("timestamp", out _).Should().BeTrue();
        firstLog.RootElement.TryGetProperty("source", out _).Should().BeTrue();
        firstLog.RootElement.TryGetProperty("metric", out var metric).Should().BeTrue();
        metric.TryGetProperty("name", out _).Should().BeTrue();
        metric.TryGetProperty("displayName", out _).Should().BeTrue();
        metric.TryGetProperty("value", out _).Should().BeTrue();
    }

    [Fact]
    public void StructuredLogListener_WithSyncDispose_ShouldDisposeCleanly()
    {
        var testFile = Path.Join(Path.GetTempPath(), $"rivulet-sync-dispose-{Guid.NewGuid()}.json");
        var listener = new RivuletStructuredLogListener(testFile);

        var task = Enumerable.Range(1, 3)
            .SelectParallelAsync((x, _) => ValueTask.FromResult(x), new());
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
        var loggedLines = new System.Collections.Concurrent.ConcurrentBag<string>();
        var listener = new RivuletStructuredLogListener(loggedLines.Add);

        var task = Enumerable.Range(1, 3)
            .SelectParallelAsync((x, _) => ValueTask.FromResult(x), new());
#pragma warning disable xUnit1031
        task.Wait();
#pragma warning restore xUnit1031

        // Call sync Dispose explicitly (tests the _filePath == null path)
        listener.Dispose();

        // Should not throw even though there's no file writer
        loggedLines.Should().NotBeNull();
    }

    public void Dispose()
    {
        TestCleanupHelper.RetryDeleteFile(_testFilePath);
    }
}