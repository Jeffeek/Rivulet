using System.Data;
using System.Diagnostics.CodeAnalysis;
using Rivulet.Base.Tests;

namespace Rivulet.Sql.Tests;

public class SqlParallelExtensionsTests
{
    [Fact]
    public async Task ExecuteQueriesParallelAsync_WithValidQueries_ShouldReturnResults()
    {
        var queries = new[] { "SELECT * FROM Users WHERE Id = 1", "SELECT * FROM Users WHERE Id = 2", "SELECT * FROM Users WHERE Id = 3" };

        var results = await queries.ExecuteQueriesParallelAsync(
            () => new TestDbConnection(
                executeReaderFunc: static _ => new TestDataReader([new() { ["Id"] = 1, ["Name"] = "User1" }])),
            reader =>
            {
                var items = new List<string>();
                while (reader.Read()) items.Add(reader.GetString(1));

                return items;
            });

        results.Count.ShouldBe(3);
        results.ShouldAllBe(r => r.Count == 1);
    }

    [Fact]
    public async Task ExecuteQueriesParallelAsync_WithNullQueries_ShouldThrowArgumentNullException()
    {
        IEnumerable<string> queries = null!;

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await queries.ExecuteQueriesParallelAsync(
            () => new TestDbConnection(),
            static _ => new List<string>()));
    }

    [Fact]
    public async Task ExecuteQueriesParallelAsync_WithNullConnectionFactory_ShouldThrowArgumentNullException()
    {
        var queries = new[] { "SELECT 1" };

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await queries.ExecuteQueriesParallelAsync(
            null!,
            static _ => new List<string>()));
    }

    [Fact]
    public async Task ExecuteQueriesParallelAsync_WithNullReaderMapper_ShouldThrowArgumentNullException()
    {
        var queries = new[] { "SELECT 1" };

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await queries.ExecuteQueriesParallelAsync<string>(
            () => new TestDbConnection(),
            null!));
    }

    [Fact]
    public async Task ExecuteQueriesParallelAsync_WithParameters_ShouldConfigureCommand()
    {
        var commandsReceived = new List<string>();
        var lockObj = new object();
        var queriesWithParams = new[]
        {
            (query: "SELECT * FROM Users WHERE Id = @id", configureParams: (Action<IDbCommand>)(cmd =>
            {
                lock (lockObj) commandsReceived.Add(cmd.CommandText);
            })),
            (query: "SELECT * FROM Products WHERE Id = @id", configureParams: (Action<IDbCommand>)(cmd =>
            {
                lock (lockObj) commandsReceived.Add(cmd.CommandText);
            }))
        };

        var results = await queriesWithParams.ExecuteQueriesParallelAsync(static () => new TestDbConnection(
                executeReaderFunc: static _ => new TestDataReader([new() { ["Id"] = 1 }])),
            static _ => new List<int> { 1 });

        results.Count.ShouldBe(2);
        commandsReceived.Count.ShouldBe(2);
        commandsReceived.ShouldContain("SELECT * FROM Users WHERE Id = @id");
        commandsReceived.ShouldContain("SELECT * FROM Products WHERE Id = @id");
    }

    [Fact]
    public async Task ExecuteCommandsParallelAsync_WithValidCommands_ShouldReturnAffectedRows()
    {
        var commands = new[]
        {
            "INSERT INTO Users (Name) VALUES ('User1')",
            "INSERT INTO Users (Name) VALUES ('User2')",
            "INSERT INTO Users (Name) VALUES ('User3')"
        };

        var results = await commands.ExecuteCommandsParallelAsync(() => new TestDbConnection(executeNonQueryFunc: static _ => 1));

        results.Count.ShouldBe(3);
        results.ShouldAllBe(r => r == 1);
    }

    [Fact]
    public async Task ExecuteCommandsParallelAsync_WithNullCommands_ShouldThrowArgumentNullException()
    {
        IEnumerable<string> commands = null!;

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await commands.ExecuteCommandsParallelAsync(() => new TestDbConnection()));
    }

    [Fact]
    public async Task ExecuteCommandsParallelAsync_WithParameters_ShouldConfigureCommand()
    {
        var commandsReceived = new List<string>();
        var lockObj = new object();
        var commandsWithParams = new[]
        {
            (command: "UPDATE Users SET Name = @name WHERE Id = @id", configureParams: cmd =>
            {
                lock (lockObj) commandsReceived.Add(cmd.CommandText);
            }),
            (command: "DELETE FROM Users WHERE Id = @id", configureParams: (Action<IDbCommand>)(cmd =>
            {
                lock (lockObj) commandsReceived.Add(cmd.CommandText);
            }))
        };

        var results = await commandsWithParams.ExecuteCommandsParallelAsync(() => new TestDbConnection(executeNonQueryFunc: static _ => 1));

        results.Count.ShouldBe(2);
        commandsReceived.Count.ShouldBe(2);
        commandsReceived.ShouldContain("UPDATE Users SET Name = @name WHERE Id = @id");
        commandsReceived.ShouldContain("DELETE FROM Users WHERE Id = @id");
    }

    [Fact]
    public async Task ExecuteScalarParallelAsync_WithValidQueries_ShouldReturnScalarValues()
    {
        var queries = new[] { "SELECT COUNT(*) FROM Users", "SELECT MAX(Id) FROM Users", "SELECT MIN(Id) FROM Users" };

        var values = new[] { 10, 100, 1 };
        var index = -1; // Start at -1 because Interlocked.Increment happens before indexing

        var results = await queries.ExecuteScalarParallelAsync<int>(() =>
            new TestDbConnection(_ => values[Interlocked.Increment(ref index) % values.Length]));

        results.Count.ShouldBe(3);
        results.ShouldContain(10);
        results.ShouldContain(100);
        results.ShouldContain(1);
    }

    [Fact]
    public async Task ExecuteScalarParallelAsync_WithNullQueries_ShouldThrowArgumentNullException()
    {
        IEnumerable<string> queries = null!;

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await queries.ExecuteScalarParallelAsync<int>(() => new TestDbConnection()));
    }

    [Fact]
    public async Task ExecuteScalarParallelAsync_WithDBNull_ShouldReturnDefault()
    {
        var queries = new[] { "SELECT NULL" };

        var results = await queries.ExecuteScalarParallelAsync<int>(() => new TestDbConnection(static _ => DBNull.Value));

        results.Count.ShouldBe(1);
        results[0].ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteScalarParallelAsync_WithParameters_ShouldConfigureCommand()
    {
        var commandsReceived = new List<string>();
        var queriesWithParams = new[]
        {
            (query: "SELECT COUNT(*) FROM Users WHERE Active = @active", configureParams: (Action<IDbCommand>)(cmd =>
            {
                commandsReceived.Add(cmd.CommandText);
            }))
        };

        var results = await queriesWithParams.ExecuteScalarParallelAsync<int>(() => new TestDbConnection(static _ => 42));

        results.Count.ShouldBe(1);
        results[0].ShouldBe(42);
        commandsReceived.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ExecuteQueriesParallelAsync_WithOptions_ShouldUseOptions()
    {
        var queries = new[] { "SELECT 1" };
        var options = new SqlOptions { CommandTimeout = 60, ParallelOptions = new() { MaxDegreeOfParallelism = 5 } };

        var commandTimeout = 0;
        var results = await queries.ExecuteQueriesParallelAsync(
            () => new TestDbConnection(
                executeReaderFunc: cmd =>
                {
                    commandTimeout = cmd.CommandTimeout;
                    return new TestDataReader([]);
                }),
            static _ => 1,
            options);

        commandTimeout.ShouldBe(60);
        results.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteCommandsParallelAsync_WithTransientError_ShouldRetry()
    {
        var commands = new[] { "INSERT INTO Users (Name) VALUES ('User1')" };
        var attemptCount = 0;

        var results = await commands.ExecuteCommandsParallelAsync(
            () => new TestDbConnection(executeNonQueryFunc: _ =>
            {
                attemptCount++;
                if (attemptCount < 2) throw new TimeoutException("Timeout occurred");

                return 1;
            }),
            new() { ParallelOptions = new() { MaxRetries = 3, BaseDelay = TimeSpan.FromMilliseconds(10) } });

        attemptCount.ShouldBe(2);
        results.Count.ShouldBe(1);
        results[0].ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteQueriesParallelAsync_WithError_ShouldInvokeErrorCallback()
    {
        var queries = new[] { "SELECT * FROM Users" };
        object? callbackItem = null;
        Exception? callbackException = null;
        int? callbackRetryAttempt = null;

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await queries.ExecuteQueriesParallelAsync(
                () => new TestDbConnection(
                    executeReaderFunc: static _ => throw new InvalidOperationException("Test error")),
                static _ => 1,
                new()
                {
                    OnSqlErrorAsync = (item, ex, retry) =>
                    {
                        callbackItem = item;
                        callbackException = ex;
                        callbackRetryAttempt = retry;
                        return ValueTask.CompletedTask;
                    },
                    ParallelOptions = new() { MaxRetries = 0 }
                }));

        callbackItem.ShouldNotBeNull();
        callbackException.ShouldNotBeNull();
        callbackException.ShouldBeOfType<InvalidOperationException>();
        callbackRetryAttempt.ShouldBe(0);
    }

    [Fact]
    [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
    public async Task ExecuteCommandsParallelAsync_WithCancellation_ShouldThrowOperationCanceledException()
    {
        var commands = Enumerable.Range(1, 100).Select(static i => $"INSERT INTO Users (Id) VALUES ({i})");
        using var cts = new CancellationTokenSource();

        cts.CancelAfter(50);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await commands.ExecuteCommandsParallelAsync(
            () => new TestDbConnection(executeNonQueryFunc: _ =>
            {
                Task.Delay(100, cts.Token).Wait(cts.Token);
                return 1;
            }),
            cancellationToken: cts.Token));
    }

    [Fact]
    public async Task ExecuteQueriesParallelAsync_WithAutoManageConnectionTrue_ShouldOpenAndCloseConnection()
    {
        var queries = new[] { "SELECT 1" };
        TestDbConnection? capturedConnection = null;

        var results = await queries.ExecuteQueriesParallelAsync(
            () =>
            {
                var conn = new TestDbConnection(
                    executeReaderFunc: static _ => new TestDataReader([]));
                capturedConnection = conn;
                return conn;
            },
            static _ => 1,
            new() { AutoManageConnection = true });

        results.Count.ShouldBe(1);
        capturedConnection.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExecuteCommandsParallelAsync_WithCustomParallelism_ShouldRespectConcurrencyLimit()
    {
        var commands = Enumerable.Range(1, 50).Select(static i => $"INSERT INTO Users (Id) VALUES ({i})");
        var maxConcurrent = 0;
        var currentConcurrent = 0;
        var lockObj = new object();

        var results = await commands.ExecuteCommandsParallelAsync(
            () => new TestDbConnection(executeNonQueryFunc: _ =>
            {
                lock (lockObj)
                {
                    currentConcurrent++;
                    maxConcurrent = Math.Max(maxConcurrent, currentConcurrent);
                }

                Task.Delay(50, CancellationToken.None).Wait();

                lock (lockObj) currentConcurrent--;

                return 1;
            }),
            new() { ParallelOptions = new() { MaxDegreeOfParallelism = 5 } });

        results.Count.ShouldBe(50);
        maxConcurrent.ShouldBeLessThanOrEqualTo(5);
    }

    [Fact]
    public async Task ExecuteQueriesParallelAsync_WithAutoManageConnectionFalse_ShouldNotManageConnection()
    {
        var connection = new TestDbConnection(
            executeReaderFunc: static _ => new TestDataReader([new() { ["Value"] = 1 }]));
        connection.Open(); // Manually open

        var queries = new[] { "SELECT 1", "SELECT 2" };

        var results = await queries.ExecuteQueriesParallelAsync(
            () => connection,
            reader =>
            {
                reader.Read();
                return reader.GetInt32(0);
            },
            new() { AutoManageConnection = false });

        results.Count.ShouldBe(2);
        connection.State.ShouldBe(ConnectionState.Open); // Should still be open
        connection.Close();
    }

    [Fact]
    public async Task ExecuteCommandsParallelAsync_WithAutoManageConnectionFalse_ShouldNotManageConnection()
    {
        var connection = new TestDbConnection(executeNonQueryFunc: static _ => 1);
        connection.Open();

        var commands = new[] { "INSERT 1", "INSERT 2" };

        var results = await commands.ExecuteCommandsParallelAsync(
            () => connection,
            new() { AutoManageConnection = false });

        results.ShouldAllBe(r => r == 1);
        connection.State.ShouldBe(ConnectionState.Open); // Should still be open
        connection.Close();
    }

    [Fact]
    public async Task ExecuteScalarParallelAsync_WithAutoManageConnectionFalse_ShouldNotManageConnection()
    {
        var callCount = 0;
        var connection = new TestDbConnection(_ => ++callCount);
        connection.Open();

        var queries = new[] { "SELECT 42", "SELECT 99" };

        var results = await queries.ExecuteScalarParallelAsync<int>(
            () => connection,
            new() { AutoManageConnection = false });

        results.Count.ShouldBe(2);
        connection.State.ShouldBe(ConnectionState.Open); // Should still be open
        connection.Close();
    }

    [Fact]
    public async Task ExecuteQueriesParallelAsync_WithOnSqlErrorCallback_ShouldInvokeOnError()
    {
        string? capturedQuery = null;
        Exception? capturedException = null;
        var options = new SqlOptions
        {
            OnSqlErrorAsync = (query, ex, _) =>
            {
                capturedQuery = query as string;
                capturedException = ex;
                return ValueTask.CompletedTask;
            }
        };

        var queries = new[] { "INVALID SQL QUERY" };

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await queries.ExecuteQueriesParallelAsync(
                () => new TestDbConnection(executeReaderFunc: static _ => throw new InvalidOperationException("SQL Error")),
                reader => reader.GetInt32(0),
                options));

        capturedQuery.ShouldBe("INVALID SQL QUERY");
        capturedException.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExecuteCommandsParallelAsync_WithOnSqlErrorCallback_ShouldInvokeOnError()
    {
        string? capturedCommand = null;
        Exception? capturedException = null;
        var options = new SqlOptions
        {
            OnSqlErrorAsync = (cmd, ex, _) =>
            {
                capturedCommand = cmd as string;
                capturedException = ex;
                return ValueTask.CompletedTask;
            }
        };

        var commands = new[] { "INVALID SQL" };

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await commands.ExecuteCommandsParallelAsync(
                () => new TestDbConnection(executeNonQueryFunc: static _ => throw new InvalidOperationException("SQL Error")),
                options));

        capturedCommand.ShouldBe("INVALID SQL");
        capturedException.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExecuteScalarParallelAsync_WithOnSqlErrorCallback_ShouldInvokeOnError()
    {
        string? capturedQuery = null;
        Exception? capturedException = null;
        var options = new SqlOptions
        {
            OnSqlErrorAsync = (query, ex, _) =>
            {
                capturedQuery = query as string;
                capturedException = ex;
                return ValueTask.CompletedTask;
            }
        };

        var queries = new[] { "INVALID SQL" };

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await queries.ExecuteScalarParallelAsync<int>(
                () => new TestDbConnection(static _ => throw new InvalidOperationException("SQL Error")),
                options));

        capturedQuery.ShouldBe("INVALID SQL");
        capturedException.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExecuteScalarParallelAsync_WithNullResult_ShouldReturnDefault()
    {
        var connection = new TestDbConnection(static _ => null);

        var queries = new[] { "SELECT NULL" };

        var results = await queries.ExecuteScalarParallelAsync<string>(() => connection);

        results.ShouldHaveSingleItem();
        results[0].ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteScalarParallelAsync_WithDBNullValue_ShouldReturnDefault()
    {
        var connection = new TestDbConnection(static _ => DBNull.Value);

        var queries = new[] { "SELECT NULL" };

        var results = await queries.ExecuteScalarParallelAsync<int?>(() => connection);

        results.ShouldHaveSingleItem();
        results[0].ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteQueriesParallelAsync_WithNullQueriesWithParams_ShouldThrowArgumentNullException()
    {
        IEnumerable<(string query, Action<IDbCommand> configureParams)> queriesWithParams = null!;

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await queriesWithParams.ExecuteQueriesParallelAsync(
            () => new TestDbConnection(),
            static _ => new List<string>()));
    }

    [Fact]
    public async Task ExecuteQueriesParallelAsync_WithNullConnectionFactoryWithParams_ShouldThrowArgumentNullException()
    {
        var queriesWithParams = new[] { (query: "SELECT 1", configureParams: (Action<IDbCommand>)(_ => { })) };

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await queriesWithParams.ExecuteQueriesParallelAsync(
            null!,
            static _ => new List<string>()));
    }

    [Fact]
    public async Task ExecuteQueriesParallelAsync_WithNullReaderMapperWithParams_ShouldThrowArgumentNullException()
    {
        var queriesWithParams = new[] { (query: "SELECT 1", configureParams: (Action<IDbCommand>)(_ => { })) };

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await queriesWithParams.ExecuteQueriesParallelAsync<string>(
            () => new TestDbConnection(),
            null!));
    }

    [Fact]
    public async Task ExecuteCommandsParallelAsync_WithNullConnectionFactory_ShouldThrowArgumentNullException()
    {
        var commands = new[] { "INSERT 1" };

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await commands.ExecuteCommandsParallelAsync(null!));
    }

    [Fact]
    public async Task ExecuteCommandsParallelAsync_WithNullCommandsWithParams_ShouldThrowArgumentNullException()
    {
        IEnumerable<(string command, Action<IDbCommand> configureParams)> commandsWithParams = null!;

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await commandsWithParams.ExecuteCommandsParallelAsync(() => new TestDbConnection()));
    }

    [Fact]
    public async Task ExecuteCommandsParallelAsync_WithNullConnectionFactoryWithParams_ShouldThrowArgumentNullException()
    {
        var commandsWithParams = new[] { (command: "INSERT 1", configureParams: (Action<IDbCommand>)(_ => { })) };

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await commandsWithParams.ExecuteCommandsParallelAsync(null!));
    }

    [Fact]
    public async Task ExecuteScalarParallelAsync_WithNullConnectionFactory_ShouldThrowArgumentNullException()
    {
        var queries = new[] { "SELECT 1" };

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await queries.ExecuteScalarParallelAsync<int>(null!));
    }

    [Fact]
    public async Task ExecuteScalarParallelAsync_WithNullQueriesWithParams_ShouldThrowArgumentNullException()
    {
        IEnumerable<(string query, Action<IDbCommand> configureParams)> queriesWithParams = null!;

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await queriesWithParams.ExecuteScalarParallelAsync<int>(() => new TestDbConnection()));
    }

    [Fact]
    public async Task ExecuteScalarParallelAsync_WithNullConnectionFactoryWithParams_ShouldThrowArgumentNullException()
    {
        var queriesWithParams = new[] { (query: "SELECT 1", configureParams: (Action<IDbCommand>)(_ => { })) };

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await queriesWithParams.ExecuteScalarParallelAsync<int>(null!));
    }

    [Fact]
    public async Task ExecuteQueriesParallelAsync_WithNonDbConnection_ShouldUseTaskRunFallback()
    {
        // Use a custom IDbConnection that's not DbConnection to test Task.Run fallback
        var queries = new[] { "SELECT 1" };

        var results = await queries.ExecuteQueriesParallelAsync(
            () => new NonDbConnection(
                executeReaderFunc: static _ => new TestDataReader([new() { ["Value"] = 1 }])),
            reader =>
            {
                reader.Read();
                return reader.GetInt32(0);
            });

        results.Count.ShouldBe(1);
        results[0].ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteCommandsParallelAsync_WithNonDbConnection_ShouldUseTaskRunFallback()
    {
        // Use a custom IDbConnection that's not DbConnection to test Task.Run fallback
        var commands = new[] { "INSERT 1" };

        var results = await commands.ExecuteCommandsParallelAsync(() => new NonDbConnection(executeNonQueryFunc: static _ => 1));

        results.Count.ShouldBe(1);
        results[0].ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteScalarParallelAsync_WithNonDbConnection_ShouldUseTaskRunFallback()
    {
        // Use a custom IDbConnection that's not DbConnection to test Task.Run fallback
        var queries = new[] { "SELECT 42" };

        var results = await queries.ExecuteScalarParallelAsync<int>(() => new NonDbConnection(static _ => 42));

        results.Count.ShouldBe(1);
        results[0].ShouldBe(42);
    }

    [Fact]
    public async Task ExecuteQueriesParallelAsync_WithoutOptions_ShouldUseDefaults()
    {
        var queries = new[] { "SELECT 1", "SELECT 2" };
        var commandTimeouts = new List<int>();

        var results = await queries.ExecuteQueriesParallelAsync(
            () => new TestDbConnection(
                executeReaderFunc: cmd =>
                {
                    commandTimeouts.Add(cmd.CommandTimeout);
                    return new TestDataReader([]);
                }),
            static _ => 1);

        results.Count.ShouldBe(2);
        commandTimeouts.ShouldAllBe(timeout => timeout == 30); // Default timeout
    }

    [Fact]
    public async Task ExecuteCommandsParallelAsync_WithoutOptions_ShouldUseDefaults()
    {
        var commands = new[] { "INSERT 1", "INSERT 2" };
        var commandTimeouts = new List<int>();

        var results = await commands.ExecuteCommandsParallelAsync(() => new TestDbConnection(executeNonQueryFunc: cmd =>
        {
            commandTimeouts.Add(cmd.CommandTimeout);
            return 1;
        }));

        results.Count.ShouldBe(2);
        commandTimeouts.ShouldAllBe(timeout => timeout == 30); // Default timeout
    }

    [Fact]
    public async Task ExecuteScalarParallelAsync_WithoutOptions_ShouldUseDefaults()
    {
        var queries = new[] { "SELECT 1", "SELECT 2" };
        var commandTimeouts = new List<int>();

        var results = await queries.ExecuteScalarParallelAsync<int>(() => new TestDbConnection(cmd =>
        {
            commandTimeouts.Add(cmd.CommandTimeout);
            return 42;
        }));

        results.Count.ShouldBe(2);
        commandTimeouts.ShouldAllBe(timeout => timeout == 30); // Default timeout
    }
}