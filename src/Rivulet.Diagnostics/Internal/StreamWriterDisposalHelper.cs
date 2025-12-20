using Rivulet.Core.Internal;

namespace Rivulet.Diagnostics.Internal;

/// <summary>
///     Helper methods for StreamWriter disposal patterns.
/// </summary>
internal static class StreamWriterDisposalHelper
{
    /// <summary>
    ///     Extracts a StreamWriter from under a lock and disposes it asynchronously.
    /// </summary>
    /// <param name="lock">The lock object.</param>
    /// <param name="extractWriter">Function that extracts the writer and sets field to null under lock.</param>
    /// <returns>A task representing the async disposal operation.</returns>
    /// <remarks>
    ///     This method follows the extract-under-lock pattern:
    ///     1. Acquire lock and extract writer reference
    ///     2. Set field to null
    ///     3. Release lock
    ///     4. Dispose writer outside of lock to avoid blocking
    /// </remarks>
    public static async ValueTask ExtractAndDisposeAsync(object @lock, Func<StreamWriter?> extractWriter)
    {
        var writer = LockHelper.Execute(@lock, extractWriter);

        if (writer is null) return;

        await writer.FlushAsync().ConfigureAwait(false);
        writer.Close();
        await writer.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Extracts a StreamWriter from under a lock and disposes it asynchronously.
    /// </summary>
    /// <param name="lock">The lock object.</param>
    /// <param name="extractWriter">Function that extracts the writer and sets field to null under lock.</param>
    /// <returns>A task representing the async disposal operation.</returns>
    /// <remarks>
    ///     This method follows the extract-under-lock pattern:
    ///     1. Acquire lock and extract writer reference
    ///     2. Set field to null
    ///     3. Release lock
    ///     4. Dispose writer outside of lock to avoid blocking
    /// </remarks>
    public static void ExtractAndDispose(object @lock, Func<StreamWriter?> extractWriter)
    {
        var writer = LockHelper.Execute(@lock, extractWriter);

        if (writer is null) return;

        writer.Flush();
        writer.Close();
        writer.Dispose();
    }
}
