using Rivulet.Core;
using Rivulet.IO.Internal;

namespace Rivulet.IO;

/// <summary>
/// Extension methods for parallel file operations with bounded concurrency and resilience.
/// </summary>
public static class FileParallelExtensions
{
    /// <summary>
    /// Reads multiple files in parallel and returns their contents as strings.
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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        return await filePaths.SelectParallelAsync(
            async (filePath, ct) => await ReadFileTextAsync(filePath, options, ct),
            parallelOptions,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads multiple files in parallel and returns their contents as byte arrays.
    /// </summary>
    /// <param name="filePaths">The collection of file paths to read.</param>
    /// <param name="options">File operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of file contents as byte arrays.</returns>
    /// <exception cref="ArgumentNullException">Thrown when filePaths is null.</exception>
    public static async Task<IReadOnlyList<byte[]>> ReadAllBytesParallelAsync(
        this IEnumerable<string> filePaths,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        return await filePaths.SelectParallelAsync(
            async (filePath, ct) => await ReadFileBytesAsync(filePath, options, ct),
            parallelOptions,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads multiple files in parallel as lines.
    /// </summary>
    /// <param name="filePaths">The collection of file paths to read.</param>
    /// <param name="options">File operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list where each element contains all lines from a file.</returns>
    /// <exception cref="ArgumentNullException">Thrown when filePaths is null.</exception>
    public static async Task<IReadOnlyList<string[]>> ReadAllLinesParallelAsync(
        this IEnumerable<string> filePaths,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        return await filePaths.SelectParallelAsync(
            async (filePath, ct) => await ReadFileLinesAsync(filePath, options, ct),
            parallelOptions,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes content to multiple files in parallel.
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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileWrites);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        return await fileWrites.SelectParallelAsync(
            async (write, ct) =>
            {
                await WriteFileTextAsync(write.filePath, write.content, options, ct);
                return write.filePath;
            },
            parallelOptions,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes byte data to multiple files in parallel.
    /// </summary>
    /// <param name="fileWrites">Collection of tuples containing file path and byte content to write.</param>
    /// <param name="options">File operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of file paths that were written successfully.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fileWrites is null.</exception>
    public static async Task<IReadOnlyList<string>> WriteAllBytesParallelAsync(
        this IEnumerable<(string filePath, byte[] content)> fileWrites,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileWrites);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        return await fileWrites.SelectParallelAsync(
            async (write, ct) =>
            {
                await WriteFileBytesAsync(write.filePath, write.content, options, ct);
                return write.filePath;
            },
            parallelOptions,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Transforms files in parallel by applying a transformation function to their contents.
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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(transformFunc);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        return await files.SelectParallelAsync(
            async (file, ct) =>
            {
                var content = await ReadFileTextAsync(file.sourcePath, options, ct);
                var transformed = await transformFunc(file.sourcePath, content);
                await WriteFileTextAsync(file.destinationPath, transformed, options, ct);
                return file.destinationPath;
            },
            parallelOptions,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Copies multiple files in parallel.
    /// </summary>
    /// <param name="files">Collection of source and destination file path pairs.</param>
    /// <param name="options">File operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of destination file paths that were copied successfully.</returns>
    /// <exception cref="ArgumentNullException">Thrown when files is null.</exception>
    public static async Task<IReadOnlyList<string>> CopyFilesParallelAsync(
        this IEnumerable<(string sourcePath, string destinationPath)> files,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        return await files.SelectParallelAsync(
            async (file, ct) =>
            {
                await CopyFileAsync(file.sourcePath, file.destinationPath, options, ct);
                return file.destinationPath;
            },
            parallelOptions,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes multiple files in parallel.
    /// </summary>
    /// <param name="filePaths">The collection of file paths to delete.</param>
    /// <param name="options">File operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of file paths that were deleted successfully.</returns>
    /// <exception cref="ArgumentNullException">Thrown when filePaths is null.</exception>
    public static async Task<IReadOnlyList<string>> DeleteFilesParallelAsync(
        this IEnumerable<string> filePaths,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        return await filePaths.SelectParallelAsync(
            async (filePath, ct) =>
            {
                await DeleteFileAsync(filePath, options, ct);
                return filePath;
            },
            parallelOptions,
            cancellationToken).ConfigureAwait(false);
    }

    // Private helper methods

    private static async ValueTask<string> ReadFileTextAsync(
        string filePath,
        FileOperationOptions options,
        CancellationToken cancellationToken)
    {
        return await FileOperationHelper.ExecuteFileOperationAsync(
            filePath,
            async () =>
            {
                await using var stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    options.ReadFileShare,
                    options.BufferSize,
                    useAsync: true);

                using var reader = new StreamReader(stream, options.Encoding);
                return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            },
            options,
            content => content.Length);
    }

    private static async ValueTask<byte[]> ReadFileBytesAsync(
        string filePath,
        FileOperationOptions options,
        CancellationToken cancellationToken)
    {
        return await FileOperationHelper.ExecuteFileOperationAsync(
            filePath,
            async () =>
            {
                await using var stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    options.ReadFileShare,
                    options.BufferSize,
                    useAsync: true);

                var buffer = new byte[stream.Length];
                _ = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                return buffer;
            },
            options,
            bytes => bytes.Length);
    }

    private static async ValueTask<string[]> ReadFileLinesAsync(
        string filePath,
        FileOperationOptions options,
        CancellationToken cancellationToken)
    {
        return await FileOperationHelper.ExecuteFileOperationAsync(
            filePath,
            async () =>
            {
                await using var stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    options.ReadFileShare,
                    options.BufferSize,
                    useAsync: true);

                using var reader = new StreamReader(stream, options.Encoding);
                var lines = new List<string>();

                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                    if (line != null)
                    {
                        lines.Add(line);
                    }
                }

                return lines.ToArray();
            },
            options,
            lines => lines.Length);
    }

    private static async ValueTask WriteFileTextAsync(
        string filePath,
        string content,
        FileOperationOptions options,
        CancellationToken cancellationToken)
    {
        await FileOperationHelper.ExecuteFileOperationAsync(
            filePath,
            async () =>
            {
                FileOperationHelper.EnsureDirectoryExists(filePath, options);
                FileOperationHelper.ValidateOverwrite(filePath, options);

                await using var stream = new FileStream(
                    filePath,
                    FileMode.Create,
                    FileAccess.Write,
                    options.WriteFileShare,
                    options.BufferSize,
                    useAsync: true);

                await using var writer = new StreamWriter(stream, options.Encoding);
                await writer.WriteAsync(content.AsMemory(), cancellationToken).ConfigureAwait(false);
                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

                return content.Length;
            },
            options,
            length => length);
    }

    private static async ValueTask WriteFileBytesAsync(
        string filePath,
        byte[] content,
        FileOperationOptions options,
        CancellationToken cancellationToken)
    {
        await FileOperationHelper.ExecuteFileOperationAsync(
            filePath,
            async () =>
            {
                FileOperationHelper.EnsureDirectoryExists(filePath, options);
                FileOperationHelper.ValidateOverwrite(filePath, options);

                await using var stream = new FileStream(
                    filePath,
                    FileMode.Create,
                    FileAccess.Write,
                    options.WriteFileShare,
                    options.BufferSize,
                    useAsync: true);

                await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

                return content.Length;
            },
            options,
            length => length);
    }

    private static async ValueTask CopyFileAsync(
        string sourcePath,
        string destinationPath,
        FileOperationOptions options,
        CancellationToken cancellationToken)
    {
        await FileOperationHelper.ExecuteFileOperationAsync(
            sourcePath,
            async () =>
            {
                FileOperationHelper.EnsureDirectoryExists(destinationPath, options);
                FileOperationHelper.ValidateOverwrite(destinationPath, options);

                await using var sourceStream = new FileStream(
                    sourcePath,
                    FileMode.Open,
                    FileAccess.Read,
                    options.ReadFileShare,
                    options.BufferSize,
                    useAsync: true);

                await using var destStream = new FileStream(
                    destinationPath,
                    FileMode.Create,
                    FileAccess.Write,
                    options.WriteFileShare,
                    options.BufferSize,
                    useAsync: true);

                await sourceStream.CopyToAsync(destStream, options.BufferSize, cancellationToken).ConfigureAwait(false);
                await destStream.FlushAsync(cancellationToken).ConfigureAwait(false);

                return sourceStream.Length;
            },
            options,
            length => length);
    }

    private static async ValueTask DeleteFileAsync(
        string filePath,
        FileOperationOptions options,
        CancellationToken cancellationToken)
    {
        await FileOperationHelper.ExecuteFileOperationAsync(
            filePath,
            async () =>
            {
                await Task.Run(() => File.Delete(filePath), cancellationToken).ConfigureAwait(false);
                return 0;
            },
            options);
    }
}
