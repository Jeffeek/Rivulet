using System.Globalization;
using System.Runtime.CompilerServices;
using CsvHelper;
using CsvHelper.Configuration;
using Rivulet.Core;
using Rivulet.IO.Base;

namespace Rivulet.Csv;

/// <summary>
///     Base record for CSV file operations, containing file path and optional per-file configuration.
/// </summary>
/// <param name="Path">The file path for the CSV operation.</param>
/// <param name="Configuration">Optional per-file configuration. If null, defaults from <see cref="CsvOperationOptions.FileConfiguration"/> are used.</param>
public abstract record RivuletCsvFile(string Path, CsvFileConfiguration? Configuration);

/// <summary>
///     Represents a CSV file to read with optional per-file configuration.
/// </summary>
/// <typeparam name="T">The record type to parse from the CSV file.</typeparam>
/// <param name="Path">The file path to read from.</param>
/// <param name="Configuration">Optional per-file configuration. If null, defaults from <see cref="CsvOperationOptions.FileConfiguration"/> are used.</param>
public record RivuletCsvReadFile<T>(string Path, CsvFileConfiguration? Configuration) : RivuletCsvFile(Path, Configuration)
    where T : class;

/// <summary>
///     Represents a CSV file to write with records and optional per-file configuration.
/// </summary>
/// <typeparam name="T">The record type to write to the CSV file.</typeparam>
/// <param name="Path">The file path to write to.</param>
/// <param name="Records">The collection of records to write.</param>
/// <param name="Configuration">Optional per-file configuration. If null, defaults from <see cref="CsvOperationOptions.FileConfiguration"/> are used.</param>
public record RivuletCsvWriteFile<T>(string Path, ICollection<T> Records, CsvFileConfiguration? Configuration) : RivuletCsvFile(Path, Configuration)
    where T : class;

/// <summary>
///     Configuration for individual CSV file operations, allowing per-file customization of CsvHelper settings.
/// </summary>
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
            IsTransient = ex => (baseOptions.IsTransient?.Invoke(ex) ?? false) || IsCsvTransientError(ex)
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsCsvTransientError(Exception ex) =>
            ex is IOException or TimeoutException or UnauthorizedAccessException;
    }
}
