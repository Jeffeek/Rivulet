using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using CsvHelper;
using CsvHelper.Configuration;
using Rivulet.Core;
using Rivulet.Csv.Internal;

namespace Rivulet.Csv;

/// <summary>
///     Extension methods for parallel CSV parsing and writing operations with bounded concurrency and resilience.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
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
    public static async Task<IReadOnlyList<T>> ParseCsvParallelAsync<T>(
        this IEnumerable<string> filePaths,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();
        var bag = new ConcurrentBag<T>();

        await filePaths.ForEachParallelAsync(async (filePath, ct) =>
            {
                var records = await ParseCsvFileAsync<T>(filePath, options, null, ct).ConfigureAwait(false);

                foreach (var record in records)
                    bag.Add(record);
            },
            parallelOptions,
            cancellationToken).ConfigureAwait(false);

        return bag.ToList();
    }

    /// <summary>
    ///     Parses multiple CSV files in parallel with per-file configuration, returning records grouped by file path.
    /// </summary>
    /// <param name="fileReads">Collection of tuples containing file path and per-file configuration.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A dictionary mapping file paths to their parsed records.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fileReads or any configuration is null.</exception>
    public static async Task<IReadOnlyDictionary<string, IReadOnlyList<object>>> ParseCsvParallelAsync(
        this IEnumerable<(string FilePath, CsvFileConfiguration Configuration)> fileReads,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileReads);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();
        var dictionary = new ConcurrentDictionary<string, IReadOnlyList<object>>();

        await fileReads.ForEachParallelAsync(
                async (read, ct) =>
                {
                    ArgumentNullException.ThrowIfNull(read.Configuration);

                    var records = await ParseCsvFileAsync<object>(
                        read.FilePath,
                        options,
                        read.Configuration,
                        ct
                    ).ConfigureAwait(false);

                    dictionary.TryAdd(read.FilePath, records);
                },
                parallelOptions,
                cancellationToken)
            .ConfigureAwait(false);

        return dictionary;
    }

    /// <summary>
    ///     Writes collections of records to multiple CSV files in parallel using default configuration.
    /// </summary>
    /// <typeparam name="T">The type of record to write to CSV files.</typeparam>
    /// <param name="fileWrites">Collection of tuples containing file path and records to write.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fileWrites is null.</exception>
    public static Task WriteCsvParallelAsync<T>(
        this IEnumerable<(string FilePath, IEnumerable<T> Records)> fileWrites,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new();

        return WriteCsvParallelAsync(
            fileWrites.Select(x => (x.FilePath, x.Records, options.FileConfiguration)),
            options,
            cancellationToken
        );
    }

    /// <summary>
    ///     Writes collections of records to multiple CSV files in parallel with per-file configuration.
    /// </summary>
    /// <typeparam name="T">The type of record to write to CSV files.</typeparam>
    /// <param name="fileWrites">Collection of tuples containing file path, records, and configuration.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of file paths that were written successfully.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fileWrites is null.</exception>
    public static Task WriteCsvParallelAsync<T>(
        this IEnumerable<(string FilePath, IEnumerable<T> Records, CsvFileConfiguration Configuration)> fileWrites,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileWrites);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        return fileWrites.ForEachParallelAsync((write, ct) =>
                WriteCsvFileAsync(
                    write.FilePath,
                    write.Records,
                    options,
                    write.Configuration,
                    ct
                ),
            parallelOptions,
            cancellationToken);
    }

    private static ValueTask<IReadOnlyList<T>> ParseCsvFileAsync<T>(
        string filePath,
        CsvOperationOptions options,
        CsvFileConfiguration? fileConfig = null,
        CancellationToken cancellationToken = default) =>
        CsvOperationHelper.ExecuteCsvOperationAsync(
            filePath,
            async () =>
            {
#pragma warning disable CA2007 // ConfigureAwait not applicable to await using
                await using var stream = CsvOperationHelper.CreateReadStream(filePath, options);
#pragma warning restore CA2007
                using var reader = new StreamReader(stream, options.Encoding);

                var readCsvConfig = new CsvConfiguration(options.Culture);

                if (fileConfig != null)
                    fileConfig.ReaderConfigurationAction?.Invoke(readCsvConfig);
                else
                    options.FileConfiguration.ReaderConfigurationAction?.Invoke(readCsvConfig);

                using var csv = new CsvReader(reader, readCsvConfig);

                if (fileConfig != null)
                    fileConfig.CsvContextAction?.Invoke(csv.Context);
                else
                    options.FileConfiguration.CsvContextAction?.Invoke(csv.Context);

                var records = new List<T>();
                await foreach (var record in csv.GetRecordsAsync<T>(cancellationToken).ConfigureAwait(false))
                    records.Add(record);

                return (IReadOnlyList<T>)records;
            },
            options,
            static records => records.Count);

    private static ValueTask WriteCsvFileAsync<T>(
        string filePath,
        IEnumerable<T> records,
        CsvOperationOptions options,
        CsvFileConfiguration? fileConfig = null,
        CancellationToken cancellationToken = default) =>
        CsvOperationHelper.ExecuteCsvOperationAsync(
            filePath,
            async () =>
            {
                CsvOperationHelper.EnsureDirectoryExists(filePath, options);
                CsvOperationHelper.ValidateOverwrite(filePath, options);

#pragma warning disable CA2007 // ConfigureAwait not applicable to await using
                await using var stream = CsvOperationHelper.CreateWriteStream(filePath, options);
                await using var writer = new StreamWriter(stream, options.Encoding);
#pragma warning restore CA2007

                var writeCsvConfig = new CsvConfiguration(options.Culture);

                if (fileConfig != null)
                    fileConfig.WriterConfigurationAction?.Invoke(writeCsvConfig);
                else
                    options.FileConfiguration.WriterConfigurationAction?.Invoke(writeCsvConfig);

#pragma warning disable CA2007 // ConfigureAwait not applicable to await using
                await using var csv = new CsvWriter(writer, writeCsvConfig);
#pragma warning restore CA2007

                if (fileConfig != null)
                    fileConfig.CsvContextAction?.Invoke(csv.Context);
                else
                    options.FileConfiguration.CsvContextAction?.Invoke(csv.Context);

                var array = records as T[] ?? records.ToArray();
                await csv.WriteRecordsAsync(array, cancellationToken).ConfigureAwait(false);
                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

                return array.Length;
            },
            options);

    /// <summary>
    ///     Transforms CSV files in parallel by reading records, applying a transformation function, and writing results.
    /// </summary>
    /// <typeparam name="TIn">The input record type to read from CSV files.</typeparam>
    /// <typeparam name="TOut">The output record type to write to CSV files.</typeparam>
    /// <param name="transformations">Collection of tuples containing input path and output path.</param>
    /// <param name="transform">Function to transform input records to output records.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when transformations or transform is null.</exception>
    public static Task TransformCsvParallelAsync<TIn, TOut>(
        this IEnumerable<(string InputPath, string OutputPath)> transformations,
        Func<TIn, TOut> transform,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transformations);
        ArgumentNullException.ThrowIfNull(transform);

        return TransformCsvParallelAsync(
            transformations.Select(static x => (x.InputPath, x.OutputPath, (CsvFileConfiguration?)null, (CsvFileConfiguration?)null)),
            transform,
            options,
            cancellationToken
        );
    }

    /// <summary>
    ///     Transforms CSV files in parallel with per-file configuration for input and output.
    /// </summary>
    /// <typeparam name="TIn">The input record type to read from CSV files.</typeparam>
    /// <typeparam name="TOut">The output record type to write to CSV files.</typeparam>
    /// <param name="transformations">Collection of tuples containing input/output paths and configurations.</param>
    /// <param name="transform">Function to transform input records to output records.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when transformations or transform is null.</exception>
    public static Task TransformCsvParallelAsync<TIn, TOut>(
        this IEnumerable<(string InputPath, string OutputPath, CsvFileConfiguration? InputConfig, CsvFileConfiguration? OutputConfig)> transformations,
        Func<TIn, TOut> transform,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transformations);
        ArgumentNullException.ThrowIfNull(transform);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        return transformations.ForEachParallelAsync(
            async (item, ct) =>
            {
                var inputRecords = await ParseCsvFileAsync<TIn>(
                    item.InputPath,
                    options,
                    item.InputConfig,
                    ct
                ).ConfigureAwait(false);

                var outputRecords = inputRecords.Select(transform).ToList();

                await WriteCsvFileAsync(
                    item.OutputPath,
                    outputRecords,
                    options,
                    item.OutputConfig,
                    ct
                ).ConfigureAwait(false);
            },
            parallelOptions,
            cancellationToken
        );
    }

    /// <summary>
    ///     Transforms CSV files in parallel with per-file configuration for input and output.
    /// </summary>
    /// <typeparam name="TIn">The input record type to read from CSV files.</typeparam>
    /// <typeparam name="TOut">The output record type to write to CSV files.</typeparam>
    /// <param name="transformations">Collection of tuples containing input/output paths and configurations.</param>
    /// <param name="transform">Task to transform input records to output records.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when transformations or transform is null.</exception>
    public static Task TransformCsvParallelAsync<TIn, TOut>(
        this IEnumerable<(string InputPath, string OutputPath, CsvFileConfiguration? InputConfig, CsvFileConfiguration? OutputConfig)> transformations,
        Func<TIn, Task<TOut>> transform,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transformations);
        ArgumentNullException.ThrowIfNull(transform);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        return transformations.ForEachParallelAsync(
            async (item, ct) =>
            {
                var inputRecords = await ParseCsvFileAsync<TIn>(
                    item.InputPath,
                    options,
                    item.InputConfig,
                    ct
                ).ConfigureAwait(false);

                var outputRecords = new List<TOut>(inputRecords.Count);

                foreach (var inputRecord in inputRecords)
                    outputRecords.Add(await transform(inputRecord).ConfigureAwait(false));

                await WriteCsvFileAsync(
                    item.OutputPath,
                    outputRecords,
                    options,
                    item.OutputConfig,
                    ct
                ).ConfigureAwait(false);
            },
            parallelOptions,
            cancellationToken
        );
    }
}
