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
        var listener = new RivuletConsoleListener(useColors: false);
        try
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

            // EventCounters fire every 1 second. Wait for at least 2 intervals
            // to ensure metrics are published.
            await Task.Delay(2500);
        }
        finally
        {
            listener.Dispose();
        }

        // Give a brief moment for console output to be written
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
