using System.Data;
using System.Diagnostics.CodeAnalysis;
using Rivulet.Core;
using Rivulet.Sql.Internal;

namespace Rivulet.Sql;

/// <summary>
///     Extension methods for parallel SQL query and command execution.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
public static class SqlParallelExtensions
{
    /// <summary>
    ///     Executes SQL queries in parallel and returns results mapped by the provided function.
    ///     Automatically manages connection state unless disabled in options.
    /// </summary>
    /// <typeparam name="TResult">The result type from each query.</typeparam>
    /// <param name="queries">The SQL queries to execute.</param>
    /// <param name="connectionFactory">Factory function to create database connections.</param>
    /// <param name="readerMapper">Function to map IDataReader to TResult.</param>
    /// <param name="options">SQL execution options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of query results.</returns>
    public static Task<List<TResult>> ExecuteQueriesParallelAsync<TResult>(
        this IEnumerable<string> queries,
        Func<IDbConnection> connectionFactory,
        Func<IDataReader, TResult> readerMapper,
        SqlOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(queries);
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(readerMapper);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        return queries.SelectParallelAsync(
            (query, ct) => ExecuteQueryAsync(query, connectionFactory, readerMapper, options, ct),
            parallelOptions,
            cancellationToken);
    }

    /// <summary>
    ///     Executes SQL queries in parallel with parameters and returns results mapped by the provided function.
    /// </summary>
    /// <typeparam name="TResult">The result type from each query.</typeparam>
    /// <param name="queriesWithParams">
    ///     The SQL queries with their parameters. Use the string overload if parameters are not
    ///     needed.
    /// </param>
    /// <param name="connectionFactory">Factory function to create database connections.</param>
    /// <param name="readerMapper">Function to map IDataReader to TResult.</param>
    /// <param name="options">SQL execution options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of query results.</returns>
    public static Task<List<TResult>> ExecuteQueriesParallelAsync<TResult>(
        this IEnumerable<(string query, Action<IDbCommand> configureParams)> queriesWithParams,
        Func<IDbConnection> connectionFactory,
        Func<IDataReader, TResult> readerMapper,
        SqlOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(queriesWithParams);
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(readerMapper);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        return queriesWithParams.SelectParallelAsync((item, ct) => ExecuteQueryAsync(item.query,
                connectionFactory,
                readerMapper,
                options,
                ct,
                item.configureParams),
            parallelOptions,
            cancellationToken);
    }

    /// <summary>
    ///     Executes SQL commands in parallel (INSERT, UPDATE, DELETE, etc.) and returns the number of affected rows.
    /// </summary>
    /// <param name="commands">The SQL commands to execute.</param>
    /// <param name="connectionFactory">Factory function to create database connections.</param>
    /// <param name="options">SQL execution options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of affected row counts.</returns>
    public static Task<List<int>> ExecuteCommandsParallelAsync(
        this IEnumerable<string> commands,
        Func<IDbConnection> connectionFactory,
        SqlOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(connectionFactory);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        return commands.SelectParallelAsync(
            (command, ct) => ExecuteCommandAsync(command, connectionFactory, options, ct),
            parallelOptions,
            cancellationToken);
    }

    /// <summary>
    ///     Executes SQL commands in parallel with parameters and returns the number of affected rows.
    /// </summary>
    /// <param name="commandsWithParams">The SQL commands with their parameters.</param>
    /// <param name="connectionFactory">Factory function to create database connections.</param>
    /// <param name="options">SQL execution options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of affected row counts.</returns>
    public static Task<List<int>> ExecuteCommandsParallelAsync(
        this IEnumerable<(string command, Action<IDbCommand> configureParams)> commandsWithParams,
        Func<IDbConnection> connectionFactory,
        SqlOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(commandsWithParams);
        ArgumentNullException.ThrowIfNull(connectionFactory);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        return commandsWithParams.SelectParallelAsync(
            (item, ct) => ExecuteCommandAsync(item.command, connectionFactory, options, ct, item.configureParams),
            parallelOptions,
            cancellationToken);
    }

    /// <summary>
    ///     Executes scalar SQL queries in parallel (e.g., SELECT COUNT, SELECT MAX, etc.).
    /// </summary>
    /// <typeparam name="TResult">The scalar result type.</typeparam>
    /// <param name="queries">The SQL queries to execute.</param>
    /// <param name="connectionFactory">Factory function to create database connections.</param>
    /// <param name="options">SQL execution options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of scalar results.</returns>
    public static Task<List<TResult?>> ExecuteScalarParallelAsync<TResult>(
        this IEnumerable<string> queries,
        Func<IDbConnection> connectionFactory,
        SqlOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(queries);
        ArgumentNullException.ThrowIfNull(connectionFactory);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

#pragma warning disable CA2007 // ConfigureAwait not applicable with 'await using' statements in ExecuteScalarAsync
        return queries.SelectParallelAsync(
            (query, ct) => ExecuteScalarAsync<TResult>(query, connectionFactory, options, ct),
            parallelOptions,
            cancellationToken);
#pragma warning restore CA2007
    }

