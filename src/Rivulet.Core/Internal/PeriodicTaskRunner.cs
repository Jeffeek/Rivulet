namespace Rivulet.Core.Internal;

/// <summary>
///     Helper for running periodic async tasks with optional final execution on cancellation.
/// </summary>
internal static class PeriodicTaskRunner
{
    /// <summary>
    ///     Runs a task periodically until cancellation, with optional final execution.
    /// </summary>
    /// <param name="work">The work to execute periodically.</param>
    /// <param name="interval">The interval between executions.</param>
    /// <param name="cancellationToken">Cancellation token to stop the periodic execution.</param>
    /// <param name="finalWork">Optional work to execute once after cancellation.</param>
    /// <returns>A task that completes when the periodic execution is cancelled.</returns>
    internal static async Task RunPeriodicAsync(
        Func<ValueTask> work,
        TimeSpan interval,
        CancellationToken cancellationToken,
        Func<ValueTask>? finalWork = null
    )
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
                await work().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancellation is expected
        }

        // Always execute final work on exit, regardless of whether the loop ended
        // via cancellation (OCE from Task.Delay) or natural exit (token checked in while condition).
        // This ensures the final snapshot captures all state changes (e.g., error counts)
        // that occurred before disposal.
        if (finalWork is not null)
            await finalWork().ConfigureAwait(false);
    }
}
