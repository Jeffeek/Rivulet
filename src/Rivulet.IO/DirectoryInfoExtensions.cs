namespace Rivulet.IO;

/// <summary>
/// Extension methods for DirectoryInfo to enable parallel directory operations.
/// </summary>
public static class DirectoryInfoExtensions
{
    /// <summary>
    /// Processes all files in a DirectoryInfo in parallel using a custom processing function.
    /// </summary>
    /// <typeparam name="TResult">The result type from processing each file.</typeparam>
    /// <param name="directory">The DirectoryInfo to process.</param>
    /// <param name="processFunc">Function that processes each file.</param>
    /// <param name="searchPattern">The search pattern (e.g., "*.csv", "*.txt"). Default is "*.*".</param>
    /// <param name="searchOption">Specifies whether to search subdirectories.</param>
    /// <param name="options">File operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of results from processing each file.</returns>
    /// <exception cref="ArgumentNullException">Thrown when directory or processFunc is null.</exception>
    public static async Task<IReadOnlyList<TResult>> ProcessFilesParallelAsync<TResult>(
        this DirectoryInfo directory,
        Func<string, CancellationToken, ValueTask<TResult>> processFunc,
        string searchPattern = "*.*",
        SearchOption searchOption = SearchOption.TopDirectoryOnly,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(processFunc);

        if (!directory.Exists)
        {
            throw new DirectoryNotFoundException($"Directory not found: {directory.FullName}");
        }

        return await DirectoryParallelExtensions.ProcessDirectoryFilesParallelAsync(
            directory.FullName,
            searchPattern,
            processFunc,
            searchOption,
            options,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads all files in a DirectoryInfo as text in parallel.
    /// </summary>
    /// <param name="directory">The DirectoryInfo to read from.</param>
    /// <param name="searchPattern">The search pattern. Default is "*.*".</param>
    /// <param name="searchOption">Specifies whether to search subdirectories.</param>
    /// <param name="options">File operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A dictionary mapping file paths to their contents.</returns>
    /// <exception cref="ArgumentNullException">Thrown when directory is null.</exception>
    public static async Task<IReadOnlyDictionary<string, string>> ReadAllFilesParallelAsync(
        this DirectoryInfo directory,
        string searchPattern = "*.*",
        SearchOption searchOption = SearchOption.TopDirectoryOnly,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(directory);

        if (!directory.Exists)
        {
            throw new DirectoryNotFoundException($"Directory not found: {directory.FullName}");
        }

        return await DirectoryParallelExtensions.ReadDirectoryFilesParallelAsync(
            directory.FullName,
            searchPattern,
            searchOption,
            options,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets all FileInfo objects matching the search pattern.
    /// </summary>
    /// <param name="directory">The DirectoryInfo to search.</param>
    /// <param name="searchPattern">The search pattern.</param>
    /// <param name="searchOption">Specifies whether to search subdirectories.</param>
    /// <returns>An enumerable of FileInfo objects.</returns>
    /// <exception cref="ArgumentNullException">Thrown when directory is null.</exception>
    public static IEnumerable<FileInfo> GetFilesEnumerable(
        this DirectoryInfo directory,
        string searchPattern = "*.*",
        SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        ArgumentNullException.ThrowIfNull(directory);

        if (!directory.Exists)
        {
            throw new DirectoryNotFoundException($"Directory not found: {directory.FullName}");
        }

        return directory.EnumerateFiles(searchPattern, searchOption);
    }

    /// <summary>
    /// Transforms all files in a DirectoryInfo to a destination directory in parallel.
    /// </summary>
    /// <param name="sourceDirectory">The source DirectoryInfo.</param>
    /// <param name="destinationDirectory">The destination directory path.</param>
    /// <param name="transformFunc">Function that transforms file content.</param>
    /// <param name="searchPattern">The search pattern for source files.</param>
    /// <param name="searchOption">Specifies whether to search subdirectories.</param>
    /// <param name="options">File operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of destination file paths that were created.</returns>
    /// <exception cref="ArgumentNullException">Thrown when sourceDirectory or transformFunc is null.</exception>
    public static async Task<IReadOnlyList<string>> TransformFilesParallelAsync(
        this DirectoryInfo sourceDirectory,
        string destinationDirectory,
        Func<string, string, ValueTask<string>> transformFunc,
        string searchPattern = "*.*",
        SearchOption searchOption = SearchOption.TopDirectoryOnly,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceDirectory);
        ArgumentNullException.ThrowIfNull(transformFunc);

        if (!sourceDirectory.Exists)
        {
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDirectory.FullName}");
        }

        return await DirectoryParallelExtensions.TransformDirectoryFilesParallelAsync(
            sourceDirectory.FullName,
            destinationDirectory,
            searchPattern,
            transformFunc,
            searchOption,
            options,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Copies all files from a DirectoryInfo to a destination directory in parallel.
    /// </summary>
    /// <param name="sourceDirectory">The source DirectoryInfo.</param>
    /// <param name="destinationDirectory">The destination directory path.</param>
    /// <param name="searchPattern">The search pattern for source files.</param>
    /// <param name="searchOption">Specifies whether to search subdirectories.</param>
    /// <param name="options">File operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of destination file paths that were copied.</returns>
    /// <exception cref="ArgumentNullException">Thrown when sourceDirectory is null.</exception>
    public static async Task<IReadOnlyList<string>> CopyFilesToParallelAsync(
        this DirectoryInfo sourceDirectory,
        string destinationDirectory,
        string searchPattern = "*.*",
        SearchOption searchOption = SearchOption.TopDirectoryOnly,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceDirectory);

        if (!sourceDirectory.Exists)
        {
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDirectory.FullName}");
        }

        return await DirectoryParallelExtensions.CopyDirectoryFilesParallelAsync(
            sourceDirectory.FullName,
            destinationDirectory,
            searchPattern,
            searchOption,
            options,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes all files matching the search pattern from a DirectoryInfo in parallel.
    /// </summary>
    /// <param name="directory">The DirectoryInfo.</param>
    /// <param name="searchPattern">The search pattern for files to delete.</param>
    /// <param name="searchOption">Specifies whether to search subdirectories.</param>
    /// <param name="options">File operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of file paths that were deleted.</returns>
    /// <exception cref="ArgumentNullException">Thrown when directory is null.</exception>
    public static async Task<IReadOnlyList<string>> DeleteFilesParallelAsync(
        this DirectoryInfo directory,
        string searchPattern,
        SearchOption searchOption = SearchOption.TopDirectoryOnly,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(directory);

        if (!directory.Exists)
        {
            throw new DirectoryNotFoundException($"Directory not found: {directory.FullName}");
        }

        return await DirectoryParallelExtensions.DeleteDirectoryFilesParallelAsync(
            directory.FullName,
            searchPattern,
            searchOption,
            options,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Processes multiple DirectoryInfo objects in parallel, applying a processing function to each file.
    /// </summary>
    /// <typeparam name="TResult">The result type from processing each file.</typeparam>
    /// <param name="directories">The collection of DirectoryInfo objects to process.</param>
    /// <param name="searchPattern">The search pattern for files in each directory.</param>
    /// <param name="processFunc">Function that processes each file.</param>
    /// <param name="searchOption">Specifies whether to search subdirectories.</param>
    /// <param name="options">File operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of results from processing all files across all directories.</returns>
    /// <exception cref="ArgumentNullException">Thrown when directories or processFunc is null.</exception>
    public static async Task<IReadOnlyList<TResult>> ProcessMultipleDirectoriesParallelAsync<TResult>(
        this IEnumerable<DirectoryInfo> directories,
        string searchPattern,
        Func<string, CancellationToken, ValueTask<TResult>> processFunc,
        SearchOption searchOption = SearchOption.TopDirectoryOnly,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(directories);
        ArgumentNullException.ThrowIfNull(processFunc);

        var directoryPaths = directories
            .Where(d => d.Exists)
            .Select(d => d.FullName);

        return await directoryPaths.ProcessMultipleDirectoriesParallelAsync(
            searchPattern,
            processFunc,
            searchOption,
            options,
            cancellationToken).ConfigureAwait(false);
    }
}
