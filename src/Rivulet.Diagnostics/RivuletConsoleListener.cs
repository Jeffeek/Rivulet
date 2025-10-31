using Rivulet.Core.Internal;

namespace Rivulet.Diagnostics;

/// <summary>
/// EventListener that writes Rivulet metrics to the console.
/// Useful for development and debugging scenarios.
/// </summary>
/// <example>
/// <code>
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
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    /// <summary>
    /// Initializes a new instance of the <see cref="RivuletConsoleListener"/> class.
    /// </summary>
    /// <param name="useColors">Whether to use console colors for output. Default is true.</param>
    public RivuletConsoleListener(bool useColors = true)
    {
        _useColors = useColors;
    }

    /// <summary>
    /// Called when a counter value is received.
    /// </summary>
    protected override void OnCounterReceived(string name, string displayName, double value, string displayUnits)
    {
        LockHelper.Execute(_lock, () =>
        {
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
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
            {
                Console.WriteLine($"[{timestamp}] {displayName}: {formattedValue}");
            }
        });
    }

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

    private static ConsoleColor GetValueColor(string name, double value)
    {
        return name switch
        {
            "total-failures" when value > 0 => ConsoleColor.Red,
            "total-retries" when value > 0 => ConsoleColor.Yellow,
            "throttle-events" when value > 0 => ConsoleColor.Yellow,
            _ => ConsoleColor.Green
        };
    }
}
