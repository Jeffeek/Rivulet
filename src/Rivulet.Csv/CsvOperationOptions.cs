using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using CsvHelper;
using CsvHelper.Configuration;
using Rivulet.Core;
using Rivulet.IO.Base;

namespace Rivulet.Csv;

/// <summary>
///     Configuration for individual CSV file operations, allowing per-file customization of CsvHelper settings.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
public sealed class CsvFileConfiguration
{
    /// <summary>
    ///     Gets or sets an action to configure CsvHelper's reader/writer settings (delimiter, culture, header detection,
    ///     etc.).
    /// </summary>
    public Action<CsvConfiguration>? ConfigurationAction { get; init; }

    /// <summary>
    ///     Gets or sets an action to configure the CsvHelper context (ClassMap registration, type conversion, etc.).
    /// </summary>
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

    /// <summary>
    ///     Gets or sets the default file configuration applied to all files unless overridden per-file.
    /// </summary>
    public CsvFileConfiguration FileConfiguration { get; init; } = new();

    /// <summary>
    ///     Creates a merged ParallelOptionsRivulet by combining CsvOperationOptions.ParallelOptions with CSV-specific
    ///     defaults.
    /// </summary>
    internal override ParallelOptionsRivulet GetMergedParallelOptions()
    {
        var baseOptions = ParallelOptions ?? new();

        return new(baseOptions)
        {
            IsTransient = ex => (baseOptions.IsTransient?.Invoke(ex) ?? false) || IsCsvTransientError(ex),
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsCsvTransientError(Exception ex) =>
            ex is IOException or TimeoutException or UnauthorizedAccessException;
    }
}
