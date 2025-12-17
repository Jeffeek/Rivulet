using System.Data;
using Microsoft.Data.SqlClient;
using Rivulet.Base.Tests;
using Testcontainers.MsSql;

namespace Rivulet.Sql.SqlServer.Tests;

/// <summary>
///     Integration tests for SqlBulkCopyExtensions using Testcontainers.
///     Uses per-test-class container for isolation (IAsyncLifetime).
///     For shared containers across test classes, use [Collection(TestCollections.SqlServer)] with SqlServerFixture.
/// </summary>
[Trait("Category", "Integration")]
public class SqlBulkCopyExtensionsIntegrationTests : IAsyncLifetime
{
    private string? _connectionString;
    private MsSqlContainer? _container;

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
        command.CommandText = """

                                          CREATE TABLE TestTable (
                                              Id INT NOT NULL,
                                              Name NVARCHAR(100) NOT NULL,
                                              Email NVARCHAR(100) NOT NULL
                                          )
                              """;
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container != null) await _container.DisposeAsync();
    }

    private SqlConnection CreateConnection() => new(_connectionString!);

    private static DataTable MapToDataTable(IEnumerable<TestRecord> records)
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("Email", typeof(string));

        foreach (var record in records) table.Rows.Add(record.Id, record.Name, record.Email);

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

        count.ShouldBe(3, "all 3 records (Alice, Bob, Charlie) should be inserted successfully");
    }

    [Fact]
    public async Task BulkInsertUsingSqlBulkCopyAsync_WithLargeBatch_ShouldInsertAllRecords()
    {
        // Arrange
        var records = Enumerable.Range(1, 10)
            .Select(static i => new TestRecord(i, $"User{i}", $"user{i}@example.com"))
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

        count.ShouldBe(10, "all 10 User records should be inserted in 4 batches (3+3+3+1)");
    }

    [Fact]
    public async Task BulkInsertUsingSqlBulkCopyAsync_WithColumnMappings_ShouldInsertRecords()
    {
        // Arrange
        var records = new[] { new TestRecord(100, "Dave", "dave@example.com"), new TestRecord(101, "Eve", "eve@example.com") };

        var columnMappings = new Dictionary<string, string> { ["Id"] = "Id", ["Name"] = "Name", ["Email"] = "Email" };

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

        count.ShouldBe(2, "Dave and Eve records (IDs 100-101) should be inserted with explicit column mappings");
    }

    [Fact]
    public async Task BulkInsertUsingSqlBulkCopyAsync_WithNullConnectionFactoryResult_ShouldThrow()
    {
        var records = new[] { new TestRecord(1, "Test", "test@example.com") };

        await Should.ThrowAsync<OperationCanceledException>(((Func<Task>?)Act)!);
        return;

        Task Act() =>
            records.BulkInsertUsingSqlBulkCopyAsync(static () => null!,
                "TestTable",
                MapToDataTable);
    }

    [Fact]
    public async Task BulkInsertUsingSqlBulkCopyAsync_WithInvalidTableName_ShouldThrow()
    {
        var records = new[] { new TestRecord(1, "Test", "test@example.com") };

        await Should.ThrowAsync<OperationCanceledException>(((Func<Task>?)Act)!);
        return;

        Task Act() =>
            records.BulkInsertUsingSqlBulkCopyAsync(CreateConnection,
                "NonExistentTable",
                MapToDataTable);
    }

    [Fact]
    public async Task BulkInsertUsingSqlBulkCopyAsync_WithColumnMappings_WithNullConnectionFactoryResult_ShouldThrow()
    {
        var records = new[] { new TestRecord(1, "Test", "test@example.com") };
        var mappings = new Dictionary<string, string> { ["Id"] = "Id" };

        await Should.ThrowAsync<OperationCanceledException>(((Func<Task>?)Act)!);
        return;

        Task Act() =>
            records.BulkInsertUsingSqlBulkCopyAsync(static () => null!,
                "TestTable",
                MapToDataTable,
                mappings);
    }

    [Fact]
    public async Task BulkInsertUsingSqlBulkCopyAsync_WithCustomBulkCopyOptions_ShouldWork()
    {
        // Arrange
        var records = new[] { new TestRecord(200, "Frank", "frank@example.com") };

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

        name.ShouldBe("Frank", "Frank record (ID 200) should be inserted with custom bulk copy options and timeout");
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
        var readers = new[] { new TestDataReader(rows) };

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

        count.ShouldBe(2, "Grace and Hank records (IDs 300-301) should be inserted from IDataReader");
    }

    [Fact]
    public async Task BulkInsertUsingSqlBulkCopyAsync_DataReader_WithNullConnectionFactoryResult_ShouldThrow()
    {
        var rows = new List<Dictionary<string, object>> { new() { ["Id"] = 1, ["Name"] = "Test" } };
        var readers = new[] { new TestDataReader(rows) };

        await Should.ThrowAsync<OperationCanceledException>((Func<Task>)Act);
        return;

        Task Act() =>
            readers.BulkInsertUsingSqlBulkCopyAsync(static () => null!,
                "TestTable");
    }

    [Fact]
    public async Task BulkInsertUsingSqlBulkCopyAsync_DataReader_WithNullReader_ShouldThrow()
    {
        // Use non-nullable IDataReader collection with OfType to filter nulls at runtime
        var readers = new IDataReader[] { new TestDataReader([]) }
            .Select(static _ => (IDataReader?)null!)
            .Where(static r => r == null!); // This will fail when enumerated

        await Should.ThrowAsync<Exception>(((Func<Task>?)Act)!);
        return;

        Task Act() =>
            readers.BulkInsertUsingSqlBulkCopyAsync(CreateConnection,
                "TestTable");
    }

    private sealed record TestRecord(int Id, string Name, string Email);
}