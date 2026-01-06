using System.Diagnostics;

namespace Rivulet.Core.Internal;

/// <summary>
///     Helper methods for disposing periodic tasks and background workers.
/// </summary>
internal static class DisposalHelper
{
    /// <summary>
    ///     Disposes a periodic task with cancellation, graceful shutdown, and cleanup.
    /// </summary>
    /// <param name="cts">The cancellation token source to cancel.</param>
    /// <param name="task">The background task to wait for.</param>
    /// <param name="waitTimeout">Maximum time to wait for the task to complete.</param>
    /// <param name="stopwatch">Optional stopwatch to stop.</param>
    /// <param name="finalWork">Optional final work to execute before cleanup.</param>
    /// <returns>A task that completes when disposal is finished.</returns>
    internal static async ValueTask DisposePeriodicTaskAsync(
        CancellationTokenSource cts,
        Task task,
        TimeSpan waitTimeout,
        Stopwatch? stopwatch = null,
        Func<ValueTask>? finalWork = null
    )
    {
        // Cancel the background task
        await cts.CancelAsync().ConfigureAwait(false);

        // Wait for task completion with timeout
        try
        {
            await task.WaitAsync(waitTimeout).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is AggregateException or OperationCanceledException or TimeoutException)
        {
            // Expected during disposal - task was cancelled or timed out
        }

        // Execute any final work
        if (finalWork is not null)
            await finalWork().ConfigureAwait(false);

        // Cleanup resources
        cts.Dispose();
        stopwatch?.Stop();
    }
}