    /// <summary>
    ///     Executes scalar SQL queries in parallel with parameters.
    /// </summary>
    /// <typeparam name="TResult">The scalar result type.</typeparam>
    /// <param name="queriesWithParams">The SQL queries with their parameters.</param>
    /// <param name="connectionFactory">Factory function to create database connections.</param>
    /// <param name="options">SQL execution options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of scalar results.</returns>
    public static Task<List<TResult?>> ExecuteScalarParallelAsync<TResult>(
        this IEnumerable<(string query, Action<IDbCommand> configureParams)> queriesWithParams,
        Func<IDbConnection> connectionFactory,
        SqlOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(queriesWithParams);
        ArgumentNullException.ThrowIfNull(connectionFactory);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

#pragma warning disable CA2007 // ConfigureAwait not applicable with 'await using' statements in ExecuteScalarAsync
        return queriesWithParams.SelectParallelAsync(
            (item, ct) => ExecuteScalarAsync<TResult>(item.query, connectionFactory, options, ct, item.configureParams),
            parallelOptions,
            cancellationToken);
#pragma warning restore CA2007
    }

    private static async ValueTask<TResult> ExecuteQueryAsync<TResult>(
        string query,
        Func<IDbConnection> connectionFactory,
        Func<IDataReader, TResult> readerMapper,
        SqlOptions options,
        CancellationToken cancellationToken,
        Action<IDbCommand>? configureParams = null
    )
    {
        var connection = SqlConnectionHelper.CreateAndValidate(connectionFactory);
        using (connection)
        {
            try
            {
                await SqlConnectionHelper.OpenConnectionIfNeededAsync(connection, options, cancellationToken)
                    .ConfigureAwait(false);

                using var command = SqlConnectionHelper.CreateAndConfigureCommand(
                    connection,
                    query,
                    options.CommandTimeout,
                    configureParams);

                using var reader = await SqlConnectionHelper.ExecuteReaderAsync(command, cancellationToken)
                    .ConfigureAwait(false);
                return readerMapper(reader);
            }
            catch (Exception ex)
            {
                if (options.OnSqlErrorAsync != null)
                    await options.OnSqlErrorAsync(query, ex, 0).ConfigureAwait(false);
                throw;
            }
            finally
            {
                SqlConnectionHelper.CloseConnectionIfNeeded(connection, options);
            }
        }
    }

    private static async ValueTask<int> ExecuteCommandAsync(
        string commandText,
        Func<IDbConnection> connectionFactory,
        SqlOptions options,
        CancellationToken cancellationToken,
        Action<IDbCommand>? configureParams = null
    )
    {
        var connection = SqlConnectionHelper.CreateAndValidate(connectionFactory);
        using (connection)
        {
            try
            {
                await SqlConnectionHelper.OpenConnectionIfNeededAsync(connection, options, cancellationToken)
                    .ConfigureAwait(false);

                using var command = SqlConnectionHelper.CreateAndConfigureCommand(
                    connection,
                    commandText,
                    options.CommandTimeout,
                    configureParams);

                return await SqlConnectionHelper.ExecuteNonQueryAsync(command, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (options.OnSqlErrorAsync != null)
                    await options.OnSqlErrorAsync(commandText, ex, 0).ConfigureAwait(false);
                throw;
            }
            finally
            {
                SqlConnectionHelper.CloseConnectionIfNeeded(connection, options);
            }
        }
    }

    private static async ValueTask<TResult?> ExecuteScalarAsync<TResult>(
        string query,
        Func<IDbConnection> connectionFactory,
        SqlOptions options,
        CancellationToken cancellationToken,
        Action<IDbCommand>? configureParams = null
    )
    {
        var connection = SqlConnectionHelper.CreateAndValidate(connectionFactory);
        using (connection)
        {
            try
            {
                await SqlConnectionHelper.OpenConnectionIfNeededAsync(connection, options, cancellationToken)
                    .ConfigureAwait(false);

                using var command = SqlConnectionHelper.CreateAndConfigureCommand(
                    connection,
                    query,
                    options.CommandTimeout,
                    configureParams);

                var result = await SqlConnectionHelper.ExecuteScalarAsync(command, cancellationToken)
                    .ConfigureAwait(false);

                return result == null || result == DBNull.Value
                    ? default
                    : (TResult)Convert.ChangeType(result, typeof(TResult));
            }
            catch (Exception ex)
            {
                if (options.OnSqlErrorAsync != null)
                    await options.OnSqlErrorAsync(query, ex, 0).ConfigureAwait(false);
                throw;
            }
            finally
            {
                SqlConnectionHelper.CloseConnectionIfNeeded(connection, options);
            }
        }
    }
}
