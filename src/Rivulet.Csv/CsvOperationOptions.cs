using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using CsvHelper;
using CsvHelper.Configuration;
using Rivulet.Core;
using Rivulet.IO.Internal;

namespace Rivulet.Csv;

public struct CsvFileConfiguration
{
    public Action<IReaderConfiguration>? ReaderConfigurationAction { get; init; }

    public Action<IWriterConfiguration>? WriterConfigurationAction { get; init; }

    public Action<CsvContext>? CsvContextAction { get; init; }
}

/// <summary>
///     Configuration options for CSV operations with Rivulet.Csv.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
public sealed class CsvOperationOptions : BaseFileOperationOptions
{
    /// <summary>
    ///     Gets or sets the culture info to use for parsing and formatting values.
    ///     Default is InvariantCulture.
    /// </summary>
    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;

    public CsvFileConfiguration FileConfiguration { get; init; } = new();

    /// <summary>
    ///     Creates a merged ParallelOptionsRivulet by combining CsvOperationOptions.ParallelOptions with CSV-specific defaults.
    /// </summary>
    internal ParallelOptionsRivulet GetMergedParallelOptions()
    {
        var baseOptions = ParallelOptions ?? new();

        return new()
        {
            IsTransient = ex => (baseOptions.IsTransient?.Invoke(ex) ?? false) || IsCsvTransientError(ex),
            MaxRetries = baseOptions.MaxRetries,
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsCsvTransientError(Exception ex) =>
            ex is IOException or TimeoutException or UnauthorizedAccessException;
    }
}
