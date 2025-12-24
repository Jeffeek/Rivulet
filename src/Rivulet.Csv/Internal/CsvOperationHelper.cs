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
    internal static ValueTask<TResult> ExecuteCsvOperationAsync<TResult>(
        string filePath,
        Func<ValueTask<TResult>> operation,
        CsvOperationOptions options,
        Func<TResult, long> getRecordCount) =>
        FileOperationHelper.ExecuteFileOperationAsync(filePath, operation, options, getRecordCount);

    /// <summary>
    ///     Executes a CSV file operation without a return value.
    /// </summary>
    internal static async ValueTask ExecuteCsvOperationAsync(
        string filePath,
        Func<ValueTask<long>> operation,
        CsvOperationOptions options)
    {
        await FileOperationHelper.ExecuteFileOperationAsync(
            filePath,
            operation,
            options,
            static count => count).ConfigureAwait(false);
    }

    /// <summary>
    ///     Ensures the directory for the given file path exists if CreateDirectoriesIfNotExist is true.
    /// </summary>
    internal static void EnsureDirectoryExists(string filePath, CsvOperationOptions options) =>
        FileOperationHelper.EnsureDirectoryExists(filePath, options);

    /// <summary>
    ///     Validates whether a file can be overwritten based on options.
    /// </summary>
    internal static void ValidateOverwrite(string filePath, CsvOperationOptions options) =>
        FileOperationHelper.ValidateOverwrite(filePath, options);

    /// <summary>
    ///     Creates a read stream for CSV file operations.
    /// </summary>
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
    internal static FileStream CreateWriteStream(string filePath, CsvOperationOptions options) =>
        new(filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            options.BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
}
