using System.Data;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.SqlClient;
using Rivulet.Core;
using Rivulet.Sql.Internal;

namespace Rivulet.Sql.SqlServer;

/// <summary>
///     Extension methods for SQL Server-specific bulk operations using SqlBulkCopy.
///     Provides 10-100x performance improvement over standard batched inserts.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
public static class SqlBulkCopyExtensions
{
    /// <summary>
    ///     Performs parallel bulk insert operations using SqlBulkCopy for maximum performance.
    /// </summary>
    /// <typeparam name="T">The type of items to insert</typeparam>
    /// <param name="source">Source collection of items</param>
    /// <param name="connectionFactory">Factory to create SQL Server connections</param>
    /// <param name="destinationTable">Target table name</param>
    /// <param name="mapToDataTable">Function to convert items to DataTable</param>
    /// <param name="options">Parallel execution options</param>
    /// <param name="bulkCopyOptions">SqlBulkCopy options (defaults to Default)</param>
    /// <param name="batchSize">Number of rows per SqlBulkCopy batch (defaults to 5000)</param>
    /// <param name="bulkCopyTimeout">Timeout in seconds for SqlBulkCopy operations (defaults to 30)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public static Task BulkInsertUsingSqlBulkCopyAsync<T>(
        this IEnumerable<T> source,
        Func<SqlConnection> connectionFactory,
        string destinationTable,
        Func<IEnumerable<T>, DataTable> mapToDataTable,
        ParallelOptionsRivulet? options = null,
        SqlBulkCopyOptions bulkCopyOptions = SqlBulkCopyOptions.Default,
        int batchSize = 5000,
        int bulkCopyTimeout = 30,
        CancellationToken cancellationToken = default
    )
    {
        // ReSharper disable once PossibleMultipleEnumeration
        SqlValidationHelper.ValidateCommonBulkParameters(source, connectionFactory, destinationTable, batchSize);
        ArgumentNullException.ThrowIfNull(mapToDataTable);
        SqlValidationHelper.ValidateTimeout(bulkCopyTimeout, nameof(bulkCopyTimeout));

        options ??= new();

        // ReSharper disable once PossibleMultipleEnumeration
        return source
            .Chunk(batchSize)
            .ForEachParallelAsync(async (batch, ct) =>
                {
                    using var dataTable = mapToDataTable(batch);

                    var connection = SqlConnectionHelper.CreateAndValidate(connectionFactory);

                    await using (connection)
                    {
                        await SqlConnectionHelper.OpenConnectionAsync(connection, ct).ConfigureAwait(false);

                        using var bulkCopy = CreateAndConfigureBulkCopy(
                            connection,
                            destinationTable,
                            bulkCopyOptions,
                            batchSize,
                            bulkCopyTimeout);

                        // Map columns automatically if not specified
                        if (bulkCopy.ColumnMappings.Count == 0)
                        {
                            foreach (DataColumn column in dataTable.Columns)
                                bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                        }

                        try
                        {
                            await bulkCopy.WriteToServerAsync(dataTable, ct).ConfigureAwait(false);
                        }
#pragma warning disable CA1031 // Do not catch general exception types
                        catch (Exception ex)
#pragma warning restore CA1031
                        {
                            throw SqlErrorHelper.WrapBulkOperationException(
                                ex,
                                "bulk insert",
                                batch.Length,
                                destinationTable);
                        }
                    }
                },
                options,
                cancellationToken);
    }

    /// <summary>
    ///     Performs parallel bulk insert operations using SqlBulkCopy with explicit column mappings.
    /// </summary>
    /// <typeparam name="T">The type of items to insert</typeparam>
    /// <param name="source">Source collection of items</param>
    /// <param name="connectionFactory">Factory to create SQL Server connections</param>
    /// <param name="destinationTable">Target table name</param>
    /// <param name="mapToDataTable">Function to convert items to DataTable</param>
    /// <param name="columnMappings">Dictionary of source column to destination column mappings</param>
    /// <param name="options">Parallel execution options</param>
    /// <param name="bulkCopyOptions">SqlBulkCopy options (defaults to Default)</param>
    /// <param name="batchSize">Number of rows per SqlBulkCopy batch (defaults to 5000)</param>
    /// <param name="bulkCopyTimeout">Timeout in seconds for SqlBulkCopy operations (defaults to 30)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public static Task BulkInsertUsingSqlBulkCopyAsync<T>(
        this IEnumerable<T> source,
        Func<SqlConnection> connectionFactory,
        string destinationTable,
        Func<IEnumerable<T>, DataTable> mapToDataTable,
        Dictionary<string, string> columnMappings,
        ParallelOptionsRivulet? options = null,
        SqlBulkCopyOptions bulkCopyOptions = SqlBulkCopyOptions.Default,
        int batchSize = 5000,
        int bulkCopyTimeout = 30,
        CancellationToken cancellationToken = default
    )
    {
        // ReSharper disable once PossibleMultipleEnumeration
        SqlValidationHelper.ValidateCommonBulkParameters(source, connectionFactory, destinationTable, batchSize);
        ArgumentNullException.ThrowIfNull(mapToDataTable);
        ArgumentNullException.ThrowIfNull(columnMappings);
        SqlValidationHelper.ValidateTimeout(bulkCopyTimeout, nameof(bulkCopyTimeout));

        if (columnMappings.Count == 0)
            throw new ArgumentException("Column mappings dictionary cannot be empty", nameof(columnMappings));

        options ??= new();

        // ReSharper disable once PossibleMultipleEnumeration
        return source
            .Chunk(batchSize)
            .ForEachParallelAsync(async (batch, ct) =>
                {
                    using var dataTable = mapToDataTable(batch);

                    var connection = SqlConnectionHelper.CreateAndValidate(connectionFactory);

                    await using (connection)
                    {
                        await SqlConnectionHelper.OpenConnectionAsync(connection, ct).ConfigureAwait(false);

                        using var bulkCopy = CreateAndConfigureBulkCopy(
                            connection,
                            destinationTable,
                            bulkCopyOptions,
                            batchSize,
                            bulkCopyTimeout);

                        // Apply explicit column mappings
                        foreach (var (sourceColumn, destColumn) in columnMappings)
                            bulkCopy.ColumnMappings.Add(sourceColumn, destColumn);

                        try
                        {
                            await bulkCopy.WriteToServerAsync(dataTable, ct).ConfigureAwait(false);
                        }
#pragma warning disable CA1031 // Do not catch general exception types
                        catch (Exception ex)
#pragma warning restore CA1031
                        {
                            throw SqlErrorHelper.WrapBulkOperationException(
                                ex,
                                "bulk insert",
                                batch.Length,
                                destinationTable);
                        }
                    }
                },
                options,
                cancellationToken);
    }

    /// <summary>
    ///     Performs parallel bulk insert operations using SqlBulkCopy with a DataReader source.
    /// </summary>
    /// <param name="source">Source collection of DataReaders</param>
    /// <param name="connectionFactory">Factory to create SQL Server connections</param>
    /// <param name="destinationTable">Target table name</param>
    /// <param name="options">Parallel execution options</param>
    /// <param name="bulkCopyOptions">SqlBulkCopy options (defaults to Default)</param>
    /// <param name="batchSize">Number of rows per SqlBulkCopy batch (defaults to 5000)</param>
    /// <param name="bulkCopyTimeout">Timeout in seconds for SqlBulkCopy operations (defaults to 30)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    /// <remarks>
    ///     IMPORTANT: The caller is responsible for disposing DataReaders in the source collection.
    ///     This method does not take ownership of or dispose the DataReaders.
    /// </remarks>
    public static Task BulkInsertUsingSqlBulkCopyAsync(
        this IEnumerable<IDataReader> source,
        Func<SqlConnection> connectionFactory,
        string destinationTable,
        ParallelOptionsRivulet? options = null,
        SqlBulkCopyOptions bulkCopyOptions = SqlBulkCopyOptions.Default,
        int batchSize = 5000,
        int bulkCopyTimeout = 30,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationTable);
        SqlValidationHelper.ValidateBatchSize(batchSize);
        SqlValidationHelper.ValidateTimeout(bulkCopyTimeout, nameof(bulkCopyTimeout));

        options ??= new();

        return source
            .ForEachParallelAsync(async (reader, ct) =>
                {
                    if (reader == null)
                        throw new InvalidOperationException("Source collection contains a null DataReader");

                    var connection = SqlConnectionHelper.CreateAndValidate(connectionFactory);

                    await using (connection)
                    {
                        await SqlConnectionHelper.OpenConnectionAsync(connection, ct).ConfigureAwait(false);

                        using var bulkCopy = CreateAndConfigureBulkCopy(
                            connection,
                            destinationTable,
                            bulkCopyOptions,
                            batchSize,
                            bulkCopyTimeout);

                        try
                        {
                            await bulkCopy.WriteToServerAsync(reader, ct).ConfigureAwait(false);
                        }
#pragma warning disable CA1031 // Do not catch general exception types
                        catch (Exception ex)
#pragma warning restore CA1031
                        {
                            throw new InvalidOperationException(
                                $"Failed to bulk insert from DataReader to table '{destinationTable}'",
                                ex);
                        }
                    }
                },
                options,
                cancellationToken);
    }

    /// <summary>
    ///     Creates and configures a SqlBulkCopy instance with standard settings.
    /// </summary>
    private static SqlBulkCopy CreateAndConfigureBulkCopy(
        SqlConnection connection,
        string destinationTable,
        SqlBulkCopyOptions bulkCopyOptions,
        int batchSize,
        int bulkCopyTimeout
    )
    {
        var bulkCopy = new SqlBulkCopy(connection, bulkCopyOptions, null);
        bulkCopy.DestinationTableName = destinationTable;
        bulkCopy.BatchSize = batchSize;
        bulkCopy.BulkCopyTimeout = bulkCopyTimeout;
        return bulkCopy;
    }
}
