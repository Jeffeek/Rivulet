using System.Diagnostics.CodeAnalysis;
using Rivulet.Core;
using Rivulet.IO.Base;
using Rivulet.IO.Internal;

namespace Rivulet.IO;

/// <summary>
///     Extension methods for parallel file operations with bounded concurrency and resilience.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
public static class FileParallelExtensions
{
    /// <summary>
    ///     Reads multiple files in parallel and returns their contents as strings.
    /// </summary>
    /// <param name="filePaths">The collection of file paths to read.</param>
    /// <param name="options">File operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of file contents as strings, in the same order as the input paths.</returns>
    /// <exception cref="ArgumentNullException">Thrown when filePaths is null.</exception>
    /// <exception cref="FileNotFoundException">Thrown when a file is not found.</exception>
    /// <exception cref="IOException">Thrown when file operations fail.</exception>
    public static async Task<IReadOnlyList<string>> ReadAllTextParallelAsync(
        this IEnumerable<string> filePaths,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

#pragma warning disable CA2007 // ConfigureAwait not applicable with 'await using' statements in ReadFileTextAsync
        return await filePaths.SelectParallelAsync((filePath, ct) => FileOperationHelper.ReadFileTextAsync(filePath, options, ct),
                parallelOptions,
                cancellationToken)
            .ConfigureAwait(false);
#pragma warning restore CA2007
    }

    /// <summary>
    ///     Reads multiple files in parallel and returns their contents as byte arrays.
    /// </summary>
    /// <param name="filePaths">The collection of file paths to read.</param>
    /// <param name="options">File operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of file contents as byte arrays.</returns>
    /// <exception cref="ArgumentNullException">Thrown when filePaths is null.</exception>
    public static async Task<IReadOnlyList<byte[]>> ReadAllBytesParallelAsync(
        this IEnumerable<string> filePaths,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

#pragma warning disable CA2007 // ConfigureAwait not applicable with 'await using' statements in ReadFileBytesAsync
        return await filePaths.SelectParallelAsync((filePath, ct) => FileOperationHelper.ReadFileBytesAsync(filePath, options, ct),
                parallelOptions,
                cancellationToken)
            .ConfigureAwait(false);
#pragma warning restore CA2007
    }

    /// <summary>
    ///     Reads multiple files in parallel as lines.
    /// </summary>
    /// <param name="filePaths">The collection of file paths to read.</param>
    /// <param name="options">File operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list where each element contains all lines from a file.</returns>
    /// <exception cref="ArgumentNullException">Thrown when filePaths is null.</exception>
    public static async Task<IReadOnlyList<string[]>> ReadAllLinesParallelAsync(
        this IEnumerable<string> filePaths,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

#pragma warning disable CA2007 // ConfigureAwait not applicable with 'await using' statements in ReadFileLinesAsync
        return await filePaths.SelectParallelAsync((filePath, ct) => ReadFileLinesAsync(filePath, options, ct),
                parallelOptions,
                cancellationToken)
            .ConfigureAwait(false);
#pragma warning restore CA2007
    }

    /// <summary>
    ///     Writes content to multiple files in parallel.
    /// </summary>
    /// <param name="fileWrites">Collection of tuples containing file path and content to write.</param>
    /// <param name="options">File operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of file paths that were written successfully.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fileWrites is null.</exception>
    /// <exception cref="IOException">Thrown when file write operations fail.</exception>
    public static async Task<IReadOnlyList<string>> WriteAllTextParallelAsync(
        this IEnumerable<(string filePath, string content)> fileWrites,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(fileWrites);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

#pragma warning disable CA2007 // ConfigureAwait not applicable with 'await using' statements in WriteFileTextAsync
        return await fileWrites.SelectParallelAsync(
                async (write, ct) =>
                {
                    await FileOperationHelper.WriteFileTextAsync(write.filePath, write.content, options, ct);
                    return write.filePath;
                },
                parallelOptions,
                cancellationToken)
            .ConfigureAwait(false);
#pragma warning restore CA2007
    }

