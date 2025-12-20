using Microsoft.Extensions.Logging;

namespace Rivulet.Hosting.Internal;

/// <summary>
///     Helper methods for background service execution with standardized error handling.
/// </summary>
internal static class BackgroundServiceExecutionHelper
{
    /// <summary>
    ///     Executes background service work with standardized error handling and logging.
    /// </summary>
    /// <param name="work">The async work to execute.</param>
    /// <param name="serviceName">Name of the service for logging context.</param>
    /// <param name="logger">Logger instance for error and cancellation logging.</param>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown.</param>
    /// <returns>A task representing the async execution.</returns>
    /// <remarks>
    ///     This method provides consistent error handling across background services:
    ///     - OperationCanceledException is caught and logged as informational (expected shutdown)
    ///     - All other exceptions are logged as errors and re-thrown for host handling
    /// </remarks>
    internal static async Task ExecuteWithErrorHandlingAsync(
        Func<CancellationToken, Task> work,
        string serviceName,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            await work(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("{ServiceName} is stopping", serviceName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception in {ServiceName}", serviceName);
            throw;
        }
    }
}
