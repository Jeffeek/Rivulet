using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Rivulet.Core;

namespace Rivulet.Sql;

/// <summary>
///     Configuration options for parallel SQL operations.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
public sealed class SqlOptions
{
    internal const int DefaultRetryCount = 3;
    internal const int DefaultCommandTimeout = 30;

    /// <summary>
    ///     Parallel execution options from Rivulet.Core.
    ///     If null, default options with SQL-specific retry logic will be used.
    /// </summary>
    public ParallelOptionsRivulet? ParallelOptions { get; init; }

    /// <summary>
    ///     Command timeout in seconds. Default: 30 seconds.
    /// </summary>
    public int CommandTimeout { get; init; } = DefaultCommandTimeout;

    /// <summary>
    ///     Whether to automatically open and close connections for each operation.
    ///     Default: true. Set to false if managing connection state manually.
    /// </summary>
    public bool AutoManageConnection { get; init; } = true;

    /// <summary>
    ///     Isolation level for transactions. Default: ReadCommitted.
    ///     Only used when UseTransaction is true.
    /// </summary>
    public IsolationLevel IsolationLevel { get; init; } = IsolationLevel.ReadCommitted;

    /// <summary>
    ///     Callback invoked when a SQL error occurs.
    ///     Parameters: (sql command/query, exception, always 0).
    ///     Note: The third parameter is always 0 because retries are handled internally by Rivulet.Core's retry mechanism.
    /// </summary>
    public Func<object?, Exception, int, ValueTask>? OnSqlErrorAsync { get; init; }

    /// <summary>
    ///     Creates a merged ParallelOptionsRivulet with SQL-specific defaults.
    /// </summary>
    internal ParallelOptionsRivulet GetMergedParallelOptions()
    {
        var baseOptions = ParallelOptions ?? new();
        var maxRetries = baseOptions.MaxRetries > 0 ? baseOptions.MaxRetries : DefaultRetryCount;
        var perItemTimeout = baseOptions.PerItemTimeout ?? TimeSpan.FromSeconds(CommandTimeout + 5);

        return new(baseOptions)
        {
            MaxRetries = maxRetries,
            PerItemTimeout = perItemTimeout,
            IsTransient = ex => (baseOptions.IsTransient != null && baseOptions.IsTransient.Invoke(ex)) || SqlIsTransient(ex),
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool SqlIsTransient(Exception ex) =>
            ex switch
            {
                TimeoutException => true,
                InvalidOperationException invalidOp when invalidOp.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) => true,
                // Check MySQL and Npgsql before SqlException since "MySqlException" contains "SqlException"
                _ when ex.GetType().Name.Contains("MySqlException") => IsMySqlTransientError(ex),
                _ when ex.GetType().Name.Contains("NpgsqlException") => IsNpgsqlTransientError(ex),
                _ when ex.GetType().Name.Contains("SqlException") => IsSqlTransientError(ex),
                _ => false
            };
    }

    private static bool IsSqlTransientError(Exception ex)
    {
        var numberProperty = ex.GetType().GetProperty("Number");
        return numberProperty?.GetValue(ex) is int errorNumber && errorNumber switch
        {
            -2 => true,    // Timeout
            -1 => true,    // Connection broken
            2 => true,     // Connection timeout
            53 => true,    // Connection does not exist
            64 => true,    // Error on server
            233 => true,   // Connection initialization failed
            10053 => true, // Transport-level error
            10054 => true, // Connection reset by peer
            10060 => true, // Network timeout
            40197 => true, // Service unavailable
            40501 => true, // Service busy
            40613 => true, // Database unavailable
            49918 => true, // Cannot process request
            49919 => true, // Cannot process create or update
            49920 => true, // Cannot process more than requests
            _ => false
        };
    }

    private static bool IsNpgsqlTransientError(Exception ex)
    {
        var sqlStateProperty = ex.GetType().GetProperty("SqlState");
        return sqlStateProperty?.GetValue(ex) is string sqlState && sqlState switch
        {
            "08000" => true, // Connection exception
            "08003" => true, // Connection does not exist
            "08006" => true, // Connection failure
            "08001" => true, // Unable to establish connection
            "08004" => true, // Server rejected connection
            "53300" => true, // Too many connections
            "57P03" => true, // Cannot connect now
            "58000" => true, // System error
            "58030" => true, // IO error
            _ => false
        };
    }

    private static bool IsMySqlTransientError(Exception ex)
    {
        var numberProperty = ex.GetType().GetProperty("Number");
        return numberProperty?.GetValue(ex) is int errorNumber && errorNumber switch
        {
            1040 => true,         // Too many connections
            1205 => true,         // Lock wait timeout
            1213 => true,         // Deadlock found
            1226 => true,         // User has exceeded resource limit
            2002 or 2003 => true, // Can't connect to server
            2006 => true,         // Server has gone away
            2013 => true,         // Lost connection during query
            _ => false
        };
    }
}
