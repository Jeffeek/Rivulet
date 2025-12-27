using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using CsvHelper;
using CsvHelper.Configuration;
using Rivulet.Core;
using Rivulet.Csv.Internal;

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
    public static async Task<IReadOnlyList<T>> ParseCsvParallelAsync<T>(
        this IEnumerable<string> filePaths,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        var fileResults = await filePaths.SelectParallelAsync(
            (filePath, ct) => ParseCsvFileAsync<T>(filePath, options, null, ct),
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
    /// <param name="fileReads">Collection of tuples containing file path and per-file configuration.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A dictionary mapping file paths to their parsed records.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fileReads or any configuration is null.</exception>
    /// <remarks>
    ///     This method groups results by file path, useful when you need to track which records came from which file.
    ///     For a flattened list of all records, use <see cref="ParseCsvParallelAsync{T}"/>.
    /// </remarks>
    public static async Task<IReadOnlyDictionary<string, IReadOnlyList<object>>> ParseCsvParallelGroupedAsync(
        this IEnumerable<(string FilePath, CsvFileConfiguration Configuration)> fileReads,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
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
        CancellationToken cancellationToken = default
    )
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
        CancellationToken cancellationToken = default
    )
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

    /// <summary>
    ///     Core streaming primitive that yields CSV records from a single file with proper resource lifetime management.
    /// </summary>
    private static async IAsyncEnumerable<T> StreamCsvFileInternalAsync<T>(
        string filePath,
        CsvOperationOptions options,
        CsvFileConfiguration? fileConfig,
        [EnumeratorCancellation]
        CancellationToken cancellationToken
    )
    {
        if (options.OnFileStartAsync != null)
            await options.OnFileStartAsync(filePath).ConfigureAwait(false);

        Stream? stream = null;
        StreamReader? reader = null;
        CsvReader? csv = null;
        long recordCount = 0;

        try
        {
            stream = CsvOperationHelper.CreateReadStream(filePath, options);
            reader = new StreamReader(stream, options.Encoding);

            var readCsvConfig = new CsvConfiguration(options.Culture);

            if (fileConfig != null)
                fileConfig.ConfigurationAction?.Invoke(readCsvConfig);
            else
                options.FileConfiguration.ConfigurationAction?.Invoke(readCsvConfig);

            csv = new CsvReader(reader, readCsvConfig);

            if (fileConfig != null)
                fileConfig.CsvContextAction?.Invoke(csv.Context);
            else
                options.FileConfiguration.CsvContextAction?.Invoke(csv.Context);

            await foreach (var record in csv.GetRecordsAsync<T>(cancellationToken).ConfigureAwait(false))
            {
                recordCount++;
                yield return record;
            }

            if (options.OnFileCompleteAsync != null)
                await options.OnFileCompleteAsync(filePath, recordCount).ConfigureAwait(false);
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
    private static async ValueTask<IReadOnlyList<T>> ParseCsvFileAsync<T>(
        string filePath,
        CsvOperationOptions options,
        CsvFileConfiguration? fileConfig,
        CancellationToken cancellationToken
    )
    {
        var records = new List<T>();
        await foreach (var record in StreamCsvFileInternalAsync<T>(filePath, options, fileConfig, cancellationToken).ConfigureAwait(false))
            records.Add(record);

        return records;
    }

    private static ValueTask WriteCsvFileAsync<T>(
        string filePath,
        IEnumerable<T> records,
        CsvOperationOptions options,
        CsvFileConfiguration? fileConfig = null,
        CancellationToken cancellationToken = default
    ) =>
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
                    fileConfig.ConfigurationAction?.Invoke(writeCsvConfig);
                else
                    options.FileConfiguration.ConfigurationAction?.Invoke(writeCsvConfig);

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
    ///     Transforms CSV files in parallel by reading records, applying a synchronous transformation function, and writing results.
    /// </summary>
    /// <typeparam name="TIn">The input record type to read from CSV files.</typeparam>
    /// <typeparam name="TOut">The output record type to write to CSV files.</typeparam>
    /// <param name="transformations">Collection of tuples containing input path and output path.</param>
    /// <param name="transform">Synchronous function to transform input records to output records.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when transformations or transform is null.</exception>
    public static Task TransformCsvParallelAsync<TIn, TOut>(
        this IEnumerable<(string InputPath, string OutputPath)> transformations,
        Func<TIn, TOut> transform,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(transformations);
        ArgumentNullException.ThrowIfNull(transform);

        return TransformCsvParallelCoreAsync<TIn, TOut>(
            transformations.Select(static x =>
                (x.InputPath, x.OutputPath, (CsvFileConfiguration?)null, (CsvFileConfiguration?)null)),
            (record, _) => new ValueTask<TOut>(transform(record)),
            options,
            cancellationToken
        );
    }

    /// <summary>
    ///     Transforms CSV files in parallel by reading records, applying an asynchronous transformation function, and writing results.
    /// </summary>
    /// <typeparam name="TIn">The input record type to read from CSV files.</typeparam>
    /// <typeparam name="TOut">The output record type to write to CSV files.</typeparam>
    /// <param name="transformations">Collection of tuples containing input path and output path.</param>
    /// <param name="transformAsync">Asynchronous function to transform input records to output records.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when transformations or transform is null.</exception>
    public static Task TransformCsvParallelAsync<TIn, TOut>(
        this IEnumerable<(string InputPath, string OutputPath)> transformations,
        Func<TIn, CancellationToken, ValueTask<TOut>> transformAsync,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(transformations);
        ArgumentNullException.ThrowIfNull(transformAsync);

        return TransformCsvParallelCoreAsync<TIn, TOut>(
            transformations.Select(static x =>
                (x.InputPath, x.OutputPath, (CsvFileConfiguration?)null, (CsvFileConfiguration?)null)),
            transformAsync,
            options,
            cancellationToken
        );
    }

    /// <summary>
    ///     Transforms CSV files in parallel with per-file configuration, applying a synchronous transformation function.
    /// </summary>
    /// <typeparam name="TIn">The input record type to read from CSV files.</typeparam>
    /// <typeparam name="TOut">The output record type to write to CSV files.</typeparam>
    /// <param name="transformations">Collection of tuples containing input/output paths and configurations.</param>
    /// <param name="transform">Synchronous function to transform input records to output records.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when transformations or transform is null.</exception>
    public static Task TransformCsvParallelAsync<TIn, TOut>(
        this IEnumerable<(string InputPath, string OutputPath, CsvFileConfiguration? InputConfig, CsvFileConfiguration?
            OutputConfig)> transformations,
        Func<TIn, TOut> transform,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(transformations);
        ArgumentNullException.ThrowIfNull(transform);

        return TransformCsvParallelCoreAsync<TIn, TOut>(
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
    /// <param name="transformations">Collection of tuples containing input/output paths and configurations.</param>
    /// <param name="transformAsync">Asynchronous function to transform input records to output records. Receives the record and a cancellation token.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when transformations or transform is null.</exception>
    public static Task TransformCsvParallelAsync<TIn, TOut>(
        this IEnumerable<(string InputPath, string OutputPath, CsvFileConfiguration? InputConfig, CsvFileConfiguration?
            OutputConfig)> transformations,
        Func<TIn, CancellationToken, ValueTask<TOut>> transformAsync,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(transformations);
        ArgumentNullException.ThrowIfNull(transformAsync);

        return TransformCsvParallelCoreAsync<TIn, TOut>(transformations, transformAsync, options, cancellationToken);
    }

    /// <summary>
    ///     Core implementation for CSV transformation operations. Consolidates logic for all transform overloads.
    /// </summary>
    private static Task TransformCsvParallelCoreAsync<TIn, TOut>(
        IEnumerable<(string InputPath, string OutputPath, CsvFileConfiguration? InputConfig, CsvFileConfiguration?
            OutputConfig)> transformations,
        Func<TIn, CancellationToken, ValueTask<TOut>> transformAsync,
        CsvOperationOptions? options,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(transformations);
        ArgumentNullException.ThrowIfNull(transformAsync);

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
                    outputRecords.Add(await transformAsync(inputRecord, ct).ConfigureAwait(false));

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
    ///     Streams records from a single CSV file asynchronously without materializing all records in memory.
    /// </summary>
    /// <typeparam name="T">The type of record to parse from the CSV file.</typeparam>
    /// <param name="filePath">The file path to parse.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>An async enumerable that yields records from the CSV file.</returns>
    /// <exception cref="ArgumentNullException">Thrown when filePath is null.</exception>
    /// <remarks>
    ///     This method streams records one at a time, keeping only one record in memory.
    ///     Resources (file handles, streams) are automatically disposed after enumeration completes.
    /// </remarks>
    public static IAsyncEnumerable<T> StreamCsvAsync<T>(
        this string filePath,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(filePath);
        options ??= new();
        return StreamCsvFileInternalAsync<T>(filePath, options, null, cancellationToken);
    }

    /// <summary>
    ///     Streams records from multiple CSV files in parallel, yielding records as they are parsed.
    /// </summary>
    /// <typeparam name="T">The type of record to parse from CSV files.</typeparam>
    /// <param name="filePaths">Collection of file paths to parse.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>An async enumerable that yields records from all files as they are parsed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when filePaths is null.</exception>
    /// <remarks>
    ///     Unlike <see cref="ParseCsvParallelAsync{T}"/>, this method does not wait for all files
    ///     to complete before yielding results. Records are yielded as soon as they are available
    ///     from any file, enabling memory-efficient processing of large datasets.
    ///     Files are processed with bounded parallelism controlled by ParallelOptions.
    ///     Order is non-deterministic unless <see cref="ParallelOptionsRivulet.OrderedOutput"/> is enabled.
    ///     For sequential processing with predictable order, use <see cref="StreamCsvSequentialAsync{T}(IEnumerable{string}, CsvOperationOptions?, CancellationToken)"/>.
    /// </remarks>
    public static async IAsyncEnumerable<T> StreamCsvParallelAsync<T>(
        this IEnumerable<string> filePaths,
        CsvOperationOptions? options = null,
        [EnumeratorCancellation]
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        // Convert IEnumerable to IAsyncEnumerable, then use SelectParallelStreamAsync
        await foreach (var records in filePaths.ToAsyncEnumerable()
                           .SelectParallelStreamAsync(
                               (filePath, ct) => ParseCsvFileAsync<T>(filePath, options, null, ct),
                               parallelOptions,
                               cancellationToken)
                           .ConfigureAwait(false))
        {
            // Flatten the list of records from each file
            foreach (var record in records)
                yield return record;
        }
    }

    /// <summary>
    ///     Streams records from multiple CSV files sequentially, yielding records from each file in order.
    /// </summary>
    /// <typeparam name="T">The type of record to parse from CSV files.</typeparam>
    /// <param name="filePaths">Collection of file paths to parse.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>An async enumerable that yields records from all files sequentially.</returns>
    /// <exception cref="ArgumentNullException">Thrown when filePaths is null.</exception>
    /// <remarks>
    ///     Files are processed sequentially in the order provided. All records from the first file
    ///     are yielded before moving to the second file, and so on. This is memory-efficient as only
    ///     one record from one file is kept in memory at a time.
    /// </remarks>
    public static async IAsyncEnumerable<T> StreamCsvSequentialAsync<T>(
        this IEnumerable<string> filePaths,
        CsvOperationOptions? options = null,
        [EnumeratorCancellation]
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        options ??= new();

        foreach (var filePath in filePaths)
        {
            await foreach (var record in StreamCsvFileInternalAsync<T>(filePath, options, null, cancellationToken).ConfigureAwait(false))
                yield return record;
        }
    }

    /// <summary>
    ///     Streams records from multiple CSV files with per-file configuration, processing files sequentially.
    /// </summary>
    /// <typeparam name="T">The type of record to parse from CSV files.</typeparam>
    /// <param name="fileReads">Collection of tuples containing file path and per-file configuration.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>An async enumerable that yields records from all files sequentially.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fileReads is null.</exception>
    public static async IAsyncEnumerable<T> StreamCsvSequentialAsync<T>(
        this IEnumerable<(string FilePath, CsvFileConfiguration Configuration)> fileReads,
        CsvOperationOptions? options = null,
        [EnumeratorCancellation]
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(fileReads);
        options ??= new();

        foreach (var (filePath, fileConfig) in fileReads)
        {
            ArgumentNullException.ThrowIfNull(fileConfig);
            await foreach (var record in StreamCsvFileInternalAsync<T>(filePath, options, fileConfig, cancellationToken).ConfigureAwait(false))
                yield return record;
        }
    }
}
