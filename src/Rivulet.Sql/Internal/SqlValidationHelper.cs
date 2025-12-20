using System.Data;

namespace Rivulet.Sql.Internal;

/// <summary>
///     Internal helper to reduce code duplication in SQL validation operations.
/// </summary>
internal static class SqlValidationHelper
{
    /// <summary>
    ///     Validates batch size is greater than 0.
    /// </summary>
    internal static void ValidateBatchSize(int batchSize)
    {
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than 0");
    }

    /// <summary>
    ///     Validates timeout is non-negative.
    /// </summary>
    internal static void ValidateTimeout(int timeout, string paramName)
    {
        if (timeout < 0)
            throw new ArgumentOutOfRangeException(paramName, "Timeout must be non-negative");
    }

    /// <summary>
    ///     Validates array is not null or empty.
    /// </summary>
    internal static void ValidateArrayNotEmpty<T>(T[] array, string paramName)
    {
        ArgumentNullException.ThrowIfNull(array, paramName);
        if (array.Length == 0)
            throw new ArgumentException($"{paramName} array cannot be empty", paramName);
    }

    /// <summary>
    ///     Validates string parameter is not null, empty, or whitespace.
    /// </summary>
    internal static void ValidateStringNotEmpty(string value, string paramName)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException($"{paramName} cannot be empty", paramName);
    }

    /// <summary>
    ///     Validates common bulk operation parameters.
    /// </summary>
    internal static void ValidateCommonBulkParameters<TItem, TConnection>(
        IEnumerable<TItem> source,
        Func<TConnection> connectionFactory,
        string tableName,
        int batchSize)
        where TConnection : class, IDbConnection
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ValidateBatchSize(batchSize);
    }
}
