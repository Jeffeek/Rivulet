using Npgsql;
using Testcontainers.PostgreSql;

namespace Rivulet.Sql.PostgreSql.Tests;

/// <summary>
///     Shared PostgreSQL container for the integration test class. Started once per class (not per test method)
///     and reused across <c>dotnet test</c> runs when Testcontainers reuse is enabled locally
///     (<c>testcontainers.reuse.enable=true</c> in <c>~/.testcontainers.properties</c>).
///     Requires Docker Desktop to be running.
/// </summary>
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:15.20")
        .WithReuse(true)
        .Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        // IF NOT EXISTS so a reused container (table already created in a prior run) does not fail
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
                              CREATE TABLE IF NOT EXISTS "TestTable" (
                                  "Id" INT NOT NULL,
                                  "Name" VARCHAR(100) NOT NULL,
                                  "Email" VARCHAR(100) NOT NULL
                              )
                              """;
        await command.ExecuteNonQueryAsync();
    }

    // Do not dispose the container: with reuse enabled it is kept alive for the next run.
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
///     Integration tests for PostgreSqlCopyExtensions using a shared Testcontainers PostgreSQL container.
///     Each test truncates TestTable first for isolation.
/// </summary>
[Trait("Category", "Integration")]
public sealed class PostgreSqlCopyExtensionsIntegrationTests(PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    private readonly string _connectionString = fixture.ConnectionString;

    // Runs per test method: reset shared table so tests are isolated despite sharing one container.
    public async ValueTask InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "TRUNCATE TABLE \"TestTable\"";
        await command.ExecuteNonQueryAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private NpgsqlConnection CreateConnection() => new(_connectionString);

    private static object?[] MapToRow(TestRecord record) =>
        [record.Id, record.Name, record.Email];

    [Fact]
    public async Task BulkInsertUsingCopyAsync_WithValidData_ShouldInsertRecords()
    {
        // Arrange
        var records = new[]
        {
            new TestRecord(1, "Alice", "alice@example.com"),
            new TestRecord(2, "Bob", "bob@example.com"),
            new TestRecord(3, "Charlie", "charlie@example.com")
        };
        var columns = new[] { "Id", "Name", "Email" };

        // Act
        await records.BulkInsertUsingCopyAsync(
            CreateConnection,
            "TestTable",
            columns,
            MapToRow,
            new() { MaxDegreeOfParallelism = 1 },
            2,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM \"TestTable\"";
        var count = Convert.ToInt64(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));

        count.ShouldBe(3);
    }

    [Fact]
    public async Task BulkInsertUsingCopyAsync_WithLargeBatch_ShouldInsertAllRecords()
    {
        // Arrange
        var records = Enumerable.Range(1, 10)
            .Select(static i => new TestRecord(i, $"User{i}", $"user{i}@example.com"))
            .ToArray();
        var columns = new[] { "Id", "Name", "Email" };

        // Act
        await records.BulkInsertUsingCopyAsync(
            CreateConnection,
            "TestTable",
            columns,
            MapToRow,
            batchSize: 3,
            cancellationToken: TestContext.Current.CancellationToken); // Will create 4 batches (3+3+3+1)

        // Assert
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM \"TestTable\"";
        var count = Convert.ToInt64(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));

        count.ShouldBe(10);
    }

    [Fact]
    public async Task BulkInsertUsingCopyAsync_WithNullConnectionFactoryResult_ShouldThrow()
    {
        var records = new[] { new TestRecord(1, "Test", "test@example.com") };
        var columns = new[] { "Id", "Name", "Email" };

        var act = () => records.BulkInsertUsingCopyAsync(static () => null!,
            "TestTable",
            columns,
            MapToRow,
            cancellationToken: TestContext.Current.CancellationToken);

        // Should throw the actual exception (null reference or InvalidOperationException)
        await act.ShouldThrowAsync<Exception>();
    }

    [Fact]
    public async Task BulkInsertUsingCopyAsync_WithInvalidTableName_ShouldThrow()
    {
        var records = new[] { new TestRecord(1, "Test", "test@example.com") };
        var columns = new[] { "Id", "Name", "Email" };

        var act = () => records.BulkInsertUsingCopyAsync(
            CreateConnection,
            "NonExistentTable",
            columns,
            MapToRow,
            cancellationToken: TestContext.Current.CancellationToken);

        // Should throw PostgresException for invalid table name
        await act.ShouldThrowAsync<Exception>();
    }

    [Fact]
    public async Task BulkInsertUsingCopyCsvAsync_WithValidData_ShouldInsertRecords()
    {
        // Arrange
        var csvLines = new[] { "100,Dave,dave@example.com", "101,Eve,eve@example.com" };
        var columns = new[] { "Id", "Name", "Email" };

        // Act
        await csvLines.BulkInsertUsingCopyCsvAsync(
            CreateConnection,
            "TestTable",
            columns,
            batchSize: 1000,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM \"TestTable\" WHERE \"Id\" >=100";
        var count = Convert.ToInt64(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));

        count.ShouldBe(2);
    }

    [Fact]
    public async Task BulkInsertUsingCopyCsvAsync_WithLargeBatch_ShouldInsertAllRecords()
    {
        // Arrange
        var csvLines = Enumerable.Range(200, 10)
            .Select(static i => $"{i},User{i},user{i}@example.com")
            .ToArray();
        var columns = new[] { "Id", "Name", "Email" };

        // Act
        await csvLines.BulkInsertUsingCopyCsvAsync(
            CreateConnection,
            "TestTable",
            columns,
            batchSize: 3,
            cancellationToken: TestContext.Current.CancellationToken); // Will create 4 batches (3+3+3+1)

        // Assert
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM \"TestTable\" WHERE \"Id\" >=200";
        var count = Convert.ToInt64(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));

        count.ShouldBe(10);
    }

    [Fact]
    public async Task BulkInsertUsingCopyCsvAsync_WithCustomDelimiter_ShouldWork()
    {
        // Arrange
        var csvLines = new[] { "300|Frank|frank@example.com", "301|Grace|grace@example.com" };
        var columns = new[] { "Id", "Name", "Email" };

        // Act
        await csvLines.BulkInsertUsingCopyCsvAsync(
            CreateConnection,
            "TestTable",
            columns,
            delimiter: '|',
            batchSize: 1000,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM \"TestTable\" WHERE \"Id\" >=300";
        var count = Convert.ToInt64(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));

        count.ShouldBe(2);
    }

    [Fact]
    public async Task BulkInsertUsingCopyCsvAsync_WithNullConnectionFactoryResult_ShouldThrow()
    {
        var csvLines = new[] { "1,Test,test@example.com" };
        var columns = new[] { "Id", "Name", "Email" };

        var act = () => csvLines.BulkInsertUsingCopyCsvAsync(static () => null!,
            "TestTable",
            columns,
            cancellationToken: TestContext.Current.CancellationToken);

        // Should throw the actual exception (null reference or InvalidOperationException)
        await act.ShouldThrowAsync<Exception>();
    }

    [Fact]
    public async Task BulkInsertUsingCopyTextAsync_WithValidData_ShouldInsertRecords()
    {
        // Arrange - PostgreSQL text format is tab-delimited by default
        var textLines = new[] { "400\tHank\thank@example.com", "401\tIvy\tivy@example.com" };
        var columns = new[] { "Id", "Name", "Email" };

        // Act
        await textLines.BulkInsertUsingCopyTextAsync(
            CreateConnection,
            "TestTable",
            columns,
            batchSize: 1000,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM \"TestTable\" WHERE \"Id\" >=400";
        var count = Convert.ToInt64(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));

        count.ShouldBe(2);
    }

    [Fact]
    public async Task BulkInsertUsingCopyTextAsync_WithLargeBatch_ShouldInsertAllRecords()
    {
        // Arrange
        var textLines = Enumerable.Range(500, 10)
            .Select(static i => $"{i}\tUser{i}\tuser{i}@example.com")
            .ToArray();
        var columns = new[] { "Id", "Name", "Email" };

        // Act
        await textLines.BulkInsertUsingCopyTextAsync(
            CreateConnection,
            "TestTable",
            columns,
            batchSize: 3,
            cancellationToken: TestContext.Current.CancellationToken); // Will create 4 batches (3+3+3+1)

        // Assert
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM \"TestTable\" WHERE \"Id\" >=500";
        var count = Convert.ToInt64(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));

        count.ShouldBe(10);
    }

    [Fact]
    public async Task BulkInsertUsingCopyTextAsync_WithNullConnectionFactoryResult_ShouldThrow()
    {
        var textLines = new[] { "1\tTest\ttest@example.com" };
        var columns = new[] { "Id", "Name", "Email" };

        var act = () => textLines.BulkInsertUsingCopyTextAsync(static () => null!,
            "TestTable",
            columns,
            cancellationToken: TestContext.Current.CancellationToken);

        // Should throw the actual exception (null reference or InvalidOperationException)
        await act.ShouldThrowAsync<Exception>();
    }

    private sealed record TestRecord(int Id, string Name, string Email);
}
