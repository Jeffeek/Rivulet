namespace Rivulet.Diagnostics.Internal;

/// <summary>
///     Helper methods for disposing collections of listeners with exception swallowing.
/// </summary>
internal static class ListenerCollectionDisposalHelper
{
    /// <summary>
    ///     Disposes all listeners in a collection synchronously, swallowing any exceptions.
    /// </summary>
    /// <param name="listeners">The collection of disposable listeners.</param>
    /// <remarks>
    ///     Exceptions during disposal are intentionally swallowed to ensure all listeners
    ///     are disposed even if some fail. This prevents cascading failures during cleanup.
    /// </remarks>
    public static void DisposeAll(IEnumerable<IDisposable> listeners)
    {
        foreach (var listener in listeners)
        {
            try
            {
                listener.Dispose();
            }
#pragma warning disable CA1031 // Do not catch general exception types - disposal exceptions must not propagate during cleanup
            catch
#pragma warning restore CA1031
            {
                // Swallow exceptions during disposal to ensure all listeners are disposed
            }
        }
    }

    /// <summary>
    ///     Disposes all listeners in a collection asynchronously, swallowing any exceptions.
    /// </summary>
    /// <param name="listeners">The collection of async disposable listeners.</param>
    /// <remarks>
    ///     Exceptions during disposal are intentionally swallowed to ensure all listeners
    ///     are disposed even if some fail. This prevents cascading failures during cleanup.
    /// </remarks>
    public static async ValueTask DisposeAllAsync(IEnumerable<IAsyncDisposable> listeners)
    {
        foreach (var listener in listeners)
        {
            try
            {
                await listener.DisposeAsync().ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Do not catch general exception types - disposal exceptions must not propagate during cleanup
            catch
#pragma warning restore CA1031
            {
                // Swallow exceptions during disposal to ensure all listeners are disposed
            }
        }
    }

    /// <summary>
    ///     Disposes all async listeners synchronously by blocking, swallowing any exceptions.
    /// </summary>
    /// <param name="listeners">The collection of async disposable listeners.</param>
    /// <remarks>
    ///     This is a fallback for when async disposal is not available (e.g., IDisposable.Dispose).
    ///     Prefer DisposeAllAsync when possible for proper async cleanup.
    ///     Exceptions during disposal are intentionally swallowed to ensure all listeners are disposed.
    /// </remarks>
    public static void DisposeAllAsyncBlocking(IEnumerable<IAsyncDisposable> listeners)
    {
        foreach (var listener in listeners)
        {
            try
            {
                var valueTask = listener.DisposeAsync();
                if (!valueTask.IsCompleted) valueTask.AsTask().GetAwaiter().GetResult();
            }
#pragma warning disable CA1031 // Do not catch general exception types - disposal exceptions must not propagate during cleanup
            catch
#pragma warning restore CA1031
            {
                // Swallow exceptions during disposal to ensure all listeners are disposed
            }
        }
    }
}
