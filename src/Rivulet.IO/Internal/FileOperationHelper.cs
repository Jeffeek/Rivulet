using Rivulet.IO.Base;

namespace Rivulet.IO.Internal;

/// <summary>
///     Internal helper to reduce code duplication in file operations.
/// </summary>
internal static class FileOperationHelper
{
    /// <summary>
    ///     Executes a file operation with standard lifecycle callbacks and error handling.
    /// </summary>
    internal static async ValueTask<TResult> ExecuteFileOperationAsync<TResult, TOptions>(
        string filePath,
        Func<ValueTask<TResult>> operation,
        TOptions options,
        Func<TResult, FileOperationResult>? getOperationResult = null
    )
        where TOptions : BaseFileOperationOptions
    {
        try
        {
            if (options.OnFileStartAsync != null)
                await options.OnFileStartAsync(filePath).ConfigureAwait(false);

            var result = await operation().ConfigureAwait(false);

            if (options.OnFileCompleteAsync == null)
                return result;

            var operationResult = getOperationResult?.Invoke(result) ?? new FileOperationResult { BytesProcessed = 0 };
            await options.OnFileCompleteAsync(filePath, operationResult).ConfigureAwait(false);

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
    ///     Ensures directory exists if CreateDirectoriesIfNotExist is enabled.
    /// </summary>
    public static void EnsureDirectoryExists<TOptions>(string filePath, TOptions options)
        where TOptions : BaseFileOperationOptions
    {
        if (!options.CreateDirectoriesIfNotExist) return;

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
    }

    /// <summary>
    ///     Validates that file doesn't exist if OverwriteExisting is false.
    /// </summary>
    public static void ValidateOverwrite<TOptions>(string filePath, TOptions options)
        where TOptions : BaseFileOperationOptions
    {
        if (!options.OverwriteExisting && File.Exists(filePath))
            throw new IOException($"File already exists: {filePath}");
    }

    /// <summary>
    ///     Creates a FileStream for reading with standard options.
    /// </summary>
    public static FileStream CreateReadStream(string filePath, FileOperationOptions options) =>
        new(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            options.ReadFileShare,
            options.BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

    /// <summary>
    ///     Creates a FileStream for writing with standard options.
    /// </summary>
    public static FileStream CreateWriteStream(string filePath, FileOperationOptions options) =>
        new(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            options.WriteFileShare,
            options.BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

    /// <summary>
    ///     Creates dual FileStreams for copy operations (source read + destination write).
    /// </summary>
    public static (FileStream sourceStream, FileStream destStream) CreateCopyStreams(
        string sourcePath,
        string destinationPath,
        FileOperationOptions options
    ) => (CreateReadStream(sourcePath, options), CreateWriteStream(destinationPath, options));

    /// <summary>
    ///     Computes destination path from source path and destination directory, preserving relative structure.
    /// </summary>
    public static string ComputeDestinationPath(
        string sourcePath,
        string sourceBaseDirectory,
        string destinationDirectory
    )
    {
        var relativePath = Path.GetRelativePath(sourceBaseDirectory, sourcePath);
        return Path.Join(destinationDirectory, relativePath);
    }
}
