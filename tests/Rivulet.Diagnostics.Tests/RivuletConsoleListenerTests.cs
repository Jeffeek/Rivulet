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
    public async Task ConsoleListener_ShouldHandleLargeValues()
    {
        var consoleOutput = new StringWriter();
        var originalOutput = Console.Out;
        Console.SetOut(consoleOutput);

        try
        {
            using var listener = new RivuletConsoleListener(useColors: false);

            // Run many operations to generate large metric values
            await Enumerable.Range(1, 2000)
                .ToAsyncEnumerable()
                .SelectParallelStreamAsync(async (x, ct) =>
                {
                    await Task.Delay(1, ct);
                    return x;
                }, new ParallelOptionsRivulet
                {
                    MaxDegreeOfParallelism = 10
                })
                .ToListAsync();

            await Task.Delay(2500);
        }
        finally
        {
            Console.SetOut(originalOutput);
            await consoleOutput.DisposeAsync();
        }
    }

    [Fact]
    public async Task ConsoleListener_ShouldHandleFailuresWithColors()
    {
        var consoleOutput = new StringWriter();
        var originalOutput = Console.Out;
        Console.SetOut(consoleOutput);

        try
        {
            using var listener = new RivuletConsoleListener(useColors: true);

            try
            {
                await Enumerable.Range(1, 10)
                    .ToAsyncEnumerable()
                    .SelectParallelStreamAsync<int, int>(async (_, ct) =>
                    {
                        await Task.Delay(1, ct);
                        throw new InvalidOperationException("Test");
                    }, new ParallelOptionsRivulet
                    {
                        MaxDegreeOfParallelism = 2,
                        ErrorMode = ErrorMode.CollectAndContinue
                    })
                    .ToListAsync();
            }
            catch
            {
                // Expected
            }

            await Task.Delay(2500);

            var output = consoleOutput.ToString();
            output.Should().NotBeNullOrEmpty();
        }
        finally
        {
            Console.SetOut(originalOutput);
            await consoleOutput.DisposeAsync();
        }
    }

    [Fact]
    public async Task AllListeners_ShouldHandleEmptyDisplayUnits()
    {
        var consoleOutput = new StringWriter();
        var originalOutput = Console.Out;
        Console.SetOut(consoleOutput);

        try
        {
            using var consoleListener = new RivuletConsoleListener(useColors: false);

            // Run operations to trigger metrics
            await Enumerable.Range(1, 5)
                .ToAsyncEnumerable()
                .SelectParallelStreamAsync(async (x, ct) =>
                {
                    await Task.Delay(1, ct);
                    return x;
                }, new ParallelOptionsRivulet
                {
                    MaxDegreeOfParallelism = 2
                })
                .ToListAsync();

            await Task.Delay(2500);
        }
        finally
        {
            Console.SetOut(originalOutput);
            await consoleOutput.DisposeAsync();
        }
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
