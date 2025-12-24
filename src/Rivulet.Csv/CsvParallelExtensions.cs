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
    /// <summary>
    ///     Parses multiple CSV files in parallel and returns their contents as strongly-typed records.
    /// </summary>
    /// <typeparam name="T">The type of record to parse from CSV files.</typeparam>
    /// <param name="filePaths">The collection of CSV file paths to parse.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list where each element contains all records parsed from a CSV file.</returns>
    /// <exception cref="ArgumentNullException">Thrown when filePaths is null.</exception>
    public static Task<IReadOnlyList<IReadOnlyList<T>>> ParseCsvParallelAsync<T>(
        this IEnumerable<string> filePaths,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        return filePaths.ParseCsvParallelAsync<T>(
            new CsvConfiguration(options?.Culture ?? CultureInfo.InvariantCulture),
            null,
            options,
            cancellationToken
        );
    }

    /// <summary>
    ///     Parses multiple CSV files in parallel using a single ClassMap for all files.
    /// </summary>
    /// <typeparam name="T">The type of record to parse from CSV files.</typeparam>
    /// <typeparam name="TMap">The ClassMap type to use for all files.</typeparam>
    /// <param name="filePaths">The collection of CSV file paths to parse.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list where each element contains all records parsed from a CSV file.</returns>
    /// <exception cref="ArgumentNullException">Thrown when filePaths is null.</exception>
    public static Task<IReadOnlyList<IReadOnlyList<T>>> ParseCsvParallelAsync<T, TMap>(
        this IEnumerable<string> filePaths,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default)
        where TMap : ClassMap
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        var builder = new CsvConfigurationBuilder(options?.Culture).WithClassMap<TMap>();

        return filePaths.ParseCsvParallelAsync<T>(builder.Build(), builder, options, cancellationToken);
    }

    /// <summary>
    ///     Parses multiple CSV files in parallel with custom configuration.
    /// </summary>
    /// <typeparam name="T">The type of record to parse from CSV files.</typeparam>
    /// <param name="filePaths">The collection of CSV file paths to parse.</param>
    /// <param name="configureBuilder">Action to configure CSV settings and ClassMaps.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list where each element contains all records parsed from a CSV file.</returns>
    /// <exception cref="ArgumentNullException">Thrown when filePaths or configureBuilder is null.</exception>
    public static Task<IReadOnlyList<IReadOnlyList<T>>> ParseCsvParallelAsync<T>(
        this IEnumerable<string> filePaths,
        Action<CsvConfigurationBuilder> configureBuilder,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        ArgumentNullException.ThrowIfNull(configureBuilder);

        var builder = new CsvConfigurationBuilder(options?.Culture);
        configureBuilder(builder);

        return filePaths.ParseCsvParallelAsync<T>(
            builder.Build(),
            builder,
            options,
            cancellationToken
        );
    }

    /// <summary>
    ///     Parses multiple CSV files in parallel with per-file configuration.
    /// </summary>
    /// <typeparam name="T">The type of record to parse from CSV files.</typeparam>
    /// <param name="filePaths">The collection of CSV file paths to parse.</param>
    /// <param name="configureBuilder">Function that returns configuration based on file path.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list where each element contains all records parsed from a CSV file.</returns>
    /// <exception cref="ArgumentNullException">Thrown when filePaths or configureBuilder is null.</exception>
    public static async Task<IReadOnlyList<IReadOnlyList<T>>> ParseCsvParallelAsync<T>(
        this IEnumerable<string> filePaths,
        Func<string, Action<CsvConfigurationBuilder>> configureBuilder,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        ArgumentNullException.ThrowIfNull(configureBuilder);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        return await filePaths.SelectParallelAsync(
                (filePath, ct) =>
                {
                    var builder = new CsvConfigurationBuilder(options.Culture);
                    configureBuilder(filePath)(builder);

                    return ParseCsvFileAsync<T>(
                        filePath,
                        builder.Build(),
                        builder,
                        options,
                        ct
                    );
                },
                parallelOptions,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Parses multiple CSV files in parallel with explicit per-file configuration.
    /// </summary>
    /// <typeparam name="T">The type of record to parse from CSV files.</typeparam>
    /// <param name="fileConfigs">Collection of file configurations.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list where each element contains all records parsed from a CSV file.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fileConfigs is null.</exception>
    public static async Task<IReadOnlyList<IReadOnlyList<T>>> ParseCsvParallelAsync<T>(
        this IEnumerable<CsvFileConfig<T>> fileConfigs,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileConfigs);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        return await fileConfigs.SelectParallelAsync(
                (config, ct) =>
                    ParseCsvFileAsync<T>(
                        config.FilePath,
                        config.Builder?.Build() ?? new CsvConfiguration(options.Culture),
                        config.Builder,
                        options,
                        ct
                    ),
                parallelOptions,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Writes collections of records to multiple CSV files in parallel.
    /// </summary>
    /// <typeparam name="T">The type of record to write to CSV files.</typeparam>
    /// <param name="fileWrites">Collection of tuples containing file path and records to write.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of file paths that were written successfully.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fileWrites is null.</exception>
    public static Task<IReadOnlyList<string>> WriteCsvParallelAsync<T>(
        this IEnumerable<(string filePath, IEnumerable<T> records)> fileWrites,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileWrites);

        return fileWrites.WriteCsvParallelAsync(
            new CsvConfiguration(options?.Culture ?? CultureInfo.InvariantCulture),
            null,
            options,
            cancellationToken
        );
    }

    /// <summary>
    ///     Writes collections of records to multiple CSV files in parallel using a single ClassMap.
    /// </summary>
    /// <typeparam name="T">The type of record to write to CSV files.</typeparam>
    /// <typeparam name="TMap">The ClassMap type to use for all files.</typeparam>
    /// <param name="fileWrites">Collection of tuples containing file path and records to write.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of file paths that were written successfully.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fileWrites is null.</exception>
    public static Task<IReadOnlyList<string>> WriteCsvParallelAsync<T, TMap>(
        this IEnumerable<(string filePath, IEnumerable<T> records)> fileWrites,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default)
        where TMap : ClassMap
    {
        ArgumentNullException.ThrowIfNull(fileWrites);

        var builder = new CsvConfigurationBuilder(options?.Culture).WithClassMap<TMap>();

        return fileWrites.WriteCsvParallelAsync(builder.Build(), builder, options, cancellationToken);
    }

    /// <summary>
    ///     Writes collections of records to multiple CSV files in parallel with custom configuration.
    /// </summary>
    /// <typeparam name="T">The type of record to write to CSV files.</typeparam>
    /// <param name="fileWrites">Collection of tuples containing file path and records to write.</param>
    /// <param name="configureBuilder">Action to configure CSV settings and ClassMaps.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of file paths that were written successfully.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fileWrites or configureBuilder is null.</exception>
    public static Task<IReadOnlyList<string>> WriteCsvParallelAsync<T>(
        this IEnumerable<(string filePath, IEnumerable<T> records)> fileWrites,
        Action<CsvConfigurationBuilder> configureBuilder,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileWrites);
        ArgumentNullException.ThrowIfNull(configureBuilder);

        var builder = new CsvConfigurationBuilder(options?.Culture);
        configureBuilder(builder);

        return fileWrites.WriteCsvParallelAsync(
            builder.Build(),
            builder,
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
    public static async Task<IReadOnlyList<string>> WriteCsvParallelAsync<T>(
        this IEnumerable<(string FilePath, IEnumerable<T> Records, Action<CsvConfigurationBuilder> ConfigureBuilder)> fileWrites,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileWrites);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        return await fileWrites.SelectParallelAsync(
                async (write, ct) =>
                {
                    var builder = new CsvConfigurationBuilder(options.Culture);
                    write.ConfigureBuilder(builder);

                    await WriteCsvFileAsync(
                        write.FilePath,
                        write.Records,
                        builder.Build(),
                        builder,
                        options,
                        ct
                    );

                    return write.FilePath;
                },
                parallelOptions,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Writes collections of records to multiple CSV files in parallel with explicit per-file configuration.
    /// </summary>
    /// <typeparam name="T">The type of record to write to CSV files.</typeparam>
    /// <param name="fileConfigs">Collection of file configurations.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of file paths that were written successfully.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fileConfigs is null.</exception>
    public static async Task WriteCsvParallelAsync<T>(this IEnumerable<CsvFileConfig<T>> fileConfigs,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileConfigs);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        await fileConfigs.SelectParallelAsync(
                async (config, ct) =>
                {
                    if (config.Records == null)
                        throw new InvalidOperationException($"CsvFileConfig for '{config.FilePath}' does not contain records. Use CsvFileConfig.ForWrite() to create write configurations.");

                    await WriteCsvFileAsync(
                        config.FilePath,
                        config.Records,
                        config.Builder?.Build() ?? new CsvConfiguration(options.Culture),
                        config.Builder,
                        options,
                        ct
                    );

                    return config.FilePath;
                },
                parallelOptions,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Transforms CSV files in parallel by parsing, applying a transformation, and writing to new files.
    /// </summary>
    /// <typeparam name="TIn">The type of record to parse from source CSV files.</typeparam>
    /// <typeparam name="TOut">The type of record to write to destination CSV files.</typeparam>
    /// <param name="files">Collection of source and destination file path pairs.</param>
    /// <param name="transformFunc">Function that transforms records.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of destination file paths that were written successfully.</returns>
    /// <exception cref="ArgumentNullException">Thrown when files or transformFunc is null.</exception>
    public static Task<IReadOnlyList<string>> TransformCsvParallelAsync<TIn, TOut>(
        this IEnumerable<(string sourcePath, string destinationPath)> files,
        Func<string, IReadOnlyList<TIn>, ValueTask<IEnumerable<TOut>>> transformFunc,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(transformFunc);

        var config = new CsvConfiguration(options?.Culture ?? CultureInfo.InvariantCulture);

        return files.TransformCsvParallelAsync(
            transformFunc,
            config,
            null,
            config,
            null,
            options,
            cancellationToken
        );
    }

    /// <summary>
    ///     Transforms CSV files in parallel using ClassMaps for input and output.
    /// </summary>
    /// <typeparam name="TIn">The type of record to parse from source CSV files.</typeparam>
    /// <typeparam name="TOut">The type of record to write to destination CSV files.</typeparam>
    /// <typeparam name="TInMap">The ClassMap type for parsing input CSV files.</typeparam>
    /// <typeparam name="TOutMap">The ClassMap type for writing output CSV files.</typeparam>
    /// <param name="files">Collection of source and destination file path pairs.</param>
    /// <param name="transformFunc">Function that transforms records.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of destination file paths that were written successfully.</returns>
    /// <exception cref="ArgumentNullException">Thrown when files or transformFunc is null.</exception>
    public static Task<IReadOnlyList<string>> TransformCsvParallelAsync<TIn, TOut, TInMap, TOutMap>(
        this IEnumerable<(string sourcePath, string destinationPath)> files,
        Func<string, IReadOnlyList<TIn>, ValueTask<IEnumerable<TOut>>> transformFunc,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default)
        where TInMap : ClassMap
        where TOutMap : ClassMap
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(transformFunc);

        var inBuilder = new CsvConfigurationBuilder(options?.Culture).WithClassMap<TInMap>();
        var outBuilder = new CsvConfigurationBuilder(options?.Culture).WithClassMap<TOutMap>();

        return files.TransformCsvParallelAsync(
            transformFunc,
            inBuilder.Build(),
            inBuilder,
            outBuilder.Build(),
            outBuilder,
            options,
            cancellationToken
        );
    }

    /// <summary>
    ///     Transforms CSV files in parallel with custom input and output configuration.
    /// </summary>
    /// <typeparam name="TIn">The type of record to parse from source CSV files.</typeparam>
    /// <typeparam name="TOut">The type of record to write to destination CSV files.</typeparam>
    /// <param name="files">Collection of source and destination file path pairs.</param>
    /// <param name="transformFunc">Function that transforms records.</param>
    /// <param name="configureInputBuilder">Action to configure input CSV parsing.</param>
    /// <param name="configureOutputBuilder">Action to configure output CSV writing.</param>
    /// <param name="options">CSV operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of destination file paths that were written successfully.</returns>
    /// <exception cref="ArgumentNullException">Thrown when files, transformFunc, or configuration actions are null.</exception>
    public static Task<IReadOnlyList<string>> TransformCsvParallelAsync<TIn, TOut>(
        this IEnumerable<(string sourcePath, string destinationPath)> files,
        Func<string, IReadOnlyList<TIn>, ValueTask<IEnumerable<TOut>>> transformFunc,
        Action<CsvConfigurationBuilder> configureInputBuilder,
        Action<CsvConfigurationBuilder> configureOutputBuilder,
        CsvOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(transformFunc);
        ArgumentNullException.ThrowIfNull(configureInputBuilder);
        ArgumentNullException.ThrowIfNull(configureOutputBuilder);

        var inBuilder = new CsvConfigurationBuilder(options?.Culture);
        configureInputBuilder(inBuilder);
        var outBuilder = new CsvConfigurationBuilder(options?.Culture);
        configureOutputBuilder(outBuilder);
        return files.TransformCsvParallelAsync(transformFunc, inBuilder.Build(), inBuilder, outBuilder.Build(), outBuilder, options, cancellationToken);
    }

    private static async Task<IReadOnlyList<IReadOnlyList<T>>> ParseCsvParallelAsync<T>(
        this IEnumerable<string> filePaths,
        IReaderConfiguration csvConfiguration,
        CsvConfigurationBuilder? builder,
        CsvOperationOptions? options,
        CancellationToken cancellationToken)
    {
        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        return await filePaths.SelectParallelAsync(
                (filePath, ct) => ParseCsvFileAsync<T>(filePath, csvConfiguration, builder, options, ct),
                parallelOptions,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<string>> WriteCsvParallelAsync<T>(
        this IEnumerable<(string filePath, IEnumerable<T> records)> fileWrites,
        IWriterConfiguration csvConfiguration,
        CsvConfigurationBuilder? builder,
        CsvOperationOptions? options,
        CancellationToken cancellationToken)
    {
        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        return await fileWrites.SelectParallelAsync(
                async (write, ct) =>
                {
                    await WriteCsvFileAsync(write.filePath, write.records, csvConfiguration, builder, options, ct);
                    return write.filePath;
                },
                parallelOptions,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<string>> TransformCsvParallelAsync<TIn, TOut>(
        this IEnumerable<(string sourcePath, string destinationPath)> files,
        Func<string, IReadOnlyList<TIn>, ValueTask<IEnumerable<TOut>>> transformFunc,
        IReaderConfiguration inputConfig,
        CsvConfigurationBuilder? inputBuilder,
        IWriterConfiguration outputConfig,
        CsvConfigurationBuilder? outputBuilder,
        CsvOperationOptions? options,
        CancellationToken cancellationToken)
    {
        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        return await files.SelectParallelAsync(
                async (file, ct) =>
                {
                    var records = await ParseCsvFileAsync<TIn>(file.sourcePath, inputConfig, inputBuilder, options, ct);
                    var transformed = await transformFunc(file.sourcePath, records);
                    await WriteCsvFileAsync(file.destinationPath, transformed, outputConfig, outputBuilder, options, ct);
                    return file.destinationPath;
                },
                parallelOptions,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static ValueTask<IReadOnlyList<T>> ParseCsvFileAsync<T>(
        string filePath,
        IReaderConfiguration csvConfiguration,
        CsvConfigurationBuilder? builder,
        CsvOperationOptions options,
        CancellationToken cancellationToken) =>
        CsvOperationHelper.ExecuteCsvOperationAsync(
            filePath,
            async () =>
            {
                await using var stream = CsvOperationHelper.CreateReadStream(filePath, options);
                using var reader = new StreamReader(stream, options.Encoding);
                using var csv = new CsvReader(reader, csvConfiguration);

                builder?.ApplyClassMaps(csv.Context);

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
        IWriterConfiguration csvConfiguration,
        CsvConfigurationBuilder? builder,
        CsvOperationOptions options,
        CancellationToken cancellationToken) =>
        CsvOperationHelper.ExecuteCsvOperationAsync(
            filePath,
            async () =>
            {
                CsvOperationHelper.EnsureDirectoryExists(filePath, options);
                CsvOperationHelper.ValidateOverwrite(filePath, options);

                await using var stream = CsvOperationHelper.CreateWriteStream(filePath, options);
                await using var writer = new StreamWriter(stream, options.Encoding);
                await using var csv = new CsvWriter(writer, csvConfiguration);

                builder?.ApplyClassMaps(csv.Context);

                var array = records as T[] ?? records.ToArray();
                await csv.WriteRecordsAsync(array, cancellationToken);
                await writer.FlushAsync(cancellationToken);

                return array.Length;
            },
            options);
}
