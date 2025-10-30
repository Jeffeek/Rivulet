using FluentAssertions;
using Rivulet.Core;
using System.Text.Json;

namespace Rivulet.Diagnostics.Tests;

public class RivuletStructuredLogListenerTests : IDisposable
{
    private readonly string _testFilePath = Path.Combine(Path.GetTempPath(), $"rivulet-test-{Guid.NewGuid()}.json");

    [Fact]
    public async Task StructuredLogListener_ShouldWriteJsonToFile_WhenOperationsRun()
    {
        using (new RivuletStructuredLogListener(_testFilePath))
        {
            await Enumerable.Range(1, 10)
                .ToAsyncEnumerable()
                .SelectParallelStreamAsync(async (x, ct) =>
                {
                    await Task.Delay(10, ct);
                    return x * 2;
                }, new ParallelOptionsRivulet
                {
                    MaxDegreeOfParallelism = 2
                })
                .ToListAsync();

            await Task.Delay(1500);
        } // Dispose listener to flush and close file

        // Wait a moment for file handle to be fully released
        await Task.Delay(100);

        File.Exists(_testFilePath).Should().BeTrue();
        var lines = await File.ReadAllLinesAsync(_testFilePath);
        lines.Should().NotBeEmpty();

        foreach (var line in lines)
        {
            var act = () => JsonDocument.Parse(line);
            act.Should().NotThrow();
        }
    }

    [Fact]
    public async Task StructuredLogListener_ShouldInvokeAction_WhenUsingCustomAction()
    {
        var loggedLines = new List<string>();
        using var listener = new RivuletStructuredLogListener(loggedLines.Add);

        await Enumerable.Range(1, 10)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x * 2;
            }, new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 2
            })
            .ToListAsync();

        await Task.Delay(5000);

        loggedLines.Should().NotBeEmpty();
        
        foreach (var act in loggedLines.Select(line => (Func<JsonDocument>?)(() => JsonDocument.Parse(line))))
        {
            act.Should().NotThrow();
        }
    }

    [Fact]
    public async Task StructuredLogListener_ShouldContainRequiredFields_InJsonOutput()
    {
        var loggedLines = new List<string>();
        using var listener = new RivuletStructuredLogListener(loggedLines.Add);

        await Enumerable.Range(1, 5)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x;
            }, new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 2
            })
            .ToListAsync();

        await Task.Delay(1500);

        loggedLines.Should().NotBeEmpty();
        
        var firstLog = JsonDocument.Parse(loggedLines[0]);
        firstLog.RootElement.TryGetProperty("timestamp", out _).Should().BeTrue();
        firstLog.RootElement.TryGetProperty("source", out _).Should().BeTrue();
        firstLog.RootElement.TryGetProperty("metric", out var metric).Should().BeTrue();
        metric.TryGetProperty("name", out _).Should().BeTrue();
        metric.TryGetProperty("displayName", out _).Should().BeTrue();
        metric.TryGetProperty("value", out _).Should().BeTrue();
    }

    public void Dispose()
    {
        TestCleanupHelper.RetryDeleteFile(_testFilePath);
    }
}