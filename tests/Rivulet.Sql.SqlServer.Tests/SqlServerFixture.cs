using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Rivulet.Sql.SqlServer.Tests;

/// <summary>
/// Shared SQL Server container fixture for all integration tests.
/// Reuses the same container across test classes to improve performance.
/// </summary>
public class SqlServerFixture : IAsyncLifetime
{
    private MsSqlContainer? _container;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        _container = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();

        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    public SqlConnection CreateConnection() => new(ConnectionString);
}

/// <summary>
/// Collection definition for SQL Server integration tests.
/// All test classes marked with [Collection("SqlServer")] will share the same container.
/// </summary>
[CollectionDefinition("SqlServer")]
public class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    // This class is never instantiated - it's just a marker for xUnit
}
