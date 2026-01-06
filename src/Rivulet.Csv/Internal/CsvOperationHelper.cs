using Rivulet.IO.Base;
using Rivulet.IO.Internal;

namespace Rivulet.Csv.Internal;

/// <summary>
///     Internal helper for CSV file operations with lifecycle callbacks.
///     Delegates common operations to FileOperationHelper for DRY compliance.
/// </summary>
internal static class CsvOperationHelper
{
    /// <summary>
    ///     Executes a CSV file operation with lifecycle callbacks (OnFileStartAsync, OnFileCompleteAsync, OnFileErrorAsync).
    /// </summary>
    /// <param name="filePath">The path to the CSV file being processed.</param>
    /// <param name="operation">The async operation to execute that returns file operation metrics.</param>
    /// <param name="options">Configuration options including lifecycle callbacks.</param>
    internal static async ValueTask ExecuteCsvOperationAsync(
        string filePath,
        Func<ValueTask<FileOperationResult>> operation,
        CsvOperationOptions options
    ) =>
        await FileOperationHelper.ExecuteFileOperationAsync(
            filePath,
            operation,
            options,
            static result => result).ConfigureAwait(false);

    /// <summary>
    ///     Ensures the directory for the given file path exists if CreateDirectoriesIfNotExist is true.
    /// </summary>
    /// <param name="filePath">The file path whose directory should be validated/created.</param>
    /// <param name="options">Configuration options containing CreateDirectoriesIfNotExist setting.</param>
    internal static void EnsureDirectoryExists(string filePath, CsvOperationOptions options) =>
        FileOperationHelper.EnsureDirectoryExists(filePath, options);

    /// <summary>
    ///     Validates whether a file can be overwritten based on options.
    /// </summary>
    /// <param name="filePath">The file path to check for existence.</param>
    /// <param name="options">Configuration options containing OverwriteExisting setting.</param>
    /// <exception cref="IOException">Thrown when the file exists and OverwriteExisting is false.</exception>
    internal static void ValidateOverwrite(string filePath, CsvOperationOptions options) =>
        FileOperationHelper.ValidateOverwrite(filePath, options);

    /// <summary>
    ///     Creates a read stream for CSV file operations.
    /// </summary>
    /// <param name="filePath">The path to the CSV file to read.</param>
    /// <param name="options">Configuration options containing buffer size.</param>
    /// <returns>A FileStream configured for async sequential reading with shared read access.</returns>
    internal static FileStream CreateReadStream(string filePath, CsvOperationOptions options) =>
        new(filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            options.BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

    /// <summary>
    ///     Creates a write stream for CSV file operations.
    /// </summary>
    /// <param name="filePath">The path to the CSV file to write.</param>
    /// <param name="options">Configuration options containing buffer size.</param>
    /// <returns>A FileStream configured for async sequential writing with exclusive access.</returns>
    internal static FileStream CreateWriteStream(string filePath, CsvOperationOptions options) =>
        new(filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            options.BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
}
