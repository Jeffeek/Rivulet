using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Rivulet.Sql.Tests;

public class SqlParallelExtensionsTests
{
    [Fact]
    public async Task ExecuteQueriesParallelAsync_WithValidQueries_ShouldReturnResults()
    {
        var queries = new[]
        {
            "SELECT * FROM Users WHERE Id = 1",
            "SELECT * FROM Users WHERE Id = 2",
            "SELECT * FROM Users WHERE Id = 3"
        };

        var results = await queries.ExecuteQueriesParallelAsync(
            () => new TestDbConnection(
                executeReaderFunc: _ => new TestDataReader([new() { ["Id"] = 1, ["Name"] = "User1" }])),
            reader =>
            {
                var items = new List<string>();
                while (reader.Read())
                {
                    items.Add(reader.GetString(1));
                }
                return items;
            });

        results.Should().HaveCount(3);
        results.Should().AllSatisfy(r => r.Should().ContainSingle());
    }

    [Fact]
    public async Task ExecuteQueriesParallelAsync_WithNullQueries_ShouldThrowArgumentNullException()
    {
        IEnumerable<string> queries = null!;

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await queries.ExecuteQueriesParallelAsync(
                () => new TestDbConnection(),
                _ => new List<string>()));
    }

    [Fact]
    public async Task ExecuteQueriesParallelAsync_WithNullConnectionFactory_ShouldThrowArgumentNullException()
    {
        var queries = new[] { "SELECT 1" };

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await queries.ExecuteQueriesParallelAsync(
                null!,
                _ => new List<string>()));
    }

    [Fact]
    public async Task ExecuteQueriesParallelAsync_WithNullReaderMapper_ShouldThrowArgumentNullException()
    {
        var queries = new[] { "SELECT 1" };

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await queries.ExecuteQueriesParallelAsync<string>(
                () => new TestDbConnection(),
                null!));
    }

    [Fact]
    public async Task ExecuteQueriesParallelAsync_WithParameters_ShouldConfigureCommand()
    {
        var commandsReceived = new List<string>();
        var queriesWithParams = new[]
        {
            (query: "SELECT * FROM Users WHERE Id = @id", configureParams: cmd =>
            {
                commandsReceived.Add(cmd.CommandText);
            }),
            (query: "SELECT * FROM Products WHERE Id = @id", configureParams: (Action<IDbCommand>?)(cmd =>
            {
                commandsReceived.Add(cmd.CommandText);
            }))
        };

        var results = await queriesWithParams.ExecuteQueriesParallelAsync(
            () => new TestDbConnection(
                executeReaderFunc: _ => new TestDataReader([new() { ["Id"] = 1 }])),
            _ => new List<int> { 1 });

        results.Should().HaveCount(2);
        commandsReceived.Should().HaveCount(2);
        commandsReceived.Should().Contain("SELECT * FROM Users WHERE Id = @id");
        commandsReceived.Should().Contain("SELECT * FROM Products WHERE Id = @id");
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

        var results = await commands.ExecuteCommandsParallelAsync(
            () => new TestDbConnection(executeNonQueryFunc: _ => 1));

        results.Should().HaveCount(3);
        results.Should().AllSatisfy(r => r.Should().Be(1));
    }

    [Fact]
    public async Task ExecuteCommandsParallelAsync_WithNullCommands_ShouldThrowArgumentNullException()
    {
        IEnumerable<string> commands = null!;

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await commands.ExecuteCommandsParallelAsync(() => new TestDbConnection()));
    }

    [Fact]
    public async Task ExecuteCommandsParallelAsync_WithParameters_ShouldConfigureCommand()
    {
        var commandsReceived = new List<string>();
        var commandsWithParams = new[]
        {
            (command: "UPDATE Users SET Name = @name WHERE Id = @id", configureParams: cmd =>
            {
                commandsReceived.Add(cmd.CommandText);
            }),
            (command: "DELETE FROM Users WHERE Id = @id", configureParams: (Action<IDbCommand>)(cmd =>
            {
                commandsReceived.Add(cmd.CommandText);
            }))
        };

        var results = await commandsWithParams.ExecuteCommandsParallelAsync(
            () => new TestDbConnection(executeNonQueryFunc: _ => 1));

        results.Should().HaveCount(2);
        commandsReceived.Should().HaveCount(2);
        commandsReceived.Should().Contain("UPDATE Users SET Name = @name WHERE Id = @id");
        commandsReceived.Should().Contain("DELETE FROM Users WHERE Id = @id");
    }

    [Fact]
    public async Task ExecuteScalarParallelAsync_WithValidQueries_ShouldReturnScalarValues()
    {
        var queries = new[]
        {
            "SELECT COUNT(*) FROM Users",
            "SELECT MAX(Id) FROM Users",
            "SELECT MIN(Id) FROM Users"
        };

        var values = new[] { 10, 100, 1 };
        var index = 0;

        var results = await queries.ExecuteScalarParallelAsync<int>(
            () => new TestDbConnection(executeScalarFunc: _ => values[index++ % values.Length]));

        results.Should().HaveCount(3);
        results.Should().Contain(10);
        results.Should().Contain(100);
        results.Should().Contain(1);
    }

    [Fact]
    public async Task ExecuteScalarParallelAsync_WithNullQueries_ShouldThrowArgumentNullException()
    {
        IEnumerable<string> queries = null!;

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await queries.ExecuteScalarParallelAsync<int>(() => new TestDbConnection()));
    }

    [Fact]
    public async Task ExecuteScalarParallelAsync_WithDBNull_ShouldReturnDefault()
    {
        var queries = new[] { "SELECT NULL" };

        var results = await queries.ExecuteScalarParallelAsync<int>(
            () => new TestDbConnection(executeScalarFunc: _ => DBNull.Value));

        results.Should().HaveCount(1);
        results[0].Should().Be(0);
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

        var results = await queriesWithParams.ExecuteScalarParallelAsync<int>(
            () => new TestDbConnection(executeScalarFunc: _ => 42));

        results.Should().HaveCount(1);
        results[0].Should().Be(42);
        commandsReceived.Should().ContainSingle();
    }

    [Fact]
    public async Task ExecuteQueriesParallelAsync_WithOptions_ShouldUseOptions()
    {
        var queries = new[] { "SELECT 1" };
        var options = new SqlOptions
        {
            CommandTimeout = 60,
            ParallelOptions = new()
            {
                MaxDegreeOfParallelism = 5
            }
        };

        var commandTimeout = 0;
        var results = await queries.ExecuteQueriesParallelAsync(
            () => new TestDbConnection(
                executeReaderFunc: cmd =>
                {
                    commandTimeout = cmd.CommandTimeout;
                    return new TestDataReader([]);
                }),
            _ => 1,
            options);

        commandTimeout.Should().Be(60);
        results.Should().HaveCount(1);
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
                if (attemptCount < 2)
                {
                    throw new TimeoutException("Timeout occurred");
                }

                return 1;
            }),
            new()
            {
                ParallelOptions = new()
                {
                    MaxRetries = 3,
                    BaseDelay = TimeSpan.FromMilliseconds(10)
                }
            });

        attemptCount.Should().Be(2);
        results.Should().HaveCount(1);
        results[0].Should().Be(1);
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
                    executeReaderFunc: _ => throw new InvalidOperationException("Test error")),
                _ => 1,
                new()
                {
                    OnSqlErrorAsync = (item, ex, retry) =>
                    {
                        callbackItem = item;
                        callbackException = ex;
                        callbackRetryAttempt = retry;
                        return ValueTask.CompletedTask;
                    },
                    ParallelOptions = new()
                    {
                        MaxRetries = 0
                    }
                }));

        callbackItem.Should().NotBeNull();
        callbackException.Should().NotBeNull();
        callbackException.Should().BeOfType<InvalidOperationException>();
        callbackRetryAttempt.Should().Be(0);
    }

    [Fact]
    [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
    public async Task ExecuteCommandsParallelAsync_WithCancellation_ShouldThrowOperationCanceledException()
    {
        var commands = Enumerable.Range(1, 100).Select(i => $"INSERT INTO Users (Id) VALUES ({i})");
        using var cts = new CancellationTokenSource();

        cts.CancelAfter(50);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await commands.ExecuteCommandsParallelAsync(
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
                    executeReaderFunc: _ => new TestDataReader([]));
                capturedConnection = conn;
                return conn;
            },
            _ => 1,
            new() { AutoManageConnection = true });

        results.Should().HaveCount(1);
        capturedConnection.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteCommandsParallelAsync_WithCustomParallelism_ShouldRespectConcurrencyLimit()
    {
        var commands = Enumerable.Range(1, 50).Select(i => $"INSERT INTO Users (Id) VALUES ({i})");
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

                lock (lockObj)
                {
                    currentConcurrent--;
                }

                return 1;
            }),
            new()
            {
                ParallelOptions = new()
                {
                    MaxDegreeOfParallelism = 5
                }
            });

        results.Should().HaveCount(50);
        maxConcurrent.Should().BeLessOrEqualTo(5);
    }
}
