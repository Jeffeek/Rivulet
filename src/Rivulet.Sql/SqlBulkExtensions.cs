using System.Data;
using System.Diagnostics.CodeAnalysis;
using Rivulet.Core;
using Rivulet.Sql.Internal;

namespace Rivulet.Sql;

/// <summary>
///     Extension methods for bulk SQL operations (inserts, updates, deletes).
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
public static class SqlBulkExtensions
{
    /// <summary>
    ///     Executes bulk insert operations in parallel batches.
    ///     Items are grouped into batches and executed with bounded parallelism.
    /// </summary>
    /// <typeparam name="T">The type of items to insert.</typeparam>
    /// <param name="items">The items to insert.</param>
    /// <param name="connectionFactory">Factory function to create database connections.</param>
    /// <param name="commandBuilder">Function to build the insert command for a batch of items.</param>
    /// <param name="options">Bulk operation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Total number of rows affected across all batches.</returns>
    public static Task<int> BulkInsertAsync<T>(
        this IEnumerable<T> items,
        Func<IDbConnection> connectionFactory,
        Func<IReadOnlyList<T>, IDbCommand, CancellationToken, ValueTask> commandBuilder,
        BulkOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(commandBuilder);

        options ??= new();

        return ExecuteBulkOperationAsync(
            items,
            connectionFactory,
            commandBuilder,
            options,
            cancellationToken);
    }

    /// <summary>
    ///     Executes bulk update operations in parallel batches.
    /// </summary>
    /// <typeparam name="T">The type of items to update.</typeparam>
    /// <param name="items">The items to update.</param>
    /// <param name="connectionFactory">Factory function to create database connections.</param>
    /// <param name="commandBuilder">Function to build the update command for a batch of items.</param>
    /// <param name="options">Bulk operation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Total number of rows affected across all batches.</returns>
    public static Task<int> BulkUpdateAsync<T>(
        this IEnumerable<T> items,
        Func<IDbConnection> connectionFactory,
        Func<IReadOnlyList<T>, IDbCommand, CancellationToken, ValueTask> commandBuilder,
        BulkOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(commandBuilder);

        options ??= new();

        return ExecuteBulkOperationAsync(
            items,
            connectionFactory,
            commandBuilder,
            options,
            cancellationToken);
    }

    /// <summary>
    ///     Executes bulk delete operations in parallel batches.
    /// </summary>
    /// <typeparam name="T">The type of items to delete.</typeparam>
    /// <param name="items">The items to delete.</param>
    /// <param name="connectionFactory">Factory function to create database connections.</param>
    /// <param name="commandBuilder">Function to build the delete command for a batch of items.</param>
    /// <param name="options">Bulk operation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Total number of rows affected across all batches.</returns>
    public static Task<int> BulkDeleteAsync<T>(
        this IEnumerable<T> items,
        Func<IDbConnection> connectionFactory,
        Func<IReadOnlyList<T>, IDbCommand, CancellationToken, ValueTask> commandBuilder,
        BulkOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(commandBuilder);

        options ??= new();

        return ExecuteBulkOperationAsync(
            items,
            connectionFactory,
            commandBuilder,
            options,
            cancellationToken);
    }

    private static async Task<int> ExecuteBulkOperationAsync<T>(
        IEnumerable<T> items,
        Func<IDbConnection> connectionFactory,
        Func<IReadOnlyList<T>, IDbCommand, CancellationToken, ValueTask> commandBuilder,
        BulkOperationOptions options,
        CancellationToken cancellationToken)
    {
        var sqlOptions = options.SqlOptions ?? new SqlOptions();
        var parallelOptions = sqlOptions.GetMergedParallelOptions();

        var batches = items
            .Select(static (item, index) => (item, index))
            .GroupBy(x => x.index / options.BatchSize)
            .Select(static g => g.Select(static x => x.item).ToList())
            .ToList();

#pragma warning disable CA2007 // ConfigureAwait not applicable with 'await using' statements in ExecuteBatchAsync
        var batchResults = await batches
            .Select(static (batch, index) => (batch, index))
            .SelectParallelAsync((item, ct) => ExecuteBatchAsync(
                    item.batch,
                    connectionFactory,
                    commandBuilder,
                    options,
                    item.index,
                    ct),
                parallelOptions,
                cancellationToken)
            .ConfigureAwait(false);
#pragma warning restore CA2007

        return batchResults.Sum();
    }

    private static async ValueTask<int> ExecuteBatchAsync<T>(
        IReadOnlyList<T> batch,
        Func<IDbConnection> connectionFactory,
        Func<IReadOnlyList<T>, IDbCommand, CancellationToken, ValueTask> commandBuilder,
        BulkOperationOptions options,
        int batchNumber,
        CancellationToken cancellationToken)
    {
        if (options.OnBatchStartAsync != null)
            await options.OnBatchStartAsync(batch.Cast<object>().ToList(), batchNumber).ConfigureAwait(false);

        var connection = SqlConnectionHelper.CreateAndValidate(connectionFactory);
        var sqlOptions = options.SqlOptions ?? new SqlOptions();

        using (connection)
        {
            try
            {
                await SqlConnectionHelper.OpenConnectionIfNeededAsync(connection, sqlOptions, cancellationToken)
                    .ConfigureAwait(false);

                var transaction = options.UseTransaction
                    ? connection.BeginTransaction(sqlOptions.IsolationLevel)
                    : null;

                using (transaction)
                {
                    try
                    {
                        using var command = connection.CreateCommand();
                        command.CommandTimeout = sqlOptions.CommandTimeout;

                        if (transaction != null) command.Transaction = transaction;

                        await commandBuilder(batch, command, cancellationToken).ConfigureAwait(false);

                        var affectedRows = await SqlConnectionHelper.ExecuteNonQueryAsync(command, cancellationToken)
                            .ConfigureAwait(false);

                        transaction?.Commit();

                        if (options.OnBatchCompleteAsync != null)
                        {
                            await options.OnBatchCompleteAsync(batch.Cast<object>().ToList(), batchNumber, affectedRows)
                                .ConfigureAwait(false);
                        }

                        return affectedRows;
                    }
                    catch (Exception ex)
                    {
                        transaction?.Rollback();

                        if (options.OnBatchErrorAsync != null)
                            await options.OnBatchErrorAsync(batch.Cast<object>().ToList(), batchNumber, ex).ConfigureAwait(false);

                        throw;
                    }
                }
            }
            finally
            {
                SqlConnectionHelper.CloseConnectionIfNeeded(connection, sqlOptions);
            }
        }
    }
}