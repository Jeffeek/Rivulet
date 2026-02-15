using Testcontainers.MsSql;

namespace Rivulet.Sql.SqlServer.Tests;

/// <summary>
///     Shared SQL Server container fixture for all integration tests.
///     Reuses the same container across test classes to improve performance.
/// </summary>
internal abstract class SqlServerFixture : IAsyncLifetime
{
    private MsSqlContainer? _container;

    public async ValueTask InitializeAsync()
    {
        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();

        await _container.StartAsync();
        _container.GetConnectionString();
    }

    public ValueTask DisposeAsync() => _container?.DisposeAsync() ?? ValueTask.CompletedTask;
}

/// <summary>
///     Collection definition for SQL Server integration tests.
///     All test classes marked with [Collection(TestCollections.SqlServer)] will share the same container.
/// </summary>
[CollectionDefinition(TestCollections.SqlServer)]
public class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    // This class is never instantiated - it's just a marker for xUnit
}
