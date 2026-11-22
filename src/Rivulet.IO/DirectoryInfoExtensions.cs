using System.Diagnostics.CodeAnalysis;

namespace Rivulet.IO;

/// <summary>
///     Extension methods for DirectoryInfo to enable parallel directory operations.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
public static class DirectoryInfoExtensions
{
    /// <param name="directory">The DirectoryInfo to process.</param>
    extension(DirectoryInfo directory)
    {
        /// <summary>
        ///     Processes all files in a DirectoryInfo in parallel using a custom processing function.
        /// </summary>
        /// <typeparam name="TResult">The result type from processing each file.</typeparam>
        /// <param name="processFunc">Function that processes each file.</param>
        /// <param name="searchPattern">The search pattern (e.g., "*.csv", "*.txt"). Default is "*.*".</param>
        /// <param name="searchOption">Specifies whether to search subdirectories.</param>
        /// <param name="options">File operation options. If null, defaults are used.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A list of results from processing each file.</returns>
        /// <exception cref="ArgumentNullException">Thrown when directory or processFunc is null.</exception>
        public Task<IReadOnlyList<TResult>> ProcessFilesParallelAsync<TResult>(
            Func<string, CancellationToken, ValueTask<TResult>> processFunc,
            string searchPattern = "*.*",
            SearchOption searchOption = SearchOption.TopDirectoryOnly,
            FileOperationOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            ArgumentNullException.ThrowIfNull(directory);
            ArgumentNullException.ThrowIfNull(processFunc);

            return !directory.Exists
                ? throw new DirectoryNotFoundException($"Directory not found: {directory.FullName}")
                : DirectoryParallelExtensions.ProcessDirectoryFilesParallelAsync(
                    directory.FullName,
                    searchPattern,
                    processFunc,
                    searchOption,
                    options,
                    cancellationToken);
        }

        /// <summary>
        ///     Reads all files in a DirectoryInfo as text in parallel.
        /// </summary>
        /// <param name="searchPattern">The search pattern. Default is "*.*".</param>
        /// <param name="searchOption">Specifies whether to search subdirectories.</param>
        /// <param name="options">File operation options. If null, defaults are used.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A dictionary mapping file paths to their contents.</returns>
        /// <exception cref="ArgumentNullException">Thrown when directory is null.</exception>
        public Task<IReadOnlyDictionary<string, string>> ReadAllFilesParallelAsync(
            string searchPattern = "*.*",
            SearchOption searchOption = SearchOption.TopDirectoryOnly,
            FileOperationOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            ArgumentNullException.ThrowIfNull(directory);

            return !directory.Exists
                ? throw new DirectoryNotFoundException($"Directory not found: {directory.FullName}")
                : DirectoryParallelExtensions.ReadDirectoryFilesParallelAsync(
                    directory.FullName,
                    searchPattern,
                    searchOption,
                    options,
                    cancellationToken);
        }

        /// <summary>
        ///     Gets all FileInfo objects matching the search pattern.
        /// </summary>
        /// <param name="searchPattern">The search pattern.</param>
        /// <param name="searchOption">Specifies whether to search subdirectories.</param>
        /// <returns>An enumerable of FileInfo objects.</returns>
        /// <exception cref="ArgumentNullException">Thrown when directory is null.</exception>
        public IEnumerable<FileInfo> GetFilesEnumerable(
            string searchPattern = "*.*",
            SearchOption searchOption = SearchOption.TopDirectoryOnly
        )
        {
            ArgumentNullException.ThrowIfNull(directory);

            return !directory.Exists
                ? throw new DirectoryNotFoundException($"Directory not found: {directory.FullName}")
                : directory.EnumerateFiles(searchPattern, searchOption);
        }

