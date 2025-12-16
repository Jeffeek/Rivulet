using System.Data;
using System.Data.Common;
using Rivulet.Core;

namespace Rivulet.Sql;

/// <summary>
///     Extension methods for parallel SQL query and command execution.
/// </summary>
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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queries);
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(readerMapper);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        return queries.SelectParallelAsync((query, ct) => ExecuteQueryAsync(query, connectionFactory, readerMapper, options, ct),
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
        CancellationToken cancellationToken = default)
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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(connectionFactory);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        return commands.SelectParallelAsync((command, ct) => ExecuteCommandAsync(command, connectionFactory, options, ct),
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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(commandsWithParams);
        ArgumentNullException.ThrowIfNull(connectionFactory);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

        return commandsWithParams.SelectParallelAsync((item, ct) => ExecuteCommandAsync(item.command, connectionFactory, options, ct, item.configureParams),
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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queries);
        ArgumentNullException.ThrowIfNull(connectionFactory);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

#pragma warning disable CA2007 // ConfigureAwait not applicable with 'await using' statements in ExecuteScalarAsync
        return queries.SelectParallelAsync((query, ct) => ExecuteScalarAsync<TResult>(query, connectionFactory, options, ct),
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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queriesWithParams);
        ArgumentNullException.ThrowIfNull(connectionFactory);

        options ??= new();
        var parallelOptions = options.GetMergedParallelOptions();

#pragma warning disable CA2007 // ConfigureAwait not applicable with 'await using' statements in ExecuteScalarAsync
        return queriesWithParams.SelectParallelAsync((item, ct) => ExecuteScalarAsync<TResult>(item.query, connectionFactory, options, ct, item.configureParams),
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
        Action<IDbCommand>? configureParams = null)
    {
        var connection = connectionFactory();
        using (connection)
        {
            try
            {
                if (options.AutoManageConnection && connection.State != ConnectionState.Open) await OpenConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

                using var command = connection.CreateCommand();
                command.CommandText = query;
                command.CommandTimeout = options.CommandTimeout;

                configureParams?.Invoke(command);

                using var reader = await ExecuteReaderAsync(command, cancellationToken).ConfigureAwait(false);
                return readerMapper(reader);
            }
            catch (Exception ex)
            {
                if (options.OnSqlErrorAsync != null) await options.OnSqlErrorAsync(query, ex, 0).ConfigureAwait(false);

                throw;
            }
            finally
            {
                if (options.AutoManageConnection && connection.State == ConnectionState.Open) connection.Close();
            }
        }
    }

    private static async ValueTask<int> ExecuteCommandAsync(
        string commandText,
        Func<IDbConnection> connectionFactory,
        SqlOptions options,
        CancellationToken cancellationToken,
        Action<IDbCommand>? configureParams = null)
    {
        var connection = connectionFactory();
        using (connection)
        {
            try
            {
                if (options.AutoManageConnection && connection.State != ConnectionState.Open) await OpenConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

                using var command = connection.CreateCommand();
                command.CommandText = commandText;
                command.CommandTimeout = options.CommandTimeout;

                configureParams?.Invoke(command);

                return await ExecuteNonQueryAsync(command, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (options.OnSqlErrorAsync != null) await options.OnSqlErrorAsync(commandText, ex, 0).ConfigureAwait(false);

                throw;
            }
            finally
            {
                if (options.AutoManageConnection && connection.State == ConnectionState.Open) connection.Close();
            }
        }
    }

    private static async ValueTask<TResult?> ExecuteScalarAsync<TResult>(
        string query,
        Func<IDbConnection> connectionFactory,
        SqlOptions options,
        CancellationToken cancellationToken,
        Action<IDbCommand>? configureParams = null)
    {
        var connection = connectionFactory();
        using (connection)
        {
            try
            {
                if (options.AutoManageConnection && connection.State != ConnectionState.Open) await OpenConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

                using var command = connection.CreateCommand();
                command.CommandText = query;
                command.CommandTimeout = options.CommandTimeout;

                configureParams?.Invoke(command);

                var result = await ExecuteScalarInternalAsync(command, cancellationToken).ConfigureAwait(false);

                if (result == null || result == DBNull.Value) return default;

                return (TResult)Convert.ChangeType(result, typeof(TResult));
            }
            catch (Exception ex)
            {
                if (options.OnSqlErrorAsync != null) await options.OnSqlErrorAsync(query, ex, 0).ConfigureAwait(false);

                throw;
            }
            finally
            {
                if (options.AutoManageConnection && connection.State == ConnectionState.Open) connection.Close();
            }
        }
    }

    private static async ValueTask OpenConnectionAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        if (connection is DbConnection dbConnection)
            await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
        else
            await Task.Run(connection.Open, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<IDataReader> ExecuteReaderAsync(IDbCommand command, CancellationToken cancellationToken)
    {
        if (command is DbCommand dbCommand) return await dbCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        return await Task.Run(command.ExecuteReader, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<int> ExecuteNonQueryAsync(IDbCommand command, CancellationToken cancellationToken)
    {
        if (command is DbCommand dbCommand) return await dbCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return await Task.Run(command.ExecuteNonQuery, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<object?> ExecuteScalarInternalAsync(IDbCommand command, CancellationToken cancellationToken)
    {
        if (command is DbCommand dbCommand) return await dbCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        return await Task.Run(command.ExecuteScalar, cancellationToken).ConfigureAwait(false);
    }
}