using MySqlConnector;
using Rivulet.Core;
using Rivulet.Sql.Internal;

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
        CancellationToken cancellationToken = default
    )
    {
        // ReSharper disable once PossibleMultipleEnumeration
        SqlValidationHelper.ValidateCommonBulkParameters(source, connectionFactory, tableName, batchSize);
        SqlValidationHelper.ValidateArrayNotEmpty(columnNames, nameof(columnNames));
        ArgumentNullException.ThrowIfNull(fieldSeparator);
        ArgumentNullException.ThrowIfNull(lineTerminator);
        SqlValidationHelper.ValidateStringNotEmpty(fieldSeparator, nameof(fieldSeparator));
        SqlValidationHelper.ValidateStringNotEmpty(lineTerminator, nameof(lineTerminator));

        options ??= new();

        // ReSharper disable once PossibleMultipleEnumeration
        return source
            .Chunk(batchSize)
            .ForEachParallelAsync(async (batch, ct) =>
                {
                    // Write batch to temporary file
                    var tempFile = Path.GetTempFileName();
                    try
                    {
                        await File.WriteAllLinesAsync(tempFile, batch, ct).ConfigureAwait(false);

                        var connection = SqlConnectionHelper.CreateAndValidate(connectionFactory);

                        await using (connection)
                        {
                            await SqlConnectionHelper.OpenConnectionAsync(connection, ct).ConfigureAwait(false);

                            var bulkLoader = CreateAndConfigureBulkLoader(
                                connection,
                                tableName,
                                tempFile,
                                columnNames,
                                fieldSeparator,
                                lineTerminator);

                            try
                            {
                                await bulkLoader.LoadAsync(ct).ConfigureAwait(false);
                            }
#pragma warning disable CA1031 // Do not catch general exception types - wrapping all MySQL exceptions with operation context
                            catch (Exception ex)
#pragma warning restore CA1031
                            {
                                throw SqlErrorHelper.WrapBulkOperationException(
                                    ex,
                                    "bulk load",
                                    batch.Length,
                                    tableName);
                            }
                        }
                    }
                    finally
                    {
                        CleanupTempFile(tempFile);
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
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        SqlValidationHelper.ValidateArrayNotEmpty(columnNames, nameof(columnNames));
        ArgumentNullException.ThrowIfNull(fieldSeparator);
        ArgumentNullException.ThrowIfNull(lineTerminator);
        SqlValidationHelper.ValidateStringNotEmpty(fieldSeparator, nameof(fieldSeparator));
        SqlValidationHelper.ValidateStringNotEmpty(lineTerminator, nameof(lineTerminator));

        options ??= new();

        return source
            .ForEachParallelAsync(async (filePath, ct) =>
                {
                    var connection = SqlConnectionHelper.CreateAndValidate(connectionFactory);

                    await using (connection)
                    {
                        await SqlConnectionHelper.OpenConnectionAsync(connection, ct).ConfigureAwait(false);

                        var bulkLoader = CreateAndConfigureBulkLoader(
                            connection,
                            tableName,
                            filePath,
                            columnNames,
                            fieldSeparator,
                            lineTerminator);

                        try
                        {
                            await bulkLoader.LoadAsync(ct).ConfigureAwait(false);
                        }
                        catch (FileNotFoundException)
                        {
                            // Re-throw FileNotFoundException with original detail
                            throw;
                        }
#pragma warning disable CA1031 // Do not catch general exception types - wrapping all MySQL exceptions with operation context
                        catch (Exception ex)
#pragma warning restore CA1031
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

    /// <summary>
    ///     Creates and configures a MySqlBulkLoader instance.
    /// </summary>
    private static MySqlBulkLoader CreateAndConfigureBulkLoader(
        MySqlConnection connection,
        string tableName,
        string fileName,
        IEnumerable<string> columnNames,
        string fieldSeparator,
        string lineTerminator
    )
    {
        var bulkLoader = new MySqlBulkLoader(connection)
        {
            TableName = tableName,
            FileName = fileName,
            FieldTerminator = fieldSeparator,
            LineTerminator = lineTerminator,
            Local = true
        };

        foreach (var columnName in columnNames)
            bulkLoader.Columns.Add(columnName);

        return bulkLoader;
    }

    /// <summary>
    ///     Cleans up temporary file with suppressed exceptions.
    /// </summary>
    private static void CleanupTempFile(string tempFile)
    {
        try
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
#pragma warning disable CA1031 // Do not catch general exception types - suppressing cleanup exceptions to avoid masking original errors
        catch (Exception)
#pragma warning restore CA1031
        {
            // Suppress cleanup exceptions to avoid masking the original exception
            // File will be cleaned up eventually by OS temp file cleanup
        }
    }
}