        /// <summary>
        ///     Transforms all files in a DirectoryInfo to a destination directory in parallel.
        /// </summary>
        /// <param name="destinationDirectory">The destination directory path.</param>
        /// <param name="transformFunc">Function that transforms file content.</param>
        /// <param name="searchPattern">The search pattern for source files.</param>
        /// <param name="searchOption">Specifies whether to search subdirectories.</param>
        /// <param name="options">File operation options. If null, defaults are used.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A list of destination file paths that were created.</returns>
        /// <exception cref="ArgumentNullException">Thrown when sourceDirectory or transformFunc is null.</exception>
        public Task<IReadOnlyList<string>> TransformFilesParallelAsync(
            string destinationDirectory,
            Func<string, string, ValueTask<string>> transformFunc,
            string searchPattern = "*.*",
            SearchOption searchOption = SearchOption.TopDirectoryOnly,
            FileOperationOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            ArgumentNullException.ThrowIfNull(directory);
            ArgumentNullException.ThrowIfNull(transformFunc);

            return !directory.Exists
                ? throw new DirectoryNotFoundException($"Source directory not found: {directory.FullName}")
                : DirectoryParallelExtensions.TransformDirectoryFilesParallelAsync(
                    directory.FullName,
                    destinationDirectory,
                    searchPattern,
                    transformFunc,
                    searchOption,
                    options,
                    cancellationToken);
        }

        /// <summary>
        ///     Copies all files from a DirectoryInfo to a destination directory in parallel.
        /// </summary>
        /// <param name="destinationDirectory">The destination directory path.</param>
        /// <param name="searchPattern">The search pattern for source files.</param>
        /// <param name="searchOption">Specifies whether to search subdirectories.</param>
        /// <param name="options">File operation options. If null, defaults are used.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A list of destination file paths that were copied.</returns>
        /// <exception cref="ArgumentNullException">Thrown when sourceDirectory is null.</exception>
        public Task<IReadOnlyList<string>> CopyFilesToParallelAsync(
            string destinationDirectory,
            string searchPattern = "*.*",
            SearchOption searchOption = SearchOption.TopDirectoryOnly,
            FileOperationOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            ArgumentNullException.ThrowIfNull(directory);

            return !directory.Exists
                ? throw new DirectoryNotFoundException($"Source directory not found: {directory.FullName}")
                : DirectoryParallelExtensions.CopyDirectoryFilesParallelAsync(
                    directory.FullName,
                    destinationDirectory,
                    searchPattern,
                    searchOption,
                    options,
                    cancellationToken);
        }

        /// <summary>
        ///     Deletes all files matching the search pattern from a DirectoryInfo in parallel.
        /// </summary>
        /// <param name="searchPattern">The search pattern for files to delete.</param>
        /// <param name="searchOption">Specifies whether to search subdirectories.</param>
        /// <param name="options">File operation options. If null, defaults are used.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A list of file paths that were deleted.</returns>
        /// <exception cref="ArgumentNullException">Thrown when directory is null.</exception>
        public Task<IReadOnlyList<string>> DeleteFilesParallelAsync(
            string searchPattern,
            SearchOption searchOption = SearchOption.TopDirectoryOnly,
            FileOperationOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            ArgumentNullException.ThrowIfNull(directory);

            return !directory.Exists
                ? throw new DirectoryNotFoundException($"Directory not found: {directory.FullName}")
                : DirectoryParallelExtensions.DeleteDirectoryFilesParallelAsync(
                    directory.FullName,
                    searchPattern,
                    searchOption,
                    options,
                    cancellationToken);
        }
    }

    /// <summary>
    ///     Processes multiple DirectoryInfo objects in parallel, applying a processing function to each file.
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
    public static Task<IReadOnlyList<TResult>> ProcessMultipleDirectoriesParallelAsync<TResult>(
        this IEnumerable<DirectoryInfo> directories,
        string searchPattern,
        Func<string, CancellationToken, ValueTask<TResult>> processFunc,
        SearchOption searchOption = SearchOption.TopDirectoryOnly,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(directories);
        ArgumentNullException.ThrowIfNull(processFunc);

        var directoryPaths = directories
            .Where(static d => d.Exists)
            .Select(static d => d.FullName);

        return directoryPaths.ProcessMultipleDirectoriesParallelAsync(
            searchPattern,
            processFunc,
            searchOption,
            options,
            cancellationToken);
    }
}
