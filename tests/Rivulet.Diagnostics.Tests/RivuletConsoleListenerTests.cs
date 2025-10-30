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

            // Wait longer for EventCounters to fire (they fire every 1 second)
            await Task.Delay(2500);
        } // Dispose listener to ensure all events are flushed

        // Flush the StringWriter
        _stringWriter.Flush();

        // Wait for console output to be fully flushed
        await Task.Delay(200);

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
