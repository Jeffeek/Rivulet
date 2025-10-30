using FluentAssertions;
using Rivulet.Core;

namespace Rivulet.Diagnostics.Tests;

public class RivuletConsoleListenerTests : IDisposable
{
    private readonly StringWriter _stringWriter;
    private readonly TextWriter _originalOutput;

    public RivuletConsoleListenerTests()
    {
        _stringWriter = new StringWriter();
        _originalOutput = Console.Out;
        Console.SetOut(_stringWriter);
    }

    [Fact]
    public async Task ConsoleListener_ShouldWriteMetrics_WhenOperationsRun()
    {
        using (var listener = new RivuletConsoleListener(useColors: false))
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
        } // Dispose listener to ensure all events are flushed

        // Wait a moment for console output to be flushed
        await Task.Delay(100);

        var output = _stringWriter.ToString();
        output.Should().Contain("Items Started");
        output.Should().Contain("Items Completed");
    }

    [Fact]
    public void ConsoleListener_ShouldNotThrow_WhenDisposed()
    {
        var listener = new RivuletConsoleListener();
        var act = () => listener.Dispose();
        act.Should().NotThrow();
    }

    public void Dispose()
    {
        Console.SetOut(_originalOutput);
        _stringWriter.Dispose();
    }
}
