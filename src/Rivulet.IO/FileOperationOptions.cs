using Rivulet.Core;
using Rivulet.IO.Internal;
using System.Diagnostics.CodeAnalysis;

namespace Rivulet.IO;

/// <summary>
///     Configuration options for file operations with Rivulet.IO.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
public sealed class FileOperationOptions : BaseFileOperationOptions
{
    /// <summary>
    ///     Gets or sets the file share mode for reading operations.
    ///     Default is FileShare.Read (allows other processes to read concurrently).
    /// </summary>
    public FileShare ReadFileShare { get; init; } = FileShare.Read;

    /// <summary>
    ///     Gets or sets the file share mode for writing operations.
    ///     Default is FileShare.None (exclusive access).
    /// </summary>
    public FileShare WriteFileShare { get; init; } = FileShare.None;

    /// <summary>
    ///     Creates a merged ParallelOptionsRivulet by combining FileOperationOptions.ParallelOptions with defaults.
    /// </summary>
    internal ParallelOptionsRivulet GetMergedParallelOptions() =>
        ParallelOptions ?? new ParallelOptionsRivulet();
}
