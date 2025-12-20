namespace Rivulet.Diagnostics.Internal;

/// <summary>
///     Helper methods for file operations with retry logic.
/// </summary>
internal static class FileOperationRetryHelper
{
    /// <summary>
    ///     Executes a file operation with retry logic for transient IOException failures.
    /// </summary>
    /// <param name="operation">The file operation to execute.</param>
    /// <param name="retries">Number of retry attempts (default: 3).</param>
    /// <param name="delayMilliseconds">Delay between retries in milliseconds (default: 10ms).</param>
    /// <remarks>
    ///     This is useful for file operations that may fail due to the OS still holding
    ///     file handles (e.g., after closing a StreamWriter). The brief delay allows the
    ///     OS to release the handle before retrying.
    /// </remarks>
    public static void ExecuteWithRetry(Action operation, int retries = 3, int delayMilliseconds = 10)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (retries < 1)
            throw new ArgumentOutOfRangeException(nameof(retries), "Retries must be at least 1");

        if (delayMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(delayMilliseconds), "Delay must be non-negative");

        for (var i = 0; i < retries; i++)
        {
            try
            {
                operation();
                return;
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (IOException) when (i < retries - 1)
#pragma warning restore CA1031
            {
                // Brief delay to allow OS to release file handle
                Thread.Sleep(delayMilliseconds);
            }
        }
    }
}
