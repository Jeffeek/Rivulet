using MySqlConnector;
using Testcontainers.MySql;

namespace Rivulet.Sql.MySql.Tests;

/// <summary>
///     Integration tests for MySqlBulkExtensions using Testcontainers.
///     Uses per-test-class container for isolation (IAsyncLifetime).
///     Requires Docker Desktop to be running.
/// </summary>
[Trait("Category", "Integration")]
public sealed class MySqlBulkExtensionsIntegrationTests : IAsyncLifetime
{
    private string? _connectionString;
    private MySqlContainer? _container;

    public async Task InitializeAsync()
    {
        _container = new MySqlBuilder()
            .WithImage("mysql:8.0")
            .WithCommand("--local-infile=1") // Enable LOAD DATA LOCAL INFILE
            .Build();

        await _container.StartAsync();

        // Add AllowLoadLocalInfile to connection string to enable client-side file loading
        var baseConnectionString = _container.GetConnectionString();
        _connectionString = $"{baseConnectionString};AllowLoadLocalInfile=true";

        // Create test table
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """

                                          CREATE TABLE TestTable (
                                              Id INT NOT NULL,
                                              Name VARCHAR(100) NOT NULL,
                                              Email VARCHAR(100) NOT NULL
                                          )
                              """;
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container != null) await _container.DisposeAsync();
    }

    private MySqlConnection CreateConnection() => new(_connectionString!);

    [Fact]
    public async Task BulkInsertUsingMySqlBulkLoaderAsync_WithValidData_ShouldInsertRecords()
    {
        // Arrange
        var csvLines = new[] { "1,Alice,alice@example.com", "2,Bob,bob@example.com", "3,Charlie,charlie@example.com" };
        var columnNames = new[] { "Id", "Name", "Email" };

        // Act
        await csvLines.BulkInsertUsingMySqlBulkLoaderAsync(
            CreateConnection,
            "TestTable",
            columnNames,
            batchSize: 2);

        // Assert
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM TestTable";
        var count = Convert.ToInt64(await command.ExecuteScalarAsync());

        count.ShouldBe(3);
    }

    [Fact]
    public async Task BulkInsertUsingMySqlBulkLoaderAsync_WithLargeBatch_ShouldInsertAllRecords()
    {
        // Arrange
        var csvLines = Enumerable.Range(1, 10)
            .Select(static i => $"{i},User{i},user{i}@example.com")
            .ToArray();
        var columnNames = new[] { "Id", "Name", "Email" };

        // Act
        await csvLines.BulkInsertUsingMySqlBulkLoaderAsync(
            CreateConnection,
            "TestTable",
            columnNames,
            batchSize: 3); // Will create 4 batches (3+3+3+1)

        // Assert
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM TestTable";
        var count = Convert.ToInt64(await command.ExecuteScalarAsync());

        count.ShouldBe(10);
    }

    [Fact]
    public async Task BulkInsertUsingMySqlBulkLoaderAsync_WithCustomSeparators_ShouldWork()
    {
        // Arrange
        var csvLines = new[] { "100|Dave|dave@example.com", "101|Eve|eve@example.com" };
        var columnNames = new[] { "Id", "Name", "Email" };

        // Act
        await csvLines.BulkInsertUsingMySqlBulkLoaderAsync(
            CreateConnection,
            "TestTable",
            columnNames,
            fieldSeparator: "|",
            batchSize: 1000);

        // Assert
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM TestTable WHERE Id >= 100";
        var count = Convert.ToInt64(await command.ExecuteScalarAsync());

        count.ShouldBe(2);
    }

    [Fact]
    public async Task BulkInsertUsingMySqlBulkLoaderAsync_WithNullConnectionFactoryResult_ShouldThrow()
    {
        var csvLines = new[] { "1,Test,test@example.com" };
        var columnNames = new[] { "Id", "Name", "Email" };

        var act = () => csvLines.BulkInsertUsingMySqlBulkLoaderAsync(static () => null!,
            "TestTable",
            columnNames);

        // ForEachParallelAsync cancels the operation when an exception occurs
        await act.ShouldThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task BulkInsertUsingMySqlBulkLoaderAsync_WithInvalidTableName_ShouldThrow()
    {
        var csvLines = new[] { "1,Test,test@example.com" };
        var columnNames = new[] { "Id", "Name", "Email" };

        var act = () => csvLines.BulkInsertUsingMySqlBulkLoaderAsync(
            CreateConnection,
            "NonExistentTable",
            columnNames);

        // ForEachParallelAsync cancels the operation when an exception occurs
        await act.ShouldThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task BulkInsertFromFilesUsingMySqlBulkLoaderAsync_WithValidFiles_ShouldInsertRecords()
    {
        // Arrange
        var tempFile1 = Path.GetTempFileName();
        var tempFile2 = Path.GetTempFileName();
        try
        {
            await File.WriteAllLinesAsync(tempFile1,
            [
                "200,Frank,frank@example.com",
                "201,Grace,grace@example.com"
            ]);
            await File.WriteAllLinesAsync(tempFile2,
            [
                "202,Hank,hank@example.com"
            ]);

            var filePaths = new[] { tempFile1, tempFile2 };
            var columnNames = new[] { "Id", "Name", "Email" };

            // Act
            await filePaths.BulkInsertFromFilesUsingMySqlBulkLoaderAsync(
                CreateConnection,
                "TestTable",
                columnNames);

            // Assert
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM TestTable WHERE Id >= 200";
            var count = Convert.ToInt64(await command.ExecuteScalarAsync());

            count.ShouldBe(3);
        }
        finally
        {
            if (File.Exists(tempFile1)) File.Delete(tempFile1);

            if (File.Exists(tempFile2)) File.Delete(tempFile2);
        }
    }

    [Fact]
    public async Task BulkInsertFromFilesUsingMySqlBulkLoaderAsync_WithNullConnectionFactoryResult_ShouldThrow()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllLinesAsync(tempFile, ["1,Test,test@example.com"]);
            var filePaths = new[] { tempFile };
            var columnNames = new[] { "Id", "Name", "Email" };

            var act = () => filePaths.BulkInsertFromFilesUsingMySqlBulkLoaderAsync(static () => null!,
                "TestTable",
                columnNames);

            // ForEachParallelAsync cancels the operation when an exception occurs
            await act.ShouldThrowAsync<OperationCanceledException>();
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task BulkInsertFromFilesUsingMySqlBulkLoaderAsync_WithNonExistentFile_ShouldThrow()
    {
        var filePaths = new[] { @"C:\NonExistent\File.csv" };
        var columnNames = new[] { "Id", "Name", "Email" };

        var act = () => filePaths.BulkInsertFromFilesUsingMySqlBulkLoaderAsync(
            CreateConnection,
            "TestTable",
            columnNames);

        // ForEachParallelAsync cancels the operation when an exception occurs
        await act.ShouldThrowAsync<Exception>();
    }
}