using MySqlConnector;
using Rivulet.Core;

namespace Rivulet.Sql.MySql;

/// <summary>
///     Extension methods for MySQL-specific bulk operations using MySqlBulkCopy and MySqlBulkLoader.
///     Provides 10-100x performance improvement over standard batched inserts.
/// </summary>
public static class MySqlBulkExtensions
{
    /// <summary>
    ///     Performs parallel bulk insert operations using MySqlBulkLoader (LOAD DATA LOCAL INFILE) from CSV data.
    /// </summary>
    /// <param name="source">Source collection of CSV lines</param>
    /// <param name="connectionFactory">Factory to create MySQL connections</param>
    /// <param name="tableName">Target table name</param>
    /// <param name="columnNames">Column names to insert</param>
    /// <param name="options">Parallel execution options</param>
    /// <param name="batchSize">Number of rows per batch (defaults to 5000)</param>
    /// <param name="fieldSeparator">Field separator (defaults to comma)</param>
    /// <param name="lineTerminator">Line terminator (defaults to \n)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    /// <remarks>
    ///     SECURITY WARNING: This method writes data to temporary files in the system temp directory.
    ///     These files may contain sensitive data and could be accessible to other processes or users
    ///     with access to the temp directory. Files are deleted after use, but data may persist on disk
    ///     until overwritten. Consider encryption or memory-based alternatives for sensitive data.
    /// </remarks>
    public static Task BulkInsertUsingMySqlBulkLoaderAsync(
        this IEnumerable<string> source,
        Func<MySqlConnection> connectionFactory,
        string tableName,
        string[] columnNames,
        ParallelOptionsRivulet? options = null,
        int batchSize = 5000,
        string fieldSeparator = ",",
        string lineTerminator = "\n",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentNullException.ThrowIfNull(columnNames);
        ArgumentNullException.ThrowIfNull(fieldSeparator);
        ArgumentNullException.ThrowIfNull(lineTerminator);

        if (columnNames.Length == 0) throw new ArgumentException("Column names array cannot be empty", nameof(columnNames));

        if (batchSize <= 0) throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than 0");

        if (string.IsNullOrEmpty(fieldSeparator)) throw new ArgumentException("Field separator cannot be empty", nameof(fieldSeparator));

        if (string.IsNullOrEmpty(lineTerminator)) throw new ArgumentException("Line terminator cannot be empty", nameof(lineTerminator));

        options ??= new();

        return source
            .Chunk(batchSize)
            .ForEachParallelAsync(async (batch, ct) =>
                {
                    // Write batch to temporary file
                    var tempFile = Path.GetTempFileName();
                    try
                    {
                        await File.WriteAllLinesAsync(tempFile, batch, ct).ConfigureAwait(false);

                        var connection = connectionFactory();
                        if (connection == null) throw new InvalidOperationException("Connection factory returned null");

                        await using (connection)
                        {
                            await connection.OpenAsync(ct).ConfigureAwait(false);

                            var bulkLoader = new MySqlBulkLoader(connection)
                            {
                                TableName = tableName,
                                FileName = tempFile,
                                FieldTerminator = fieldSeparator,
                                LineTerminator = lineTerminator,
                                Local = true
                            };

                            foreach (var columnName in columnNames) bulkLoader.Columns.Add(columnName);

                            try
                            {
                                await bulkLoader.LoadAsync(ct).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                throw new InvalidOperationException(
                                    $"Failed to bulk load batch of {batch.Length} rows to table '{tableName}'",
                                    ex);
                            }
                        }
                    }
                    finally
                    {
                        // Prevent exception masking during cleanup
                        try
                        {
                            if (File.Exists(tempFile)) File.Delete(tempFile);
                        }
                        catch (Exception)
                        {
                            // Suppress cleanup exceptions to avoid masking the original exception
                            // File will be cleaned up eventually by OS temp file cleanup
                        }
                    }
                },
                options,
                cancellationToken);
    }

    /// <summary>
    ///     Performs parallel bulk insert operations using MySqlBulkLoader from file paths.
    /// </summary>
    /// <param name="source">Source collection of file paths</param>
    /// <param name="connectionFactory">Factory to create MySQL connections</param>
    /// <param name="tableName">Target table name</param>
    /// <param name="columnNames">Column names to insert</param>
    /// <param name="options">Parallel execution options</param>
    /// <param name="fieldSeparator">Field separator (defaults to comma)</param>
    /// <param name="lineTerminator">Line terminator (defaults to \n)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public static Task BulkInsertFromFilesUsingMySqlBulkLoaderAsync(
        this IEnumerable<string> source,
        Func<MySqlConnection> connectionFactory,
        string tableName,
        string[] columnNames,
        ParallelOptionsRivulet? options = null,
        string fieldSeparator = ",",
        string lineTerminator = "\n",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentNullException.ThrowIfNull(columnNames);
        ArgumentNullException.ThrowIfNull(fieldSeparator);
        ArgumentNullException.ThrowIfNull(lineTerminator);

        if (columnNames.Length == 0) throw new ArgumentException("Column names array cannot be empty", nameof(columnNames));

        if (string.IsNullOrEmpty(fieldSeparator)) throw new ArgumentException("Field separator cannot be empty", nameof(fieldSeparator));

        if (string.IsNullOrEmpty(lineTerminator)) throw new ArgumentException("Line terminator cannot be empty", nameof(lineTerminator));

        options ??= new();

        return source
            .ForEachParallelAsync(async (filePath, ct) =>
                {
                    var connection = connectionFactory();
                    if (connection == null) throw new InvalidOperationException("Connection factory returned null");

                    await using (connection)
                    {
                        await connection.OpenAsync(ct).ConfigureAwait(false);

                        var bulkLoader = new MySqlBulkLoader(connection)
                        {
                            TableName = tableName,
                            FileName = filePath,
                            FieldTerminator = fieldSeparator,
                            LineTerminator = lineTerminator,
                            Local = true
                        };

                        foreach (var columnName in columnNames) bulkLoader.Columns.Add(columnName);

                        try
                        {
                            await bulkLoader.LoadAsync(ct).ConfigureAwait(false);
                        }
                        catch (FileNotFoundException)
                        {
                            // Re-throw FileNotFoundException with original detail
                            throw;
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException(
                                $"Failed to bulk load file '{filePath}' to table '{tableName}'",
                                ex);
                        }
                    }
                },
                options,
                cancellationToken);
    }
}