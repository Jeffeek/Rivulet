using System.Diagnostics.CodeAnalysis;

namespace Rivulet.Csv;

/// <summary>
///     Represents a CSV file with its associated configuration for per-file customization.
///     Allows different files to use different ClassMaps and CsvHelper settings in a single parallel operation.
/// </summary>
/// <typeparam name="T">The type of record to parse from or write to the CSV file.</typeparam>
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
public sealed class CsvFileConfig<T>
{
    /// <summary>
    ///     Gets the file path of the CSV file.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    ///     Gets the optional configuration builder for this specific file.
    /// </summary>
    public CsvConfigurationBuilder? Builder { get; }

    /// <summary>
    ///     Gets the records to write (for write operations only).
    /// </summary>
    internal IEnumerable<T>? Records { get; init; }

    /// <summary>
    ///     Initializes a new instance of the <see cref="CsvFileConfig{T}"/> class for reading operations.
    /// </summary>
    /// <param name="filePath">The path to the CSV file.</param>
    /// <param name="configureBuilder">Optional action to configure CsvHelper settings for this file.</param>
    /// <exception cref="ArgumentException">Thrown when filePath is null or whitespace.</exception>
    public CsvFileConfig(string filePath, Action<CsvConfigurationBuilder>? configureBuilder = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        FilePath = filePath;

        if (configureBuilder != null)
        {
            Builder = new CsvConfigurationBuilder();
            configureBuilder(Builder);
        }
    }

    /// <summary>
    ///     Creates a configuration for writing records to a CSV file.
    /// </summary>
    /// <param name="filePath">The path to the CSV file.</param>
    /// <param name="records">The records to write.</param>
    /// <param name="configureBuilder">Optional action to configure CsvHelper settings for this file.</param>
    /// <returns>A new <see cref="CsvFileConfig{T}"/> instance configured for writing.</returns>
    /// <exception cref="ArgumentException">Thrown when filePath is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when records is null.</exception>
    public static CsvFileConfig<T> ForWrite(
        string filePath,
        IEnumerable<T> records,
        Action<CsvConfigurationBuilder>? configureBuilder = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(records);

        return new CsvFileConfig<T>(filePath, configureBuilder)
        {
            Records = records
        };
    }
}
