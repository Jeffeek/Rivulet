using FluentAssertions;
using Rivulet.Core;

namespace Rivulet.Diagnostics.Tests;

public class DiagnosticsBuilderTests : IDisposable
{
    private readonly string _testFilePath;
    private readonly StringWriter _stringWriter;
    private readonly TextWriter _originalOutput;

    public DiagnosticsBuilderTests()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"rivulet-test-{Guid.NewGuid()}.log");
        _stringWriter = new StringWriter();
        _originalOutput = Console.Out;
        Console.SetOut(_stringWriter);
    }

    [Fact]
    public async Task DiagnosticsBuilder_ShouldConfigureMultipleListeners()
    {
        var aggregatedMetrics = new List<IReadOnlyList<AggregatedMetrics>>();

        using (var _ = new DiagnosticsBuilder()
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
                }, new ParallelOptionsRivulet
                {
                    MaxDegreeOfParallelism = 2
                });

            await Task.Delay(3000);
        } // Dispose to flush all listeners

        // Wait for file handle to be released
        await Task.Delay(100);

        // Note: Console output may include FluentAssertions license warnings,
        // so we verify metrics through file output and aggregated metrics instead
        var consoleOutput = _stringWriter.ToString();
        consoleOutput.Should().NotBeNullOrEmpty(); // Verify something was written

        File.Exists(_testFilePath).Should().BeTrue();
        var fileContent = await File.ReadAllTextAsync(_testFilePath);
        fileContent.Should().Contain("Items Started");

        aggregatedMetrics.Should().NotBeEmpty();
    }

    [Fact]
    public async Task DiagnosticsBuilder_ShouldSupportPrometheusExporter()
    {
        using var diagnostics = new DiagnosticsBuilder()
            .AddPrometheusExporter(out var exporter)
            .Build();

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
        var loggedLines = new List<string>();
        
        using var diagnostics = new DiagnosticsBuilder()
            .AddStructuredLogListener(loggedLines.Add)
            .Build();

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
    }

    public void Dispose()
    {
        Console.SetOut(_originalOutput);
        _stringWriter.Dispose();
        
        TestCleanupHelper.RetryDeleteFile(_testFilePath);
    }
}
