namespace Rivulet.Csv.Internal;

/// <summary>
///     Internal helper for CSV file operations with lifecycle callbacks.
/// </summary>
internal static class CsvOperationHelper
{
    /// <summary>
    ///     Executes a CSV file operation with lifecycle callbacks (OnFileStartAsync, OnFileCompleteAsync, OnFileErrorAsync).
    /// </summary>
    internal static async ValueTask<TResult> ExecuteCsvOperationAsync<TResult>(
        string filePath,
        Func<ValueTask<TResult>> operation,
        CsvOperationOptions options,
        Func<TResult, long> getRecordCount)
    {
        try
        {
            if (options.OnFileStartAsync != null)
                await options.OnFileStartAsync(filePath).ConfigureAwait(false);

            var result = await operation().ConfigureAwait(false);

            if (options.OnFileCompleteAsync == null)
                return result;

            var recordCount = getRecordCount(result);
            await options.OnFileCompleteAsync(filePath, recordCount).ConfigureAwait(false);

            return result;
        }
        catch (Exception ex)
        {
            if (options.OnFileErrorAsync != null)
                await options.OnFileErrorAsync(filePath, ex).ConfigureAwait(false);

            throw;
        }
    }

    /// <summary>
    ///     Executes a CSV file operation without a return value.
    /// </summary>
    internal static async ValueTask ExecuteCsvOperationAsync(
        string filePath,
        Func<ValueTask<long>> operation,
        CsvOperationOptions options)
    {
        try
        {
            if (options.OnFileStartAsync != null)
                await options.OnFileStartAsync(filePath).ConfigureAwait(false);

            var recordCount = await operation().ConfigureAwait(false);

            if (options.OnFileCompleteAsync != null)
                await options.OnFileCompleteAsync(filePath, recordCount).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (options.OnFileErrorAsync != null)
                await options.OnFileErrorAsync(filePath, ex).ConfigureAwait(false);

            throw;
        }
    }

    /// <summary>
    ///     Ensures the directory for the given file path exists if CreateDirectoriesIfNotExist is true.
    /// </summary>
    internal static void EnsureDirectoryExists(string filePath, CsvOperationOptions options)
    {
        if (!options.CreateDirectoriesIfNotExist)
            return;

        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(directory))
            return;

        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);
    }

    /// <summary>
    ///     Validates whether a file can be overwritten based on options.
    /// </summary>
    internal static void ValidateOverwrite(string filePath, CsvOperationOptions options)
    {
        if (!File.Exists(filePath))
            return;

        if (!options.OverwriteExisting)
            throw new IOException($"File already exists and OverwriteExisting is false: {filePath}");
    }

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
