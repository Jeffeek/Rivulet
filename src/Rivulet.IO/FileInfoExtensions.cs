using System.Diagnostics.CodeAnalysis;
using Rivulet.IO.Internal;

namespace Rivulet.IO;

/// <summary>
///     Extension methods for FileInfo to enable parallel operations on collections of FileInfo objects.
/// </summary>
#pragma warning disable IDE0079
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
#pragma warning restore IDE0079
public static class FileInfoExtensions
{
    /// <summary>
    ///     Reads multiple FileInfo objects in parallel and returns their contents as strings.
    /// </summary>
    /// <param name="files">The collection of FileInfo objects to read.</param>
    /// <param name="options">File operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of file contents as strings.</returns>
    /// <exception cref="ArgumentNullException">Thrown when files is null.</exception>
    public static Task<IReadOnlyList<string>> ReadAllTextParallelAsync(
        this IEnumerable<FileInfo> files,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(files);

        return files.Select(static f => f.FullName)
            .ReadAllTextParallelAsync(options, cancellationToken);
    }

    /// <summary>
    ///     Reads multiple FileInfo objects in parallel and returns their contents as byte arrays.
    /// </summary>
    /// <param name="files">The collection of FileInfo objects to read.</param>
    /// <param name="options">File operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of file contents as byte arrays.</returns>
    /// <exception cref="ArgumentNullException">Thrown when files is null.</exception>
    public static Task<IReadOnlyList<byte[]>> ReadAllBytesParallelAsync(
        this IEnumerable<FileInfo> files,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(files);

        return files.Select(static f => f.FullName)
            .ReadAllBytesParallelAsync(options, cancellationToken);
    }

    /// <summary>
    ///     Reads a single FileInfo object as text asynchronously.
    /// </summary>
    /// <param name="file">The FileInfo object to read.</param>
    /// <param name="options">File operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The file content as a string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when file is null.</exception>
    public static ValueTask<string> ReadAllTextAsync(
        this FileInfo file,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(file);

        return FileOperationHelper.ReadFileTextAsync(file.FullName, options ?? new(), cancellationToken);
    }

    /// <summary>
    ///     Reads a single FileInfo object as bytes asynchronously.
    /// </summary>
    /// <param name="file">The FileInfo object to read.</param>
    /// <param name="options">File operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The file content as a byte array.</returns>
    /// <exception cref="ArgumentNullException">Thrown when file is null.</exception>
    public static ValueTask<byte[]> ReadAllBytesAsync(
        this FileInfo file,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(file);

        return FileOperationHelper.ReadFileBytesAsync(file.FullName, options ?? new(), cancellationToken);
    }

    /// <summary>
    ///     Writes text content to a FileInfo object asynchronously.
    /// </summary>
    /// <param name="file">The FileInfo object to write to.</param>
    /// <param name="content">The text content to write.</param>
    /// <param name="options">File operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>File length.</returns>
    /// <exception cref="ArgumentNullException">Thrown when file is null.</exception>
    /// <exception cref="IOException">Thrown when file exists and OverwriteExisting is false.</exception>
    public static async ValueTask<int> WriteAllTextAsync(
        this FileInfo file,
        string content,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(file);

        await FileOperationHelper.WriteFileTextAsync(file.FullName, content, options ?? new(), cancellationToken)
            .ConfigureAwait(false);

        return content.Length;
    }

    /// <summary>
    ///     Writes byte content to a FileInfo object asynchronously.
    /// </summary>
    /// <param name="file">The FileInfo object to write to.</param>
    /// <param name="content">The byte content to write.</param>
    /// <param name="options">File operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>File length.</returns>
    /// <exception cref="ArgumentNullException">Thrown when file is null.</exception>
    /// <exception cref="IOException">Thrown when file exists and OverwriteExisting is false.</exception>
    public static async ValueTask<int> WriteAllBytesAsync(
        this FileInfo file,
        byte[] content,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(file);

        await FileOperationHelper.WriteFileBytesAsync(file.FullName, content, options ?? new(), cancellationToken)
            .ConfigureAwait(false);

        return content.Length;
    }

    /// <summary>
    ///     Copies a FileInfo to a destination path asynchronously.
    /// </summary>
    /// <param name="file">The source FileInfo object.</param>
    /// <param name="destinationPath">The destination file path.</param>
    /// <param name="options">File operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>Stream length.</returns>
    /// <exception cref="ArgumentNullException">Thrown when file is null.</exception>
    /// <exception cref="IOException">Thrown when destination exists and OverwriteExisting is false.</exception>
    public static async ValueTask<long> CopyToAsync(
        this FileInfo file,
        string destinationPath,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(file);

        await FileOperationHelper.CopyFileAsync(file.FullName, destinationPath, options ?? new(), cancellationToken)
            .ConfigureAwait(false);

        return file.Length;
    }

    /// <summary>
    ///     Deletes a FileInfo object asynchronously.
    /// </summary>
    /// <param name="file">The FileInfo object to delete.</param>
    /// <param name="options">File operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>
    ///     <value>0</value>
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when file is null.</exception>
    public static async ValueTask<int> DeleteAsync(
        this FileInfo file,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(file);

        await FileOperationHelper.DeleteFileAsync(file.FullName, options ?? new(), cancellationToken)
            .ConfigureAwait(false);

        return 0;
    }
}
