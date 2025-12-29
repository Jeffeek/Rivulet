using System.Diagnostics.CodeAnalysis;
using System.Text;
using Rivulet.Core;

namespace Rivulet.IO.Base;

/// <summary>
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
public abstract class BaseFileOperationOptions
{
    /// <summary>
    ///     Gets or sets the text encoding to use for file operations.
    ///     Default is UTF-8.
    /// </summary>
    public Encoding Encoding { get; init; } = Encoding.UTF8;

    /// <summary>
    ///     Gets or sets the buffer size for file read/write operations in bytes.
    ///     Default is 81920 bytes (80 KB).
    /// </summary>
    public int BufferSize { get; init; } = 81920;

    /// <summary>
    ///     Gets or sets whether to create directories automatically if they don't exist when writing files.
    ///     Default is true.
    /// </summary>
    public bool CreateDirectoriesIfNotExist { get; init; } = true;

    /// <summary>
    ///     Gets or sets whether to overwrite existing files during write operations.
    ///     Default is false.
    /// </summary>
    public bool OverwriteExisting { get; init; }

    /// <summary>
    ///     Gets or sets the parallel execution options.
    ///     If null, defaults will be used.
    /// </summary>
    public ParallelOptionsRivulet? ParallelOptions { get; init; }

    /// <summary>
    ///     Gets or sets a callback invoked before processing each file.
    /// </summary>
    public Func<string, ValueTask>? OnFileStartAsync { get; init; }

    /// <summary>
    ///     Gets or sets a callback invoked after successfully processing each file.
    ///     Parameters: filePath, result
    /// </summary>
    public Func<string, FileOperationResult, ValueTask>? OnFileCompleteAsync { get; init; }

    /// <summary>
    ///     Gets or sets a callback invoked when an error occurs processing a file.
    ///     Parameters: filePath, exception
    /// </summary>
    public Func<string, Exception, ValueTask>? OnFileErrorAsync { get; init; }

    /// <summary>
    ///     Creates a merged ParallelOptionsRivulet by combining FileOperationOptions.ParallelOptions with defaults.
    /// </summary>
    internal virtual ParallelOptionsRivulet GetMergedParallelOptions() =>
        ParallelOptions ?? new();
}

/// <summary>
///     Represents the result of a file operation, containing metrics about the operation.
/// </summary>
public readonly struct FileOperationResult
{
    /// <summary>
    ///     Gets the number of bytes processed during the file operation.
    /// </summary>
    public required long BytesProcessed { get; init; }

    /// <summary>
    ///     Gets the number of records processed. Only populated for structured data operations (CSV, JSON, etc.).
    ///     Null for raw file operations.
    /// </summary>
    public long? RecordCount { get; init; }
}
