using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using CsvHelper;
using CsvHelper.Configuration;
using Rivulet.Core;
using Rivulet.Csv.Internal;
using Rivulet.IO.Base;

namespace Rivulet.Csv;

/// <summary>
///     Extension methods for parallel CSV parsing and writing operations with bounded concurrency and resilience.
/// </summary>
public static class CsvParallelExtensions
{
    /// <summary>
    ///     Parses multiple CSV files in parallel and returns all records as a flattened list.
    /// </summary>
    /// <typeparam name="T">The type of record to parse from CSV files.</typeparam>
    /// <param name="filePaths">Collection of file paths to parse.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A flattened list containing all records from all files.</returns>
    /// <exception cref="ArgumentNullException">Thrown when filePaths is null.</exception>
    public static Task<IReadOnlyList<T>> ParseCsvParallelAsync<T>(
        this IEnumerable<string> filePaths,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
        where T : class => ParseCsvParallelAsync<T>(filePaths.Select(static x => new RivuletCsvReadFile<T>(x, null)), options, cancellationToken);

    /// <summary>
    ///     Parses multiple CSV files in parallel with per-file configuration and returns all records as a flattened list.
    /// </summary>
    /// <typeparam name="T">The type of record to parse from CSV files.</typeparam>
    /// <param name="csvFiles">Collection of CSV files with optional per-file configuration to parse.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A flattened list containing all records from all files.</returns>
    /// <exception cref="ArgumentNullException">Thrown when csvFiles is null.</exception>
    public static async Task<IReadOnlyList<T>> ParseCsvParallelAsync<T>(
        this IEnumerable<RivuletCsvFile> csvFiles,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        ArgumentNullException.ThrowIfNull(csvFiles);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        var fileResults = await csvFiles.SelectParallelAsync(
            (csvFile, ct) => ParseCsvFileAsync<T>(csvFile, options, ct),
            parallelOptions,
            cancellationToken
        ).ConfigureAwait(false);

        var totalCount = fileResults.Sum(static list => list.Count);
        var result = new List<T>(totalCount);

        foreach (var fileRecords in fileResults)
            result.AddRange(fileRecords);

        return result;
    }

    /// <summary>
    ///     Parses multiple CSV files in parallel with per-file configuration, returning records grouped by file path.
    /// </summary>
    /// <typeparam name="T1">The record type to parse from CSV files.</typeparam>
    /// <param name="fileReads">Collection of CSV files with optional per-file configuration to parse.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A dictionary mapping file paths to their parsed records.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fileReads is null.</exception>
    /// <remarks>
    ///     This method groups results by file path, useful when you need to track which records came from which file.
    ///     For a flattened list of all records, use <see cref="ParseCsvParallelAsync{T}(IEnumerable{string}, CsvOperationOptions, CancellationToken)"/>.
    /// </remarks>
    public static async Task<IReadOnlyDictionary<string, IReadOnlyList<T1>>> ParseCsvParallelGroupedAsync<T1>(
        this IEnumerable<RivuletCsvReadFile<T1>> fileReads,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
        where T1 : class
    {
        ArgumentNullException.ThrowIfNull(fileReads);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();
        var dictionary = new ConcurrentDictionary<string, IReadOnlyList<T1>>();

        await fileReads.ForEachParallelAsync(
            async (read, ct) =>
            {
                var records = await ParseCsvFileAsync<T1>(read, options, ct).ConfigureAwait(false);
                dictionary.TryAdd(read.Path, records);
            },
            parallelOptions,
            cancellationToken
        ).ConfigureAwait(false);

        return dictionary;
    }

    /// <summary>
    ///     Parses multiple CSV file groups in parallel with per-file configuration, returning records grouped by file path for each type.
    /// </summary>
    /// <typeparam name="T1">The first record type to parse.</typeparam>
    /// <typeparam name="T2">The second record type to parse.</typeparam>
    /// <param name="fileReads">First collection of files to parse.</param>
    /// <param name="fileReads2">Second collection of files to parse.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A tuple of dictionaries, each mapping file paths to their parsed records.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any file collection is null.</exception>
    public static async Task<(IReadOnlyDictionary<string, IReadOnlyList<T1>>, IReadOnlyDictionary<string, IReadOnlyList<T2>>)> ParseCsvParallelGroupedAsync<T1, T2>(
        IEnumerable<RivuletCsvReadFile<T1>> fileReads,
        IEnumerable<RivuletCsvReadFile<T2>> fileReads2,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
        where T1 : class
        where T2 : class
    {
        ArgumentNullException.ThrowIfNull(fileReads);
        ArgumentNullException.ThrowIfNull(fileReads2);

        var task1 = fileReads.ParseCsvParallelGroupedAsync(options, cancellationToken);
        var task2 = fileReads2.ParseCsvParallelGroupedAsync(options, cancellationToken);

        await Task.WhenAll(task1, task2).ConfigureAwait(false);

        return (await task1.ConfigureAwait(false), await task2.ConfigureAwait(false));
    }

    /// <summary>
    ///     Parses multiple CSV file groups in parallel with per-file configuration, returning records grouped by file path for each type.
    /// </summary>
    /// <typeparam name="T1">The first record type to parse.</typeparam>
    /// <typeparam name="T2">The second record type to parse.</typeparam>
    /// <typeparam name="T3">The third record type to parse.</typeparam>
    /// <param name="fileReads">First collection of files to parse.</param>
    /// <param name="fileReads2">Second collection of files to parse.</param>
    /// <param name="fileReads3">Third collection of files to parse.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A tuple of dictionaries, each mapping file paths to their parsed records.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any file collection is null.</exception>
    // ReSharper disable once MemberCanBeInternal
    public static async Task<(
        IReadOnlyDictionary<string, IReadOnlyList<T1>>,
        IReadOnlyDictionary<string, IReadOnlyList<T2>>,
        IReadOnlyDictionary<string, IReadOnlyList<T3>>)> ParseCsvParallelGroupedAsync<T1, T2, T3>(
        IEnumerable<RivuletCsvReadFile<T1>> fileReads,
        IEnumerable<RivuletCsvReadFile<T2>> fileReads2,
        IEnumerable<RivuletCsvReadFile<T3>> fileReads3,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
        where T1 : class
        where T2 : class
        where T3 : class
    {
        ArgumentNullException.ThrowIfNull(fileReads);
        ArgumentNullException.ThrowIfNull(fileReads2);
        ArgumentNullException.ThrowIfNull(fileReads3);

        var task1 = fileReads.ParseCsvParallelGroupedAsync(options, cancellationToken);
        var task2 = fileReads2.ParseCsvParallelGroupedAsync(options, cancellationToken);
        var task3 = fileReads3.ParseCsvParallelGroupedAsync(options, cancellationToken);

        await Task.WhenAll(task1, task2, task3).ConfigureAwait(false);

        return (await task1.ConfigureAwait(false), await task2.ConfigureAwait(false), await task3.ConfigureAwait(false));
    }

    /// <summary>
    ///     Parses multiple CSV file groups in parallel with per-file configuration, returning records grouped by file path for each type.
    /// </summary>
    /// <typeparam name="T1">The first record type to parse.</typeparam>
    /// <typeparam name="T2">The second record type to parse.</typeparam>
    /// <typeparam name="T3">The third record type to parse.</typeparam>
    /// <typeparam name="T4">The fourth record type to parse.</typeparam>
    /// <param name="fileReads">First collection of files to parse.</param>
    /// <param name="fileReads2">Second collection of files to parse.</param>
    /// <param name="fileReads3">Third collection of files to parse.</param>
    /// <param name="fileReads4">Fourth collection of files to parse.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A tuple of dictionaries, each mapping file paths to their parsed records.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any file collection is null.</exception>
    // ReSharper disable once MemberCanBeInternal
    public static async Task<(
        IReadOnlyDictionary<string, IReadOnlyList<T1>>,
        IReadOnlyDictionary<string, IReadOnlyList<T2>>,
        IReadOnlyDictionary<string, IReadOnlyList<T3>>,
        IReadOnlyDictionary<string, IReadOnlyList<T4>>)> ParseCsvParallelGroupedAsync<T1, T2, T3, T4>(
        IEnumerable<RivuletCsvReadFile<T1>> fileReads,
        IEnumerable<RivuletCsvReadFile<T2>> fileReads2,
        IEnumerable<RivuletCsvReadFile<T3>> fileReads3,
        IEnumerable<RivuletCsvReadFile<T4>> fileReads4,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
        where T1 : class
        where T2 : class
        where T3 : class
        where T4 : class
    {
        ArgumentNullException.ThrowIfNull(fileReads);
        ArgumentNullException.ThrowIfNull(fileReads2);
        ArgumentNullException.ThrowIfNull(fileReads3);
        ArgumentNullException.ThrowIfNull(fileReads4);

        var task1 = fileReads.ParseCsvParallelGroupedAsync(options, cancellationToken);
        var task2 = fileReads2.ParseCsvParallelGroupedAsync(options, cancellationToken);
        var task3 = fileReads3.ParseCsvParallelGroupedAsync(options, cancellationToken);
        var task4 = fileReads4.ParseCsvParallelGroupedAsync(options, cancellationToken);

        await Task.WhenAll(task1, task2, task3, task4).ConfigureAwait(false);

        return (await task1.ConfigureAwait(false), await task2.ConfigureAwait(false), await task3.ConfigureAwait(false), await task4.ConfigureAwait(false));
    }

    /// <summary>
    ///     Parses multiple CSV file groups in parallel with per-file configuration, returning records grouped by file path for each type.
    /// </summary>
    /// <typeparam name="T1">The first record type to parse.</typeparam>
    /// <typeparam name="T2">The second record type to parse.</typeparam>
    /// <typeparam name="T3">The third record type to parse.</typeparam>
    /// <typeparam name="T4">The fourth record type to parse.</typeparam>
    /// <typeparam name="T5">The fifth record type to parse.</typeparam>
    /// <param name="fileReads">First collection of files to parse.</param>
    /// <param name="fileReads2">Second collection of files to parse.</param>
    /// <param name="fileReads3">Third collection of files to parse.</param>
    /// <param name="fileReads4">Fourth collection of files to parse.</param>
    /// <param name="fileReads5">Fifth collection of files to parse.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A tuple of dictionaries, each mapping file paths to their parsed records.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any file collection is null.</exception>
    // ReSharper disable once MemberCanBeInternal
    public static async Task<(
        IReadOnlyDictionary<string, IReadOnlyList<T1>>,
        IReadOnlyDictionary<string, IReadOnlyList<T2>>,
        IReadOnlyDictionary<string, IReadOnlyList<T3>>,
        IReadOnlyDictionary<string, IReadOnlyList<T4>>,
        IReadOnlyDictionary<string, IReadOnlyList<T5>>)> ParseCsvParallelGroupedAsync<T1, T2, T3, T4, T5>(
        IEnumerable<RivuletCsvReadFile<T1>> fileReads,
        IEnumerable<RivuletCsvReadFile<T2>> fileReads2,
        IEnumerable<RivuletCsvReadFile<T3>> fileReads3,
        IEnumerable<RivuletCsvReadFile<T4>> fileReads4,
        IEnumerable<RivuletCsvReadFile<T5>> fileReads5,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
        where T1 : class
        where T2 : class
        where T3 : class
        where T4 : class
        where T5 : class
    {
        ArgumentNullException.ThrowIfNull(fileReads);
        ArgumentNullException.ThrowIfNull(fileReads2);
        ArgumentNullException.ThrowIfNull(fileReads3);
        ArgumentNullException.ThrowIfNull(fileReads4);
        ArgumentNullException.ThrowIfNull(fileReads5);

        var task1 = fileReads.ParseCsvParallelGroupedAsync(options, cancellationToken);
        var task2 = fileReads2.ParseCsvParallelGroupedAsync(options, cancellationToken);
        var task3 = fileReads3.ParseCsvParallelGroupedAsync(options, cancellationToken);
        var task4 = fileReads4.ParseCsvParallelGroupedAsync(options, cancellationToken);
        var task5 = fileReads5.ParseCsvParallelGroupedAsync(options, cancellationToken);

        await Task.WhenAll(task1, task2, task3, task4, task5).ConfigureAwait(false);

        return (await task1.ConfigureAwait(false), await task2.ConfigureAwait(false), await task3.ConfigureAwait(false), await task4.ConfigureAwait(false), await task5.ConfigureAwait(false));
    }

    /// <summary>
    ///     Writes collections of records to multiple CSV files in parallel with per-file configuration.
    /// </summary>
    /// <typeparam name="T">The type of record to write to CSV files.</typeparam>
    /// <param name="fileWrites">Collection of CSV file writes containing records and optional per-file configuration.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fileWrites is null.</exception>
    public static Task WriteCsvParallelAsync<T>(
        this IEnumerable<RivuletCsvWriteFile<T>> fileWrites,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        ArgumentNullException.ThrowIfNull(fileWrites);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        return fileWrites.ForEachParallelAsync(
            (write, ct) => WriteCsvFileAsync(write, options, ct),
            parallelOptions,
            cancellationToken);
    }

    /// <summary>
    ///     Writes multiple CSV file groups in parallel with per-file configuration.
    /// </summary>
    /// <typeparam name="T1">The first record type to write.</typeparam>
    /// <typeparam name="T2">The second record type to write.</typeparam>
    /// <param name="fileWrites">First collection of files to write.</param>
    /// <param name="fileWrites2">Second collection of files to write.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any file collection is null.</exception>
    // ReSharper disable once MemberCanBeInternal
    public static Task WriteCsvParallelAsync<T1, T2>(
        IEnumerable<RivuletCsvWriteFile<T1>> fileWrites,
        IEnumerable<RivuletCsvWriteFile<T2>> fileWrites2,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
        where T1 : class
        where T2 : class
    {
        ArgumentNullException.ThrowIfNull(fileWrites);
        ArgumentNullException.ThrowIfNull(fileWrites2);

        var task1 = fileWrites.WriteCsvParallelAsync(options, cancellationToken);
        var task2 = fileWrites2.WriteCsvParallelAsync(options, cancellationToken);

        return Task.WhenAll(task1, task2);
    }

    /// <summary>
    ///     Writes multiple CSV file groups in parallel with per-file configuration.
    /// </summary>
    /// <typeparam name="T1">The first record type to write.</typeparam>
    /// <typeparam name="T2">The second record type to write.</typeparam>
    /// <typeparam name="T3">The third record type to write.</typeparam>
    /// <param name="fileWrites">First collection of files to write.</param>
    /// <param name="fileWrites2">Second collection of files to write.</param>
    /// <param name="fileWrites3">Third collection of files to write.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any file collection is null.</exception>
    // ReSharper disable once MemberCanBeInternal
    public static Task WriteCsvParallelAsync<T1, T2, T3>(
        IEnumerable<RivuletCsvWriteFile<T1>> fileWrites,
        IEnumerable<RivuletCsvWriteFile<T2>> fileWrites2,
        IEnumerable<RivuletCsvWriteFile<T3>> fileWrites3,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
        where T1 : class
        where T2 : class
        where T3 : class
    {
        ArgumentNullException.ThrowIfNull(fileWrites);
        ArgumentNullException.ThrowIfNull(fileWrites2);
        ArgumentNullException.ThrowIfNull(fileWrites3);

        var task1 = fileWrites.WriteCsvParallelAsync(options, cancellationToken);
        var task2 = fileWrites2.WriteCsvParallelAsync(options, cancellationToken);
        var task3 = fileWrites3.WriteCsvParallelAsync(options, cancellationToken);

        return Task.WhenAll(task1, task2, task3);
    }

    /// <summary>
    ///     Writes multiple CSV file groups in parallel with per-file configuration.
    /// </summary>
    /// <typeparam name="T1">The first record type to write.</typeparam>
    /// <typeparam name="T2">The second record type to write.</typeparam>
    /// <typeparam name="T3">The third record type to write.</typeparam>
    /// <typeparam name="T4">The fourth record type to write.</typeparam>
    /// <param name="fileWrites">First collection of files to write.</param>
    /// <param name="fileWrites2">Second collection of files to write.</param>
    /// <param name="fileWrites3">Third collection of files to write.</param>
    /// <param name="fileWrites4">Fourth collection of files to write.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any file collection is null.</exception>
    // ReSharper disable once MemberCanBeInternal
    public static Task WriteCsvParallelAsync<T1, T2, T3, T4>(
        IEnumerable<RivuletCsvWriteFile<T1>> fileWrites,
        IEnumerable<RivuletCsvWriteFile<T2>> fileWrites2,
        IEnumerable<RivuletCsvWriteFile<T3>> fileWrites3,
        IEnumerable<RivuletCsvWriteFile<T4>> fileWrites4,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
        where T1 : class
        where T2 : class
        where T3 : class
        where T4 : class
    {
        ArgumentNullException.ThrowIfNull(fileWrites);
        ArgumentNullException.ThrowIfNull(fileWrites2);
        ArgumentNullException.ThrowIfNull(fileWrites3);
        ArgumentNullException.ThrowIfNull(fileWrites4);

        var task1 = fileWrites.WriteCsvParallelAsync(options, cancellationToken);
        var task2 = fileWrites2.WriteCsvParallelAsync(options, cancellationToken);
        var task3 = fileWrites3.WriteCsvParallelAsync(options, cancellationToken);
        var task4 = fileWrites4.WriteCsvParallelAsync(options, cancellationToken);

        return Task.WhenAll(task1, task2, task3, task4);
    }

    /// <summary>
    ///     Writes multiple CSV file groups in parallel with per-file configuration.
    /// </summary>
    /// <typeparam name="T1">The first record type to write.</typeparam>
    /// <typeparam name="T2">The second record type to write.</typeparam>
    /// <typeparam name="T3">The third record type to write.</typeparam>
    /// <typeparam name="T4">The fourth record type to write.</typeparam>
    /// <typeparam name="T5">The fifth record type to write.</typeparam>
    /// <param name="fileWrites">First collection of files to write.</param>
    /// <param name="fileWrites2">Second collection of files to write.</param>
    /// <param name="fileWrites3">Third collection of files to write.</param>
    /// <param name="fileWrites4">Fourth collection of files to write.</param>
    /// <param name="fileWrites5">Fifth collection of files to write.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any file collection is null.</exception>
    // ReSharper disable once MemberCanBeInternal
    public static Task WriteCsvParallelAsync<T1, T2, T3, T4, T5>(
        IEnumerable<RivuletCsvWriteFile<T1>> fileWrites,
        IEnumerable<RivuletCsvWriteFile<T2>> fileWrites2,
        IEnumerable<RivuletCsvWriteFile<T3>> fileWrites3,
        IEnumerable<RivuletCsvWriteFile<T4>> fileWrites4,
        IEnumerable<RivuletCsvWriteFile<T5>> fileWrites5,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
        where T1 : class
        where T2 : class
        where T3 : class
        where T4 : class
        where T5 : class
    {
        ArgumentNullException.ThrowIfNull(fileWrites);
        ArgumentNullException.ThrowIfNull(fileWrites2);
        ArgumentNullException.ThrowIfNull(fileWrites3);
        ArgumentNullException.ThrowIfNull(fileWrites4);
        ArgumentNullException.ThrowIfNull(fileWrites5);

        var task1 = fileWrites.WriteCsvParallelAsync(options, cancellationToken);
        var task2 = fileWrites2.WriteCsvParallelAsync(options, cancellationToken);
        var task3 = fileWrites3.WriteCsvParallelAsync(options, cancellationToken);
        var task4 = fileWrites4.WriteCsvParallelAsync(options, cancellationToken);
        var task5 = fileWrites5.WriteCsvParallelAsync(options, cancellationToken);

        return Task.WhenAll(task1, task2, task3, task4, task5);
    }

    /// <summary>
    ///     Transforms CSV files in parallel with per-file configuration, applying a synchronous transformation function.
    /// </summary>
    /// <typeparam name="TIn">The input record type to read from CSV files.</typeparam>
    /// <typeparam name="TOut">The output record type to write to CSV files.</typeparam>
    /// <param name="transformations">Collection of input/output CSV file pairs with optional per-file configuration.</param>
    /// <param name="transform">Synchronous function to transform input records to output records.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when transformations or transform is null.</exception>
    public static Task TransformCsvParallelAsync<TIn, TOut>(
        this IEnumerable<(RivuletCsvReadFile<TIn> Input, RivuletCsvWriteFile<TOut> Output)> transformations,
        Func<TIn, TOut> transform,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
        where TIn : class
        where TOut : class
    {
        ArgumentNullException.ThrowIfNull(transformations);
        ArgumentNullException.ThrowIfNull(transform);

        return TransformCsvParallelAsync(
            transformations,
            (record, _) => new ValueTask<TOut>(transform(record)),
            options,
            cancellationToken
        );
    }

    /// <summary>
    ///     Transforms CSV files in parallel with per-file configuration, applying an asynchronous transformation function.
    /// </summary>
    /// <typeparam name="TIn">The input record type to read from CSV files.</typeparam>
    /// <typeparam name="TOut">The output record type to write to CSV files.</typeparam>
    /// <param name="transformations">Collection of input/output CSV file pairs with optional per-file configuration.</param>
    /// <param name="transformAsync">Asynchronous function to transform input records to output records. Receives the record and a cancellation token.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when transformations or transformAsync is null.</exception>
    // ReSharper disable once MemberCanBeInternal
    public static Task TransformCsvParallelAsync<TIn, TOut>(
        this IEnumerable<(RivuletCsvReadFile<TIn> Input, RivuletCsvWriteFile<TOut> Output)> transformations,
        Func<TIn, CancellationToken, ValueTask<TOut>> transformAsync,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
        where TIn : class
        where TOut : class
    {
        ArgumentNullException.ThrowIfNull(transformations);
        ArgumentNullException.ThrowIfNull(transformAsync);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        return transformations.ForEachParallelAsync(
            async (item, ct) =>
            {
                var inputRecords = await ParseCsvFileAsync<TIn>(
                    item.Input,
                    options,
                    ct
                ).ConfigureAwait(false);

                var outputRecords = new List<TOut>(inputRecords.Count);
                foreach (var inputRecord in inputRecords)
                    outputRecords.Add(await transformAsync(inputRecord, ct).ConfigureAwait(false));

                await WriteCsvFileAsync(
                    new RivuletCsvWriteFile<TOut>(item.Output.Path, outputRecords, item.Output.Configuration),
                    options,
                    ct
                ).ConfigureAwait(false);
            },
            parallelOptions,
            cancellationToken
        );
    }

    /// <summary>
    ///     Core streaming primitive that yields CSV records from a single file with proper resource lifetime management.
    /// </summary>
    /// <typeparam name="T">The type of record to parse from the CSV file.</typeparam>
    /// <param name="fileConfig">Configuration for the CSV file including path and per-file settings.</param>
    /// <param name="options">CSV operation options including encoding, buffer size, and callbacks.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the streaming operation.</param>
    /// <returns>An async enumerable stream of parsed CSV records.</returns>
    private static async IAsyncEnumerable<T> StreamCsvFileInternalAsync<T>(
        RivuletCsvFile fileConfig,
        CsvOperationOptions options,
        [EnumeratorCancellation]
        CancellationToken cancellationToken
    )
        where T : class
    {
        if (options.OnFileStartAsync != null)
            await options.OnFileStartAsync(fileConfig.Path).ConfigureAwait(false);

        Stream? stream = null;
        StreamReader? reader = null;
        CsvReader? csv = null;
        long recordCount = 0;

        try
        {
            stream = CsvOperationHelper.CreateReadStream(fileConfig.Path, options);
            reader = new StreamReader(stream, options.Encoding);

            var readCsvConfig = CreateAndConfigureCsvConfiguration(fileConfig, options);

            csv = new CsvReader(reader, readCsvConfig);

            ConfigureCsvContext(fileConfig, options, csv.Context);

            await foreach (var record in csv.GetRecordsAsync<T>(cancellationToken).ConfigureAwait(false))
            {
                recordCount++;
                yield return record;
            }

            if (options.OnFileCompleteAsync == null)
                yield break;

            var result = new FileOperationResult
            {
                BytesProcessed = stream.Position,
                RecordCount = recordCount
            };
            await options.OnFileCompleteAsync(fileConfig.Path, result).ConfigureAwait(false);
        }
        finally
        {
            csv?.Dispose();
            reader?.Dispose();
            if (stream != null)
                await stream.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Materializes all records from a single CSV file into memory.
    /// </summary>
    /// <typeparam name="T">The type of record to parse from the CSV file.</typeparam>
    /// <param name="csvFile">Configuration for the CSV file including path and per-file settings.</param>
    /// <param name="options">CSV operation options including encoding, buffer size, and callbacks.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the parse operation.</param>
    /// <returns>A read-only list containing all parsed CSV records from the file.</returns>
    private static async ValueTask<IReadOnlyList<T>> ParseCsvFileAsync<T>(
        RivuletCsvFile csvFile,
        CsvOperationOptions options,
        CancellationToken cancellationToken
    )
        where T : class
    {
        var records = new List<T>();
        await foreach (var record in StreamCsvFileInternalAsync<T>(csvFile, options, cancellationToken).ConfigureAwait(false))
            records.Add(record);

        return records;
    }

    /// <summary>
    ///     Creates and configures a CsvConfiguration by applying per-file or default configuration actions.
    /// </summary>
    /// <param name="fileConfig">File-specific configuration that may override defaults.</param>
    /// <param name="options">Global CSV operation options containing default configuration.</param>
    /// <returns>A configured CsvConfiguration instance ready for use with CsvHelper.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static CsvConfiguration CreateAndConfigureCsvConfiguration(
        RivuletCsvFile fileConfig,
        CsvOperationOptions options
    )
    {
        var csvConfig = new CsvConfiguration(options.Culture);

        if (fileConfig.Configuration != null)
            fileConfig.Configuration.ConfigurationAction?.Invoke(csvConfig);
        else
            options.FileConfiguration.ConfigurationAction?.Invoke(csvConfig);

        return csvConfig;
    }

    /// <summary>
    ///     Configures the CsvHelper context by applying per-file or default context actions (ClassMap registration, type conversion, etc.).
    /// </summary>
    /// <param name="fileConfig">File-specific configuration that may override defaults.</param>
    /// <param name="options">Global CSV operation options containing default configuration.</param>
    /// <param name="context">The CsvHelper context to configure.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ConfigureCsvContext(
        RivuletCsvFile fileConfig,
        CsvOperationOptions options,
        CsvContext context
    )
    {
        if (fileConfig.Configuration != null)
            fileConfig.Configuration.CsvContextAction?.Invoke(context);
        else
            options.FileConfiguration.CsvContextAction?.Invoke(context);
    }

    /// <summary>
    ///     Writes a collection of records to a single CSV file with lifecycle callbacks.
    /// </summary>
    /// <typeparam name="T">The type of record to write to the CSV file.</typeparam>
    /// <param name="write">The write operation configuration containing records, path, and per-file settings.</param>
    /// <param name="options">CSV operation options including encoding, buffer size, and callbacks.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the write operation.</param>
    /// <returns>A ValueTask representing the asynchronous write operation.</returns>
    private static ValueTask WriteCsvFileAsync<T>(
        RivuletCsvWriteFile<T> write,
        CsvOperationOptions options,
        CancellationToken cancellationToken = default
    )
        where T : class =>
        CsvOperationHelper.ExecuteCsvOperationAsync(
            write.Path,
            async () =>
            {
                CsvOperationHelper.EnsureDirectoryExists(write.Path, options);
                CsvOperationHelper.ValidateOverwrite(write.Path, options);

#pragma warning disable CA2007 // ConfigureAwait not applicable to await using
                await using var stream = CsvOperationHelper.CreateWriteStream(write.Path, options);
                await using var writer = new StreamWriter(stream, options.Encoding);
#pragma warning restore CA2007

                var writeCsvConfig = CreateAndConfigureCsvConfiguration(write, options);

#pragma warning disable CA2007 // ConfigureAwait not applicable to await using
                await using var csv = new CsvWriter(writer, writeCsvConfig);
#pragma warning restore CA2007

                ConfigureCsvContext(write, options, csv.Context);

                await csv.WriteRecordsAsync(write.Records, cancellationToken).ConfigureAwait(false);
                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

                return new FileOperationResult
                {
                    BytesProcessed = stream.Position,
                    RecordCount = write.Records.Count
                };
            },
            options);
}
