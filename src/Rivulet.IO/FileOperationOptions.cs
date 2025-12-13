using Rivulet.Core;

namespace Rivulet.IO;

/// <summary>
/// Configuration options for file operations with Rivulet.IO.
/// </summary>
public class FileOperationOptions
{
    /// <summary>
    /// Gets or sets the buffer size for file read/write operations in bytes.
    /// Default is 81920 bytes (80 KB).
    /// </summary>
    public int BufferSize { get; set; } = 81920;

    /// <summary>
    /// Gets or sets the text encoding to use for text file operations.
    /// Default is UTF-8.
    /// </summary>
    public System.Text.Encoding Encoding { get; init; } = System.Text.Encoding.UTF8;

    /// <summary>
    /// Gets or sets whether to create directories automatically if they don't exist when writing files.
    /// Default is true.
    /// </summary>
    public bool CreateDirectoriesIfNotExist { get; init; } = true;

    /// <summary>
    /// Gets or sets whether to overwrite existing files during write operations.
    /// Default is false.
    /// </summary>
    public bool OverwriteExisting { get; init; }

    /// <summary>
    /// Gets or sets the file share mode for reading operations.
    /// Default is FileShare.Read (allows other processes to read concurrently).
    /// </summary>
    public FileShare ReadFileShare { get; init; } = FileShare.Read;

    /// <summary>
    /// Gets or sets the file share mode for writing operations.
    /// Default is FileShare.None (exclusive access).
    /// </summary>
    public FileShare WriteFileShare { get; init; } = FileShare.None;

    /// <summary>
    /// Gets or sets the parallel execution options.
    /// If null, defaults will be used.
    /// </summary>
    public ParallelOptionsRivulet? ParallelOptions { get; init; }

    /// <summary>
    /// Gets or sets a callback invoked before processing each file.
    /// </summary>
    public Func<string, ValueTask>? OnFileStartAsync { get; init; }

    /// <summary>
    /// Gets or sets a callback invoked after successfully processing each file.
    /// Parameters: filePath, bytesProcessed
    /// </summary>
    public Func<string, long, ValueTask>? OnFileCompleteAsync { get; init; }

    /// <summary>
    /// Gets or sets a callback invoked when an error occurs processing a file.
    /// Parameters: filePath, exception
    /// </summary>
    public Func<string, Exception, ValueTask>? OnFileErrorAsync { get; init; }

    /// <summary>
    /// Creates a merged ParallelOptionsRivulet by combining FileOperationOptions.ParallelOptions with defaults.
    /// </summary>
    internal ParallelOptionsRivulet GetMergedParallelOptions() =>
        ParallelOptions ?? new ParallelOptionsRivulet();
}
