using Rivulet.Core;

namespace Rivulet.IO;

/// <summary>
/// Extension methods for parallel directory and file processing operations.
/// </summary>
public static class DirectoryParallelExtensions
{
    /// <summary>
    /// Processes all files in a directory in parallel using a custom processing function.
    /// This is the core method matching the ROADMAP example: Directory.GetFiles("*.csv").ProcessFilesParallelAsync(...)
    /// </summary>
    /// <typeparam name="TResult">The result type from processing each file.</typeparam>
    /// <param name="filePaths">The collection of file paths to process.</param>
    /// <param name="processFunc">Function that processes each file. Parameters: filePath, cancellationToken.</param>
    /// <param name="options">File operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of results from processing each file.</returns>
    /// <exception cref="ArgumentNullException">Thrown when filePaths or processFunc is null.</exception>
    public static async Task<IReadOnlyList<TResult>> ProcessFilesParallelAsync<TResult>(
        this IEnumerable<string> filePaths,
        Func<string, CancellationToken, ValueTask<TResult>> processFunc,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        ArgumentNullException.ThrowIfNull(processFunc);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        return await filePaths.SelectParallelAsync(
            async (filePath, ct) =>
            {
                if (options.OnFileStartAsync != null)
                {
                    await options.OnFileStartAsync(filePath).ConfigureAwait(false);
                }

                try
                {
                    var result = await processFunc(filePath, ct);

                    if (options.OnFileCompleteAsync == null)
                        return result;

                    var fileInfo = new FileInfo(filePath);
                    await options.OnFileCompleteAsync(filePath, fileInfo.Length).ConfigureAwait(false);

                    return result;
                }
                catch (Exception ex)
                {
                    if (options.OnFileErrorAsync != null)
                    {
                        await options.OnFileErrorAsync(filePath, ex).ConfigureAwait(false);
                    }
                    throw;
                }
            },
            parallelOptions,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets all files matching a search pattern from a directory and processes them in parallel.
    /// </summary>
    /// <typeparam name="TResult">The result type from processing each file.</typeparam>
    /// <param name="directoryPath">The directory path to search.</param>
    /// <param name="searchPattern">The search pattern (e.g., "*.csv", "*.txt"). Default is "*.*" (all files).</param>
    /// <param name="processFunc">Function that processes each file.</param>
    /// <param name="searchOption">Specifies whether to search only the current directory or all subdirectories.</param>
    /// <param name="options">File operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of results from processing each file.</returns>
    /// <exception cref="ArgumentNullException">Thrown when directoryPath or processFunc is null.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when the directory doesn't exist.</exception>
    public static async Task<IReadOnlyList<TResult>> ProcessDirectoryFilesParallelAsync<TResult>(
        string directoryPath,
        string searchPattern,
        Func<string, CancellationToken, ValueTask<TResult>> processFunc,
        SearchOption searchOption = SearchOption.TopDirectoryOnly,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentNullException.ThrowIfNull(processFunc);

        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
        }

        var files = Directory.GetFiles(directoryPath, searchPattern, searchOption);
        return await files.ProcessFilesParallelAsync(processFunc, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets all files from a directory and reads them as text in parallel.
    /// </summary>
    /// <param name="directoryPath">The directory path to search.</param>
    /// <param name="searchPattern">The search pattern. Default is "*.*".</param>
    /// <param name="searchOption">Specifies whether to search subdirectories.</param>
    /// <param name="options">File operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A dictionary mapping file paths to their contents.</returns>
    public static async Task<IReadOnlyDictionary<string, string>> ReadDirectoryFilesParallelAsync(
        string directoryPath,
        string searchPattern = "*.*",
        SearchOption searchOption = SearchOption.TopDirectoryOnly,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
        }

        var files = Directory.GetFiles(directoryPath, searchPattern, searchOption);

        // Ensure ordered output so Zip matches files with their contents correctly
        options ??= new();
        var parallelOpts = options.ParallelOptions ?? new();

        var orderedOptions = new FileOperationOptions
        {
            BufferSize = options.BufferSize,
            Encoding = options.Encoding,
            CreateDirectoriesIfNotExist = options.CreateDirectoriesIfNotExist,
            OverwriteExisting = options.OverwriteExisting,
            ReadFileShare = options.ReadFileShare,
            WriteFileShare = options.WriteFileShare,
            OnFileStartAsync = options.OnFileStartAsync,
            OnFileCompleteAsync = options.OnFileCompleteAsync,
            OnFileErrorAsync = options.OnFileErrorAsync,
            ParallelOptions = new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = parallelOpts.MaxDegreeOfParallelism,
                OrderedOutput = true // Force ordered output for correct Zip alignment
            }
        };

        var contents = await files.ReadAllTextParallelAsync(orderedOptions, cancellationToken).ConfigureAwait(false);

        return files.Zip(contents, (file, content) => (file, content))
            .ToDictionary(x => x.file, x => x.content);
    }

    /// <summary>
    /// Transforms all files in a directory in parallel by applying a transformation function.
    /// </summary>
    /// <param name="sourceDirectory">The source directory containing files to transform.</param>
    /// <param name="destinationDirectory">The destination directory for transformed files.</param>
    /// <param name="searchPattern">The search pattern for source files.</param>
    /// <param name="transformFunc">Function that transforms file content.</param>
    /// <param name="searchOption">Specifies whether to search subdirectories.</param>
    /// <param name="options">File operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of destination file paths that were created.</returns>
    public static async Task<IReadOnlyList<string>> TransformDirectoryFilesParallelAsync(
        string sourceDirectory,
        string destinationDirectory,
        string searchPattern,
        Func<string, string, ValueTask<string>> transformFunc,
        SearchOption searchOption = SearchOption.TopDirectoryOnly,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        ArgumentNullException.ThrowIfNull(transformFunc);

        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDirectory}");
        }

