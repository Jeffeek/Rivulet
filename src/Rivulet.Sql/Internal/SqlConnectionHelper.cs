using System.Data;
using System.Data.Common;

namespace Rivulet.Sql.Internal;

/// <summary>
///     Internal helper to reduce code duplication in SQL connection operations.
/// </summary>
internal static class SqlConnectionHelper
{
    /// <summary>
    ///     Creates a connection from factory and validates it's not null.
    /// </summary>
    internal static TConnection CreateAndValidate<TConnection>(Func<TConnection> connectionFactory)
        where TConnection : class, IDbConnection
    {
        var connection = connectionFactory();
        return connection ?? throw new InvalidOperationException("Connection factory returned null");
    }

    /// <summary>
    ///     Opens a connection asynchronously, handling both DbConnection and IDbConnection.
    /// </summary>
    internal static async ValueTask OpenConnectionAsync(
        IDbConnection connection,
        CancellationToken cancellationToken
    )
    {
        if (connection is DbConnection dbConnection)
            await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
        else
            await Task.Run(connection.Open, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Conditionally opens connection based on AutoManageConnection setting.
    /// </summary>
    internal static ValueTask OpenConnectionIfNeededAsync(
        IDbConnection connection,
        SqlOptions options,
        CancellationToken cancellationToken
    ) =>
        options.AutoManageConnection && connection.State != ConnectionState.Open
            ? OpenConnectionAsync(connection, cancellationToken)
            : ValueTask.CompletedTask;

    /// <summary>
    ///     Conditionally closes connection based on AutoManageConnection setting.
    /// </summary>
    internal static void CloseConnectionIfNeeded(IDbConnection connection, SqlOptions options)
    {
        if (options.AutoManageConnection && connection.State == ConnectionState.Open)
            connection.Close();
    }

    /// <summary>
    ///     Creates a command with standard configuration.
    /// </summary>
    private static IDbCommand CreateAndConfigureCommand(
        IDbConnection connection,
        string commandText,
        int commandTimeout,
        Action<IDbCommand>? configureParams = null
    )
    {
        var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.CommandTimeout = commandTimeout;
        configureParams?.Invoke(command);
        return command;
    }

    /// <summary>
    ///     Executes an operation within a managed connection lifecycle:
    ///     create → open → configure command → execute → error callback → close.
    /// </summary>
    internal static async ValueTask<TResult> ExecuteWithConnectionAsync<TResult>(
        Func<IDbConnection> connectionFactory,
        string sql,
        SqlOptions options,
        Func<IDbCommand, CancellationToken, ValueTask<TResult>> executor,
        CancellationToken cancellationToken,
        Action<IDbCommand>? configureParams = null
    )
    {
        var connection = CreateAndValidate(connectionFactory);
        using (connection)
        {
            try
            {
                await OpenConnectionIfNeededAsync(connection, options, cancellationToken)
                    .ConfigureAwait(false);

                using var command = CreateAndConfigureCommand(
                    connection,
                    sql,
                    options.CommandTimeout,
                    configureParams);

                return await executor(command, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (options.OnSqlErrorAsync != null)
                    await options.OnSqlErrorAsync(sql, ex, 0).ConfigureAwait(false);
                throw;
            }
            finally
            {
                CloseConnectionIfNeeded(connection, options);
            }
        }
    }

    /// <summary>
    ///     Executes a non-query command asynchronously.
    /// </summary>
    internal static async ValueTask<int> ExecuteNonQueryAsync(
        IDbCommand command,
        CancellationToken cancellationToken
    ) => command is DbCommand dbCommand
        ? await dbCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false)
        : await Task.Run(command.ExecuteNonQuery, cancellationToken).ConfigureAwait(false);

    /// <summary>
    ///     Executes a reader command asynchronously.
    /// </summary>
    internal static async ValueTask<IDataReader> ExecuteReaderAsync(
        IDbCommand command,
        CancellationToken cancellationToken
    ) => command is DbCommand dbCommand
        ? await dbCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false)
        : await Task.Run(command.ExecuteReader, cancellationToken).ConfigureAwait(false);

    /// <summary>
    ///     Executes a scalar command asynchronously.
    /// </summary>
    internal static async ValueTask<object?> ExecuteScalarAsync(
        IDbCommand command,
        CancellationToken cancellationToken
    ) => command is DbCommand dbCommand
        ? await dbCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)
        : await Task.Run(command.ExecuteScalar, cancellationToken).ConfigureAwait(false);
}
