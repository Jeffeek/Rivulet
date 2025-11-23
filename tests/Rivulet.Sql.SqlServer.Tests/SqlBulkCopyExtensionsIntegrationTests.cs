using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using System.Data;

namespace Rivulet.Sql.SqlServer.Tests;

/// <summary>
/// Integration tests for SqlBulkCopyExtensions using Testcontainers.
/// Uses per-test-class container for isolation (IAsyncLifetime).
/// For shared containers across test classes, use [Collection("SqlServer")] with SqlServerFixture.
/// </summary>
[Trait("Category", "Integration")]
public class SqlBulkCopyExtensionsIntegrationTests : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private string? _connectionString;

    private record TestRecord(int Id, string Name, string Email);

    public async Task InitializeAsync()
    {
        _container = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();

        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        // Create test table
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE TestTable (
                Id INT NOT NULL,
                Name NVARCHAR(100) NOT NULL,
                Email NVARCHAR(100) NOT NULL
            )";
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    private SqlConnection CreateConnection() => new(_connectionString!);

    private static DataTable MapToDataTable(IEnumerable<TestRecord> records)
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("Email", typeof(string));

        foreach (var record in records)
        {
            table.Rows.Add(record.Id, record.Name, record.Email);
        }

        return table;
    }

    [Fact]
    public async Task BulkInsertUsingSqlBulkCopyAsync_WithValidData_ShouldInsertRecords()
    {
        // Arrange
        var records = new[]
        {
            new TestRecord(1, "Alice", "alice@example.com"),
            new TestRecord(2, "Bob", "bob@example.com"),
            new TestRecord(3, "Charlie", "charlie@example.com")
        };

        // Act
        await records.BulkInsertUsingSqlBulkCopyAsync(
            CreateConnection,
            "TestTable",
            MapToDataTable,
            batchSize: 2);

        // Assert
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM TestTable";
        var count = (int)(await command.ExecuteScalarAsync())!;

        count.Should().Be(3);
    }

    [Fact]
    public async Task BulkInsertUsingSqlBulkCopyAsync_WithLargeBatch_ShouldInsertAllRecords()
    {
        // Arrange
        var records = Enumerable.Range(1, 10)
            .Select(i => new TestRecord(i, $"User{i}", $"user{i}@example.com"))
            .ToArray();

        // Act
        await records.BulkInsertUsingSqlBulkCopyAsync(
            CreateConnection,
            "TestTable",
            MapToDataTable,
            batchSize: 3); // Will create 4 batches (3+3+3+1)

        // Assert
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM TestTable";
        var count = (int)(await command.ExecuteScalarAsync())!;

        count.Should().Be(10);
    }

    [Fact]
    public async Task BulkInsertUsingSqlBulkCopyAsync_WithColumnMappings_ShouldInsertRecords()
    {
        // Arrange
        var records = new[]
        {
            new TestRecord(100, "Dave", "dave@example.com"),
            new TestRecord(101, "Eve", "eve@example.com")
        };

        var columnMappings = new Dictionary<string, string>
        {
            ["Id"] = "Id",
            ["Name"] = "Name",
            ["Email"] = "Email"
        };

        // Act
        await records.BulkInsertUsingSqlBulkCopyAsync(
            CreateConnection,
            "TestTable",
            MapToDataTable,
            columnMappings,
            batchSize: 10);

        // Assert
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM TestTable WHERE Id >= 100";
        var count = (int)(await command.ExecuteScalarAsync())!;

        count.Should().Be(2);
    }

    [Fact]
    public async Task BulkInsertUsingSqlBulkCopyAsync_WithNullConnectionFactoryResult_ShouldThrow()
    {
        var records = new[] { new TestRecord(1, "Test", "test@example.com") };

        var act = async () => await records.BulkInsertUsingSqlBulkCopyAsync(
            () => null!,
            "TestTable",
            MapToDataTable);

        // ForEachParallelAsync cancels the operation when an exception occurs,
        // so we expect OperationCanceledException
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task BulkInsertUsingSqlBulkCopyAsync_WithInvalidTableName_ShouldThrow()
    {
        var records = new[] { new TestRecord(1, "Test", "test@example.com") };

        var act = async () => await records.BulkInsertUsingSqlBulkCopyAsync(
            CreateConnection,
            "NonExistentTable",
            MapToDataTable);

        // ForEachParallelAsync cancels the operation when an exception occurs
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task BulkInsertUsingSqlBulkCopyAsync_WithColumnMappings_WithNullConnectionFactoryResult_ShouldThrow()
    {
        var records = new[] { new TestRecord(1, "Test", "test@example.com") };
        var mappings = new Dictionary<string, string> { ["Id"] = "Id" };

        var act = async () => await records.BulkInsertUsingSqlBulkCopyAsync(
            () => null!,
            "TestTable",
            MapToDataTable,
            mappings);

        // ForEachParallelAsync cancels the operation when an exception occurs
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task BulkInsertUsingSqlBulkCopyAsync_WithCustomBulkCopyOptions_ShouldWork()
    {
        // Arrange
        var records = new[]
        {
            new TestRecord(200, "Frank", "frank@example.com")
        };

        // Act
        await records.BulkInsertUsingSqlBulkCopyAsync(
            CreateConnection,
            "TestTable",
            MapToDataTable,
            bulkCopyOptions: SqlBulkCopyOptions.Default,
            batchSize: 1000,
            bulkCopyTimeout: 60);

        // Assert
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Name FROM TestTable WHERE Id = 200";
        var name = (string)(await command.ExecuteScalarAsync())!;

        name.Should().Be("Frank");
    }

    [Fact]
    public async Task BulkInsertUsingSqlBulkCopyAsync_DataReader_WithValidData_ShouldInsertRecords()
    {
        // Arrange
        var rows = new List<Dictionary<string, object>>
        {
            new() { ["Id"] = 300, ["Name"] = "Grace", ["Email"] = "grace@example.com" },
            new() { ["Id"] = 301, ["Name"] = "Hank", ["Email"] = "hank@example.com" }
        };
        var readers = new[] { new Rivulet.Sql.Tests.TestDataReader(rows) };

        // Act
        await readers.BulkInsertUsingSqlBulkCopyAsync(
            CreateConnection,
            "TestTable");

        // Assert
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM TestTable WHERE Id >= 300";
        var count = (int)(await command.ExecuteScalarAsync())!;

        count.Should().Be(2);
    }

    [Fact]
    public async Task BulkInsertUsingSqlBulkCopyAsync_DataReader_WithNullConnectionFactoryResult_ShouldThrow()
    {
        var rows = new List<Dictionary<string, object>>
        {
            new() { ["Id"] = 1, ["Name"] = "Test" }
        };
        var readers = new[] { new Rivulet.Sql.Tests.TestDataReader(rows) };

        var act = async () => await readers.BulkInsertUsingSqlBulkCopyAsync(
            () => null!,
            "TestTable");

        // ForEachParallelAsync cancels the operation when an exception occurs
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task BulkInsertUsingSqlBulkCopyAsync_DataReader_WithNullReader_ShouldThrow()
    {
        // Use non-nullable IDataReader collection with OfType to filter nulls at runtime
        var readers = new IDataReader[] { new Rivulet.Sql.Tests.TestDataReader([]) }
            .Select(_ => (IDataReader?)null!)
            .Where(r => r == null!); // This will fail when enumerated

        var act = async () => await readers.BulkInsertUsingSqlBulkCopyAsync(
            CreateConnection,
            "TestTable");

        // ForEachParallelAsync cancels the operation when an exception occurs
        // (in this case, NullReferenceException or InvalidOperationException from null reader)
        await act.Should().ThrowAsync<Exception>();
    }
}
