namespace Rivulet.IO.Internal;

/// <summary>
///     Internal helper to reduce code duplication in file operations.
/// </summary>
internal static class FileOperationHelper
{
    /// <summary>
    ///     Executes a file operation with standard lifecycle callbacks and error handling.
    /// </summary>
    public static async ValueTask<TResult> ExecuteFileOperationAsync<TResult>(
        string filePath,
        Func<ValueTask<TResult>> operation,
        FileOperationOptions options,
        Func<TResult, long>? getBytesProcessed = null)
    {
        if (options.OnFileStartAsync != null) await options.OnFileStartAsync(filePath).ConfigureAwait(false);

        try
        {
            var result = await operation().ConfigureAwait(false);

            if (options.OnFileCompleteAsync == null) return result;

            var bytesProcessed = getBytesProcessed?.Invoke(result) ?? 0;
            await options.OnFileCompleteAsync(filePath, bytesProcessed).ConfigureAwait(false);

            return result;
        }
        catch (Exception ex)
        {
            if (options.OnFileErrorAsync != null) await options.OnFileErrorAsync(filePath, ex).ConfigureAwait(false);

            throw;
        }
    }

    /// <summary>
    ///     Ensures directory exists if CreateDirectoriesIfNotExist is enabled.
    /// </summary>
    public static void EnsureDirectoryExists(string filePath, FileOperationOptions options)
    {
        if (!options.CreateDirectoriesIfNotExist) return;

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
    }

    /// <summary>
    ///     Validates that file doesn't exist if OverwriteExisting is false.
    /// </summary>
    public static void ValidateOverwrite(string filePath, FileOperationOptions options)
    {
        if (!options.OverwriteExisting && File.Exists(filePath)) throw new IOException($"File already exists: {filePath}");
    }
}