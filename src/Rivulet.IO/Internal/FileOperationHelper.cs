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
    /// <typeparam name="TResult">The result type returned by the operation.</typeparam>
    /// <typeparam name="TOptions">The options type derived from BaseFileOperationOptions.</typeparam>
    /// <param name="filePath">The path to the file being processed.</param>
    /// <param name="operation">The async operation to execute.</param>
    /// <param name="options">Configuration options including lifecycle callbacks.</param>
    /// <param name="getOperationResult">Optional function to extract FileOperationResult metrics from the operation result. If null and OnFileCompleteAsync is set, BytesProcessed will be 0.</param>
    /// <returns>The result of the operation.</returns>
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
    /// <typeparam name="TOptions">The options type derived from BaseFileOperationOptions.</typeparam>
    /// <param name="filePath">The file path whose parent directory should be validated/created.</param>
    /// <param name="options">Configuration options containing CreateDirectoriesIfNotExist setting.</param>
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
    /// <typeparam name="TOptions">The options type derived from BaseFileOperationOptions.</typeparam>
    /// <param name="filePath">The file path to check for existence.</param>
    /// <param name="options">Configuration options containing OverwriteExisting setting.</param>
    /// <exception cref="IOException">Thrown when the file exists and OverwriteExisting is false.</exception>
    public static void ValidateOverwrite<TOptions>(string filePath, TOptions options)
        where TOptions : BaseFileOperationOptions
    {
        if (!options.OverwriteExisting && File.Exists(filePath))
            throw new IOException($"File already exists: {filePath}");
    }

    /// <summary>
    ///     Creates a FileStream for reading with standard options.
    /// </summary>
    /// <param name="filePath">The path to the file to read.</param>
    /// <param name="options">Configuration options containing buffer size and file share settings.</param>
    /// <returns>A FileStream configured for async sequential reading.</returns>
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
    /// <param name="filePath">The path to the file to write.</param>
    /// <param name="options">Configuration options containing buffer size and file share settings.</param>
    /// <returns>A FileStream configured for async sequential writing.</returns>
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
    /// <param name="sourcePath">The path to the source file to read.</param>
    /// <param name="destinationPath">The path to the destination file to write.</param>
    /// <param name="options">Configuration options for both streams.</param>
    /// <returns>A tuple containing the source read stream and destination write stream.</returns>
    public static (FileStream sourceStream, FileStream destStream) CreateCopyStreams(
        string sourcePath,
        string destinationPath,
        FileOperationOptions options
    ) => (CreateReadStream(sourcePath, options), CreateWriteStream(destinationPath, options));

    /// <summary>
    ///     Computes destination path from source path and destination directory, preserving relative structure.
    /// </summary>
    /// <param name="sourcePath">The full path to the source file.</param>
    /// <param name="sourceBaseDirectory">The base directory of the source file (used to compute relative path).</param>
    /// <param name="destinationDirectory">The target directory where the file should be copied.</param>
    /// <returns>The computed destination file path preserving the relative directory structure.</returns>
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
