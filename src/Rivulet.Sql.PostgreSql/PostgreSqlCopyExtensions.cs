using Npgsql;
using Rivulet.Core;

namespace Rivulet.Sql.PostgreSql;

/// <summary>
/// Extension methods for PostgreSQL-specific bulk operations using COPY command.
/// Provides 10-100x performance improvement over standard batched inserts.
/// </summary>
public static class PostgreSqlCopyExtensions
{
    /// <summary>
    /// Performs parallel bulk insert operations using PostgreSQL COPY command for maximum performance.
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
    public static async Task BulkInsertUsingCopyAsync<T>(
        this IEnumerable<T> source,
        Func<NpgsqlConnection> connectionFactory,
        string tableName,
        string[] columns,
        Func<T, object?[]> mapToRow,
        ParallelOptionsRivulet? options = null,
        int batchSize = 5000,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(mapToRow);

        if (columns.Length == 0)
            throw new ArgumentException("Columns array cannot be empty", nameof(columns));
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than 0");

        options ??= new();

        // Escape table name to prevent SQL injection
        var escapedTableName = $"\"{tableName.Replace("\"", "\"\"")}\"";
        var columnList = string.Join(", ", columns.Select(c => $"\"{c.Replace("\"", "\"\"")}\""));
        var copyCommand = $"COPY {escapedTableName} ({columnList}) FROM STDIN (FORMAT BINARY)";

        await source
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

                    var connection = connectionFactory();
                    if (connection == null)
                        throw new InvalidOperationException("Connection factory returned null");

                    await using (connection)
                    {
                        await connection.OpenAsync(ct).ConfigureAwait(false);

                        await using var writer = await connection.BeginBinaryImportAsync(copyCommand, ct)
                            .ConfigureAwait(false);

                        foreach (var (_, rowData) in validatedRows)
                        {
                            await writer.StartRowAsync(ct).ConfigureAwait(false);
                            foreach (var value in rowData)
                            {
                                await writer.WriteAsync(value, ct).ConfigureAwait(false);
                            }
                        }

                        await writer.CompleteAsync(ct).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    var detailMessage = $"[PostgreSQL COPY] Failed to bulk insert batch of {batch.Length} rows to table '{tableName}'. " +
                        $"Copy command: {copyCommand}. " +
                        $"Exception: {ex.GetType().FullName} - {ex.Message}";
                    if (ex.InnerException != null)
                    {
                        detailMessage += $" | InnerException: {ex.InnerException.GetType().FullName} - {ex.InnerException.Message}";
                    }
                    if (ex.StackTrace != null)
                    {
                        detailMessage += $" | StackTrace: {ex.StackTrace[..Math.Min(500, ex.StackTrace.Length)]}";
                    }
                    throw new InvalidOperationException(detailMessage, ex);
                }
            }, options, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Performs parallel bulk insert operations using PostgreSQL COPY command with CSV format.
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
    public static async Task BulkInsertUsingCopyCsvAsync(
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
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentNullException.ThrowIfNull(columns);

        if (columns.Length == 0)
            throw new ArgumentException("Columns array cannot be empty", nameof(columns));
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than 0");

        options ??= new();

        // Escape table name and columns to prevent SQL injection
        var escapedTableName = $"\"{tableName.Replace("\"", "\"\"")}\"";
        var columnList = string.Join(", ", columns.Select(c => $"\"{c.Replace("\"", "\"\"")}\""));

        // Escape delimiter (handle single quotes)
        var escapedDelimiter = delimiter.ToString().Replace("'", "''");
        var headerOption = hasHeader ? ", HEADER" : "";
        var copyCommand = $"COPY {escapedTableName} ({columnList}) FROM STDIN (FORMAT CSV, DELIMITER '{escapedDelimiter}'{headerOption})";

        await source
            .Chunk(batchSize)
            .ForEachParallelAsync(async (batch, ct) =>
            {
                var connection = connectionFactory();
                if (connection == null)
                    throw new InvalidOperationException("Connection factory returned null");

                await using (connection)
                {
                    await connection.OpenAsync(ct).ConfigureAwait(false);

                    try
                    {
                        await using var writer = await connection.BeginTextImportAsync(copyCommand, ct)
                            .ConfigureAwait(false);
                        foreach (var line in batch)
                        {
                            await writer.WriteLineAsync(line).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"Failed to bulk insert CSV batch of {batch.Length} lines to table '{tableName}'", ex);
                    }
                }
            }, options, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Performs parallel bulk insert operations using PostgreSQL COPY command with raw text format.
    /// </summary>
    /// <param name="source">Source collection of tab-delimited text lines</param>
    /// <param name="connectionFactory">Factory to create PostgreSQL connections</param>
    /// <param name="tableName">Target table name</param>
    /// <param name="columns">Column names to insert</param>
    /// <param name="options">Parallel execution options</param>
    /// <param name="batchSize">Number of rows per COPY batch (defaults to 5000)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public static async Task BulkInsertUsingCopyTextAsync(
        this IEnumerable<string> source,
        Func<NpgsqlConnection> connectionFactory,
        string tableName,
        string[] columns,
        ParallelOptionsRivulet? options = null,
        int batchSize = 5000,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentNullException.ThrowIfNull(columns);

        if (columns.Length == 0)
            throw new ArgumentException("Columns array cannot be empty", nameof(columns));
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than 0");

        options ??= new();

        // Escape table name and columns to prevent SQL injection
        var escapedTableName = $"\"{tableName.Replace("\"", "\"\"")}\"";
        var columnList = string.Join(", ", columns.Select(c => $"\"{c.Replace("\"", "\"\"")}\""));
        var copyCommand = $"COPY {escapedTableName} ({columnList}) FROM STDIN";

        await source
            .Chunk(batchSize)
            .ForEachParallelAsync(async (batch, ct) =>
            {
                var connection = connectionFactory();
                if (connection == null)
                    throw new InvalidOperationException("Connection factory returned null");

                await using (connection)
                {
                    await connection.OpenAsync(ct).ConfigureAwait(false);

                    try
                    {
                        await using var writer = await connection.BeginTextImportAsync(copyCommand, ct)
                            .ConfigureAwait(false);
                        foreach (var line in batch)
                        {
                            await writer.WriteLineAsync(line).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"Failed to bulk insert text batch of {batch.Length} lines to table '{tableName}'", ex);
                    }
                }
            }, options, cancellationToken)
            .ConfigureAwait(false);
    }
}
