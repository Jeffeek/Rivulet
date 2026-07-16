using System.Text;
using Rivulet.Core.Internal;

namespace Rivulet.Diagnostics.Internal;

/// <summary>
///     Base class for Rivulet EventListeners that write to a file via StreamWriter.
///     Provides shared disposal, writer management, and thread-safe locking patterns.
/// </summary>
public abstract class FileEventListenerBase : RivuletEventListenerBase, IAsyncDisposable
{
    private readonly object _lock = LockFactory.CreateLock();
    private bool _disposed;

    /// <summary>
///     Gets or sets the current StreamWriter. May be null after disposal.
/// </summary>
    protected StreamWriter? Writer { get; set; }

    /// <summary>
///     Creates and initializes the StreamWriter for the specified file path.
///     Creates the parent directory if it doesn't exist.
/// </summary>
    protected void InitializeWriter(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

        Writer = new(filePath, true, Encoding.UTF8) { AutoFlush = false };
    }

    /// <summary>
///     Executes the action under lock for thread-safe file operations.
/// </summary>
    protected void ExecuteUnderLock(Action action) => LockHelper.Execute(_lock, action);

    /// <summary>
///     Disposes the file listener asynchronously and closes the file.
/// </summary>
    public async ValueTask DisposeAsync()
    {
        await StreamWriterDisposalHelper.ExtractAndDisposeAsync(_lock,
            () =>
            {
                if (_disposed)
                    return null;

                _disposed = true;
                var w = Writer;
                Writer = null;
                return w;
            }).ConfigureAwait(false);

        base.Dispose();
    }

    /// <summary>
///     Disposes the file listener and closes the file.
/// </summary>
    public override void Dispose()
    {
        StreamWriterDisposalHelper.ExtractAndDispose(_lock,
            () =>
            {
                if (_disposed)
                    return null;

                _disposed = true;
                var w = Writer;
                Writer = null;
                return w;
            });

        base.Dispose();
    }
}
