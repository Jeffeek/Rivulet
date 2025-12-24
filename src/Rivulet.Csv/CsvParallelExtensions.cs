using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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
                    );

                    dictionary.TryAdd(read.FilePath, records);
                },
                parallelOptions,
                cancellationToken)
            .ConfigureAwait(false);

        return dictionary;
    }

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
            {
                ArgumentNullException.ThrowIfNull(write.Configuration);

                return WriteCsvFileAsync(
                    write.FilePath,
                    write.Records,
                    options,
                    write.Configuration,
                    ct
                );
            },
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
                await using var stream = CsvOperationHelper.CreateReadStream(filePath, options);
                using var reader = new StreamReader(stream, options.Encoding);

                var readCsvConfig = new CsvConfiguration(options.Culture);

                if (fileConfig != null)
                    fileConfig.Value.ReaderConfigurationAction?.Invoke(readCsvConfig);
                else
                    options.FileConfiguration.ReaderConfigurationAction?.Invoke(readCsvConfig);

                using var csv = new CsvReader(reader, readCsvConfig);

                if (fileConfig != null)
                    fileConfig.Value.CsvContextAction?.Invoke(csv.Context);
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

                await using var stream = CsvOperationHelper.CreateWriteStream(filePath, options);
                await using var writer = new StreamWriter(stream, options.Encoding);

                var writeCsvConfig = new CsvConfiguration(options.Culture);

                if (fileConfig != null)
                    fileConfig.Value.WriterConfigurationAction?.Invoke(writeCsvConfig);
                else
                    options.FileConfiguration.WriterConfigurationAction?.Invoke(writeCsvConfig);

                await using var csv = new CsvWriter(writer, writeCsvConfig);

                if (fileConfig != null)
                    fileConfig.Value.CsvContextAction?.Invoke(csv.Context);
                else
                    options.FileConfiguration.CsvContextAction?.Invoke(csv.Context);

                var array = records as T[] ?? records.ToArray();
                await csv.WriteRecordsAsync(array, cancellationToken);
                await writer.FlushAsync(cancellationToken);

                return array.Length;
            },
            options);
}
