using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Rivulet.Core;
using Rivulet.IO.Internal;

namespace Rivulet.Csv;

/// <summary>
///     Configuration options for CSV operations with Rivulet.Csv.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
public sealed class CsvOperationOptions : BaseFileOperationOptions
{
    /// <summary>
    ///     Gets or sets whether the CSV file has a header record.
    ///     Default is true.
    /// </summary>
    public bool HasHeaderRecord { get; init; } = true;

    /// <summary>
    ///     Gets or sets the field delimiter character.
    ///     Default is "," (comma).
    /// </summary>
    public string Delimiter { get; init; } = ",";

    /// <summary>
    ///     Gets or sets the culture info to use for parsing and formatting values.
    ///     Default is InvariantCulture.
    /// </summary>
    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;

    /// <summary>
    ///     Gets or sets whether to trim whitespace from fields.
    ///     Default is false.
    /// </summary>
    public bool TrimWhitespace { get; init; }

    /// <summary>
    ///     Gets or sets whether to ignore blank lines in CSV files.
    ///     Default is true.
    /// </summary>
    public bool IgnoreBlankLines { get; init; } = true;

    /// <summary>
    ///     Creates a merged ParallelOptionsRivulet by combining CsvOperationOptions.ParallelOptions with CSV-specific defaults.
    /// </summary>
    internal ParallelOptionsRivulet GetMergedParallelOptions()
    {
        var baseOptions = ParallelOptions ?? new();

        return new()
        {
            MaxRetries = baseOptions.MaxRetries > 0 ? baseOptions.MaxRetries : 3,
            IsTransient = ex => (baseOptions.IsTransient?.Invoke(ex) ?? false) || IsCsvTransientError(ex),
            MaxDegreeOfParallelism = baseOptions.MaxDegreeOfParallelism,
            BaseDelay = baseOptions.BaseDelay,
            BackoffStrategy = baseOptions.BackoffStrategy,
            PerItemTimeout = baseOptions.PerItemTimeout,
            ErrorMode = baseOptions.ErrorMode,
            OnStartItemAsync = baseOptions.OnStartItemAsync,
            OnCompleteItemAsync = baseOptions.OnCompleteItemAsync,
            OnErrorAsync = baseOptions.OnErrorAsync,
            CircuitBreaker = baseOptions.CircuitBreaker,
            RateLimit = baseOptions.RateLimit,
            Progress = baseOptions.Progress,
            OrderedOutput = baseOptions.OrderedOutput,
            Metrics = baseOptions.Metrics,
            AdaptiveConcurrency = baseOptions.AdaptiveConcurrency,
            OnRetryAsync = baseOptions.OnRetryAsync,
            OnDrainAsync = baseOptions.OnDrainAsync,
            OnFallback = baseOptions.OnFallback,
            OnThrottleAsync = baseOptions.OnThrottleAsync,
            ChannelCapacity = baseOptions.ChannelCapacity
        };

        static bool IsCsvTransientError(Exception ex) =>
            ex is IOException or TimeoutException or UnauthorizedAccessException;
    }
}
