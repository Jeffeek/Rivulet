using Rivulet.Core.Internal;
using Rivulet.Core.Observability;

namespace Rivulet.Diagnostics;

/// <summary>
///     EventListener that writes Rivulet metrics to the console.
///     Useful for development and debugging scenarios.
/// </summary>
/// <example>
///     <code>
/// using var listener = new RivuletConsoleListener();
/// 
/// await Enumerable.Range(1, 100)
///     .ToAsyncEnumerable()
///     .SelectParallelAsync(async x => await ProcessAsync(x), new ParallelOptionsRivulet
///     {
///         MaxDegreeOfParallelism = 10
///     });
/// </code>
/// </example>
public sealed class RivuletConsoleListener : RivuletEventListenerBase
{
    private readonly bool _useColors;
    private readonly object _lock = LockFactory.CreateLock();

    /// <summary>
    ///     Initializes a new instance of the <see cref="RivuletConsoleListener" /> class.
    /// </summary>
    /// <param name="useColors">Whether to use console colors for output. Default is true.</param>
    public RivuletConsoleListener(bool useColors = true) => _useColors = useColors;

    /// <summary>
    ///     Called when a counter value is received.
    /// </summary>
    protected override void OnCounterReceived(string name,
        string displayName,
        double value,
        string displayUnits) =>
        LockHelper.Execute(_lock,
            () =>
            {
                var timestamp = DateTime.UtcNow.ToString(RivuletDiagnosticsConstants.DateTimeFormats.Console);
                var formattedValue = FormatValue(value, displayUnits);

                if (_useColors)
                {
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.Write($"[{timestamp}] ");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write($"{displayName}: ");
                    Console.ForegroundColor = GetValueColor(name, value);
                    Console.Write(formattedValue);
                    Console.ResetColor();
                    Console.WriteLine();
                }
                else
                    Console.WriteLine($"[{timestamp}] {displayName}: {formattedValue}");
            });

    private static string FormatValue(double value, string displayUnits)
    {
        var formattedNumber = value switch
        {
            >= 1000000 => $"{value / 1000000:F2}M",
            >= 1000 => $"{value / 1000:F2}K",
            _ => $"{value:F2}"
        };

        return string.IsNullOrEmpty(displayUnits)
            ? formattedNumber
            : $"{formattedNumber} {displayUnits}";
    }

    private static ConsoleColor GetValueColor(string name, double value) =>
        name switch
        {
            RivuletMetricsConstants.CounterNames.TotalFailures when value > 0 => ConsoleColor.Red,
            RivuletMetricsConstants.CounterNames.TotalRetries when value > 0 => ConsoleColor.Yellow,
            RivuletMetricsConstants.CounterNames.ThrottleEvents when value > 0 => ConsoleColor.Yellow,
            _ => ConsoleColor.Green
        };
}