        var sourceFiles = Directory.GetFiles(sourceDirectory, searchPattern, searchOption);

        var filePairs = sourceFiles.Select(sourcePath =>
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, sourcePath);
            var destPath = Path.Combine(destinationDirectory, relativePath);
            return (sourcePath, destPath);
        });

        return await filePairs.TransformFilesParallelAsync(transformFunc, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Copies all files from source directory to destination directory in parallel.
    /// </summary>
    /// <param name="sourceDirectory">The source directory.</param>
    /// <param name="destinationDirectory">The destination directory.</param>
    /// <param name="searchPattern">The search pattern for source files.</param>
    /// <param name="searchOption">Specifies whether to search subdirectories.</param>
    /// <param name="options">File operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of destination file paths that were copied.</returns>
    public static async Task<IReadOnlyList<string>> CopyDirectoryFilesParallelAsync(
        string sourceDirectory,
        string destinationDirectory,
        string searchPattern = "*.*",
        SearchOption searchOption = SearchOption.TopDirectoryOnly,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDirectory}");
        }

        var sourceFiles = Directory.GetFiles(sourceDirectory, searchPattern, searchOption);

        var filePairs = sourceFiles.Select(sourcePath =>
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, sourcePath);
            var destPath = Path.Combine(destinationDirectory, relativePath);
            return (sourcePath, destPath);
        });

        return await filePairs.CopyFilesParallelAsync(options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes all files matching the search pattern from a directory in parallel.
    /// </summary>
    /// <param name="directoryPath">The directory path.</param>
    /// <param name="searchPattern">The search pattern for files to delete.</param>
    /// <param name="searchOption">Specifies whether to search subdirectories.</param>
    /// <param name="options">File operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of file paths that were deleted.</returns>
    public static async Task<IReadOnlyList<string>> DeleteDirectoryFilesParallelAsync(
        string directoryPath,
        string searchPattern,
        SearchOption searchOption = SearchOption.TopDirectoryOnly,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
        }

        var files = Directory.GetFiles(directoryPath, searchPattern, searchOption);
        return await files.DeleteFilesParallelAsync(options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Processes multiple directories in parallel, applying a processing function to each file.
    /// </summary>
    /// <typeparam name="TResult">The result type from processing each file.</typeparam>
    /// <param name="directoryPaths">The collection of directory paths to process.</param>
    /// <param name="searchPattern">The search pattern for files in each directory.</param>
    /// <param name="processFunc">Function that processes each file.</param>
    /// <param name="searchOption">Specifies whether to search subdirectories.</param>
    /// <param name="options">File operation options. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of results from processing all files across all directories.</returns>
    public static async Task<IReadOnlyList<TResult>> ProcessMultipleDirectoriesParallelAsync<TResult>(
        this IEnumerable<string> directoryPaths,
        string searchPattern,
        Func<string, CancellationToken, ValueTask<TResult>> processFunc,
        SearchOption searchOption = SearchOption.TopDirectoryOnly,
        FileOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(directoryPaths);
        ArgumentNullException.ThrowIfNull(processFunc);

        options ??= new();

        var allFiles = directoryPaths
            .Where(Directory.Exists)
            .SelectMany(dir => Directory.GetFiles(dir, searchPattern, searchOption));

        return await allFiles.ProcessFilesParallelAsync(processFunc, options, cancellationToken).ConfigureAwait(false);
    }
}
