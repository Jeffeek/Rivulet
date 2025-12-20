using Npgsql;
using Rivulet.Core;
using Rivulet.Sql.Internal;

namespace Rivulet.Sql.PostgreSql;

/// <summary>
///     Extension methods for PostgreSQL-specific bulk operations using COPY command.
///     Provides 10-100x performance improvement over standard batched inserts.
/// </summary>
public static class PostgreSqlCopyExtensions
{
    /// <summary>
    ///     Performs parallel bulk insert operations using PostgreSQL COPY command for maximum performance.
    /// </summary>
    /// <typeparam name="T">The type of items to insert</typeparam>
    /// <param name="source">Source collection of items</param>
    /// <param name="connectionFactory">Factory to create PostgreSQL connections</param>
    /// <param name="tableName">Target table name</param>
    /// <param name="columns">Column names to insert</param>
    /// <param name="mapToRow">Function to map item to row data</param>
    /// <param name="options">Parallel execution options</param>
    /// <param name="batchSize">Number of rows per COPY batch (defaults to 5000)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public static Task BulkInsertUsingCopyAsync<T>(
        this IEnumerable<T> source,
        Func<NpgsqlConnection> connectionFactory,
        string tableName,
        string[] columns,
        Func<T, object?[]> mapToRow,
        ParallelOptionsRivulet? options = null,
        int batchSize = 5000,
        CancellationToken cancellationToken = default)
    {
        // ReSharper disable once PossibleMultipleEnumeration
        SqlValidationHelper.ValidateCommonBulkParameters(source, connectionFactory, tableName, batchSize);
        SqlValidationHelper.ValidateArrayNotEmpty(columns, nameof(columns));
        ArgumentNullException.ThrowIfNull(mapToRow);

        options ??= new();

        // Escape table name and columns to prevent SQL injection
        var (escapedTableName, columnList) = EscapeTableAndColumns(tableName, columns);
        var copyCommand = $"COPY {escapedTableName} ({columnList}) FROM STDIN (FORMAT BINARY)";

        // ReSharper disable once PossibleMultipleEnumeration
        return source
            .Chunk(batchSize)
            .ForEachParallelAsync(async (batch, ct) =>
                {
                    try
                    {
                        // Validate all rows before starting write operation
                        var validatedRows = new List<(T item, object?[] rowData)>(batch.Length);
                        foreach (var item in batch)
                        {
                            var rowData = mapToRow(item);
                            if (rowData.Length != columns.Length)
                            {
                                throw new InvalidOperationException(
                                    $"Row data length ({rowData.Length}) does not match columns length ({columns.Length})");
                            }

                            validatedRows.Add((item, rowData));
                        }

                        var connection = SqlConnectionHelper.CreateAndValidate(connectionFactory);

                        await using (connection)
                        {
                            await SqlConnectionHelper.OpenConnectionAsync(connection, ct).ConfigureAwait(false);

#pragma warning disable CA2007 // ConfigureAwait not applicable to await using declarations
                            await using var writer = await connection.BeginBinaryImportAsync(copyCommand, ct);
#pragma warning restore CA2007

                            foreach (var (_, rowData) in validatedRows)
                            {
                                await writer.StartRowAsync(ct).ConfigureAwait(false);
                                foreach (var value in rowData) await writer.WriteAsync(value, ct).ConfigureAwait(false);
                            }

                            await writer.CompleteAsync(ct).ConfigureAwait(false);
                        }
                    }
#pragma warning disable CA1031 // Do not catch general exception types
                    catch (Exception ex)
#pragma warning restore CA1031
                    {
                        throw SqlErrorHelper.WrapBulkOperationExceptionWithDetails(
                            ex,
                            "PostgreSQL COPY",
                            batch.Length,
                            tableName,
                            copyCommand);
                    }
                },
                options,
                cancellationToken);
    }

    /// <summary>
    ///     Performs parallel bulk insert operations using PostgreSQL COPY command with CSV format.
    /// </summary>
    /// <param name="source">Source collection of CSV lines</param>
    /// <param name="connectionFactory">Factory to create PostgreSQL connections</param>
    /// <param name="tableName">Target table name</param>
    /// <param name="columns">Column names to insert</param>
    /// <param name="options">Parallel execution options</param>
    /// <param name="batchSize">Number of rows per COPY batch (defaults to 5000)</param>
    /// <param name="hasHeader">Whether the CSV has a header row (defaults to false)</param>
    /// <param name="delimiter">CSV delimiter (defaults to comma)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public static Task BulkInsertUsingCopyCsvAsync(
        this IEnumerable<string> source,
        Func<NpgsqlConnection> connectionFactory,
        string tableName,
        string[] columns,
        ParallelOptionsRivulet? options = null,
        int batchSize = 5000,
        bool hasHeader = false,
        char delimiter = ',',
        CancellationToken cancellationToken = default)
    {
        // ReSharper disable once PossibleMultipleEnumeration
        SqlValidationHelper.ValidateCommonBulkParameters(source, connectionFactory, tableName, batchSize);
        SqlValidationHelper.ValidateArrayNotEmpty(columns, nameof(columns));

        options ??= new();

        // Escape table name and columns to prevent SQL injection
        var (escapedTableName, columnList) = EscapeTableAndColumns(tableName, columns);

        // Escape delimiter (handle single quotes)
        var escapedDelimiter = delimiter.ToString().Replace("'", "''");
        var headerOption = hasHeader ? ", HEADER" : "";
        var copyCommand =
            $"COPY {escapedTableName} ({columnList}) FROM STDIN (FORMAT CSV, DELIMITER '{escapedDelimiter}'{headerOption})";

        // ReSharper disable once PossibleMultipleEnumeration
        return source
            .Chunk(batchSize)
            .ForEachParallelAsync(async (batch, ct) =>
                {
                    var connection = SqlConnectionHelper.CreateAndValidate(connectionFactory);

                    await using (connection)
                    {
                        await SqlConnectionHelper.OpenConnectionAsync(connection, ct).ConfigureAwait(false);

                        try
                        {
#pragma warning disable CA2007 // ConfigureAwait not applicable to await using declarations
                            await using var writer = await connection.BeginTextImportAsync(copyCommand, ct);
#pragma warning restore CA2007
                            foreach (var line in batch) await writer.WriteLineAsync(line).ConfigureAwait(false);
                        }
#pragma warning disable CA1031 // Do not catch general exception types
                        catch (Exception ex)
#pragma warning restore CA1031
                        {
                            throw SqlErrorHelper.WrapBulkOperationException(
                                ex,
                                "bulk insert CSV",
                                batch.Length,
                                tableName);
                        }
                    }
                },
                options,
                cancellationToken);
    }

    /// <summary>
    ///     Performs parallel bulk insert operations using PostgreSQL COPY command with raw text format.
    /// </summary>
    /// <param name="source">Source collection of tab-delimited text lines</param>
    /// <param name="connectionFactory">Factory to create PostgreSQL connections</param>
    /// <param name="tableName">Target table name</param>
    /// <param name="columns">Column names to insert</param>
    /// <param name="options">Parallel execution options</param>
    /// <param name="batchSize">Number of rows per COPY batch (defaults to 5000)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public static Task BulkInsertUsingCopyTextAsync(
        this IEnumerable<string> source,
        Func<NpgsqlConnection> connectionFactory,
        string tableName,
        string[] columns,
        ParallelOptionsRivulet? options = null,
        int batchSize = 5000,
        CancellationToken cancellationToken = default)
    {
        // ReSharper disable once PossibleMultipleEnumeration
        SqlValidationHelper.ValidateCommonBulkParameters(source, connectionFactory, tableName, batchSize);
        SqlValidationHelper.ValidateArrayNotEmpty(columns, nameof(columns));

        options ??= new();

        // Escape table name and columns to prevent SQL injection
        var (escapedTableName, columnList) = EscapeTableAndColumns(tableName, columns);
        var copyCommand = $"COPY {escapedTableName} ({columnList}) FROM STDIN";

        // ReSharper disable once PossibleMultipleEnumeration
        return source
            .Chunk(batchSize)
            .ForEachParallelAsync(async (batch, ct) =>
                {
                    var connection = SqlConnectionHelper.CreateAndValidate(connectionFactory);

                    await using (connection)
                    {
                        await SqlConnectionHelper.OpenConnectionAsync(connection, ct).ConfigureAwait(false);

                        try
                        {
#pragma warning disable CA2007 // ConfigureAwait not applicable to await using declarations
                            await using var writer = await connection.BeginTextImportAsync(copyCommand, ct);
#pragma warning restore CA2007
                            foreach (var line in batch) await writer.WriteLineAsync(line).ConfigureAwait(false);
                        }
#pragma warning disable CA1031 // Do not catch general exception types
                        catch (Exception ex)
#pragma warning restore CA1031
                        {
                            throw SqlErrorHelper.WrapBulkOperationException(
                                ex,
                                "bulk insert text",
                                batch.Length,
                                tableName);
                        }
                    }
                },
                options,
                cancellationToken);
    }

    /// <summary>
    ///     Escapes PostgreSQL identifier (table or column name) to prevent SQL injection.
    /// </summary>
    private static string EscapeIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"")}\"";

    /// <summary>
    ///     Builds escaped column list for COPY command.
    /// </summary>
    private static string BuildColumnList(IEnumerable<string> columns) =>
        string.Join(", ", columns.Select(EscapeIdentifier));

    /// <summary>
    ///     Builds COPY command with escaped table and column names.
    /// </summary>
    private static (string escapedTableName, string columnList) EscapeTableAndColumns(
        string tableName,
        IEnumerable<string> columns)
    {
        var escapedTableName = EscapeIdentifier(tableName);
        var columnList = BuildColumnList(columns);
        return (escapedTableName, columnList);
    }
}