    /// <summary>
    ///     Writes byte data to multiple files in parallel.
    /// </summary>
    /// <param name="fileWrites">Collection of tuples containing file path and byte content to write.</param>
    /// <param name="options">File operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of file paths that were written successfully.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fileWrites is null.</exception>
    public static async Task<IReadOnlyList<string>> WriteAllBytesParallelAsync(
        this IEnumerable<(string filePath, byte[] content)> fileWrites,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(fileWrites);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

#pragma warning disable CA2007 // ConfigureAwait not applicable with 'await using' statements in WriteFileBytesAsync
        return await fileWrites.SelectParallelAsync(
                async (write, ct) =>
                {
                    await FileOperationHelper.WriteFileBytesAsync(write.filePath, write.content, options, ct);
                    return write.filePath;
                },
                parallelOptions,
                cancellationToken)
            .ConfigureAwait(false);
#pragma warning restore CA2007
    }

    /// <summary>
    ///     Transforms files in parallel by applying a transformation function to their contents.
    /// </summary>
    /// <param name="files">Collection of source and destination file path pairs.</param>
    /// <param name="transformFunc">Function that transforms the content (input file path, content) => transformed content.</param>
    /// <param name="options">File operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of destination file paths that were written successfully.</returns>
    /// <exception cref="ArgumentNullException">Thrown when files or transformFunc is null.</exception>
    public static async Task<IReadOnlyList<string>> TransformFilesParallelAsync(
        this IEnumerable<(string sourcePath, string destinationPath)> files,
        Func<string, string, ValueTask<string>> transformFunc,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(transformFunc);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

#pragma warning disable CA2007 // ConfigureAwait not applicable with 'await using' statements in ReadFileTextAsync and WriteFileTextAsync
        return await files.SelectParallelAsync(
                async (file, ct) =>
                {
                    var content = await FileOperationHelper.ReadFileTextAsync(file.sourcePath, options, ct);
                    var transformed = await transformFunc(file.sourcePath, content);
                    await FileOperationHelper.WriteFileTextAsync(file.destinationPath, transformed, options, ct);
                    return file.destinationPath;
                },
                parallelOptions,
                cancellationToken)
            .ConfigureAwait(false);
#pragma warning restore CA2007
    }

    /// <summary>
    ///     Copies multiple files in parallel.
    /// </summary>
    /// <param name="files">Collection of source and destination file path pairs.</param>
    /// <param name="options">File operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of destination file paths that were copied successfully.</returns>
    /// <exception cref="ArgumentNullException">Thrown when files is null.</exception>
    public static async Task<IReadOnlyList<string>> CopyFilesParallelAsync(
        this IEnumerable<(string sourcePath, string destinationPath)> files,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(files);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

#pragma warning disable CA2007 // ConfigureAwait not applicable with 'await using' statements in CopyFileAsync
        return await files.SelectParallelAsync(
                async (file, ct) =>
                {
                    await FileOperationHelper.CopyFileAsync(file.sourcePath, file.destinationPath, options, ct);
                    return file.destinationPath;
                },
                parallelOptions,
                cancellationToken)
            .ConfigureAwait(false);
#pragma warning restore CA2007
    }

    /// <summary>
    ///     Deletes multiple files in parallel.
    /// </summary>
    /// <param name="filePaths">The collection of file paths to delete.</param>
    /// <param name="options">File operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of file paths that were deleted successfully.</returns>
    /// <exception cref="ArgumentNullException">Thrown when filePaths is null.</exception>
    public static async Task<IReadOnlyList<string>> DeleteFilesParallelAsync(
        this IEnumerable<string> filePaths,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

#pragma warning disable CA2007 // DeleteFileAsync uses Task.Run
        return await filePaths.SelectParallelAsync(
                async (filePath, ct) =>
                {
                    await FileOperationHelper.DeleteFileAsync(filePath, options, ct);
                    return filePath;
                },
                parallelOptions,
                cancellationToken)
            .ConfigureAwait(false);
#pragma warning restore CA2007
    }

    private static ValueTask<string[]> ReadFileLinesAsync(
        string filePath,
        FileOperationOptions options,
        CancellationToken cancellationToken
    )
    {
#pragma warning disable CA2007 // ConfigureAwait not applicable with 'await using' statements
        return FileOperationHelper.ExecuteFileOperationAsync(
            filePath,
            async () =>
            {
                await using var stream = FileOperationHelper.CreateReadStream(filePath, options);

                using var reader = new StreamReader(stream, options.Encoding);
                var lines = new List<string>();

                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                    if (line != null) lines.Add(line);
                }

                return lines.ToArray();
            },
            options,
            static lines => new FileOperationResult { BytesProcessed = lines.Length });
#pragma warning restore CA2007
    }
}
