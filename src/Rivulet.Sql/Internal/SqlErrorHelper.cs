namespace Rivulet.Sql.Internal;

/// <summary>
///     Internal helper to reduce code duplication in SQL error handling operations.
/// </summary>
internal static class SqlErrorHelper
{
    /// <summary>
    ///     Wraps a provider-specific exception in InvalidOperationException with a standard message.
    /// </summary>
    internal static InvalidOperationException WrapBulkOperationException(
        Exception ex,
        string operation,
        int batchSize,
        string tableName
    ) => new($"Failed to {operation} batch of {batchSize} rows to table '{tableName}'", ex);

    /// <summary>
    ///     Creates a detailed error message for bulk operations with exception information.
    /// </summary>
    private static string CreateDetailedErrorMessage(
        Exception ex,
        string context,
        int batchSize,
        string tableName,
        string? command = null
    )
    {
        var message = $"[{context}] Failed to bulk insert batch of {batchSize} rows to table '{tableName}'.";

        if (!string.IsNullOrEmpty(command))
            message += $" Command: {command}.";

        message += $" Exception: {ex.GetType().FullName} - {ex.Message}";

        if (ex.InnerException != null)
            message += $" | InnerException: {ex.InnerException.GetType().FullName} - {ex.InnerException.Message}";

        if (ex.StackTrace != null)
            message += $" | StackTrace: {ex.StackTrace[..Math.Min(500, ex.StackTrace.Length)]}";

        return message;
    }

    /// <summary>
    ///     Wraps a provider-specific exception with detailed diagnostic information.
    /// </summary>
    internal static InvalidOperationException WrapBulkOperationExceptionWithDetails(
        Exception ex,
        string context,
        int batchSize,
        string tableName,
        string? command = null
    )
    {
        var detailMessage = CreateDetailedErrorMessage(ex, context, batchSize, tableName, command);
        return new InvalidOperationException(detailMessage, ex);
    }
}
