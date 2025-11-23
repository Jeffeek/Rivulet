# Rivulet.Sql

**Safe parallel SQL operations with connection pooling awareness and bulk operations.**

Built on top of Rivulet.Core, this package provides SQL-aware parallel operators that automatically handle transient database failures, respect connection pooling limits, and support efficient bulk operations.

## Installation

```bash
dotnet add package Rivulet.Sql
```

Requires `Rivulet.Core` (automatically included).

## Quick Start

### Parallel SQL Queries

Execute multiple queries in parallel with automatic retry for transient SQL errors:

```csharp
using Rivulet.Sql;
using System.Data.SqlClient;

var userIds = new[] { 1, 2, 3, 4, 5 };
var queries = userIds.Select(id => $"SELECT * FROM Users WHERE Id = {id}");

var results = await queries.ExecuteQueriesParallelAsync(
    () => new SqlConnection(connectionString),
    reader =>
    {
        var users = new List<User>();
        while (reader.Read())
        {
            users.Add(new User
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Email = reader.GetString(2)
            });
        }
        return users;
    },
    new SqlOptions
    {
        CommandTimeout = 30,
        ParallelOptions = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 10,
            MaxRetries = 3
        }
    });

foreach (var userList in results)
{
    foreach (var user in userList)
    {
        Console.WriteLine($"{user.Id}: {user.Name}");
    }
}
```

### Parameterized Queries

Use parameters to prevent SQL injection:

```csharp
var userIds = new[] { 1, 2, 3 };
var queriesWithParams = userIds.Select(id => (
    query: "SELECT * FROM Users WHERE Id = @id",
    configureParams: (Action<IDbCommand>)((cmd) =>
    {
        var param = cmd.CreateParameter();
        param.ParameterName = "@id";
        param.Value = id;
        cmd.Parameters.Add(param);
    })
));

var results = await queriesWithParams.ExecuteQueriesParallelAsync(
    () => new SqlConnection(connectionString),
    reader =>
    {
        var user = new User();
        if (reader.Read())
        {
            user.Id = reader.GetInt32(0);
            user.Name = reader.GetString(1);
        }
        return user;
    });
```

### Parallel SQL Commands

Execute multiple INSERT, UPDATE, or DELETE commands in parallel:

```csharp
var updates = new[]
{
    "UPDATE Users SET LastLogin = GETDATE() WHERE Id = 1",
    "UPDATE Users SET LastLogin = GETDATE() WHERE Id = 2",
    "UPDATE Users SET LastLogin = GETDATE() WHERE Id = 3"
};

var affectedRows = await updates.ExecuteCommandsParallelAsync(
    () => new SqlConnection(connectionString),
    new SqlOptions
    {
        ParallelOptions = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 5,
            ErrorMode = ErrorMode.CollectAndContinue
        }
    });

Console.WriteLine($"Total rows affected: {affectedRows.Sum()}");
```

### Parallel Scalar Queries

Execute scalar queries (COUNT, MAX, MIN, etc.) in parallel:

```csharp
var tableNames = new[] { "Users", "Products", "Orders" };
var queries = tableNames.Select(table => $"SELECT COUNT(*) FROM {table}");

var counts = await queries.ExecuteScalarParallelAsync<int>(
    () => new SqlConnection(connectionString));

for (int i = 0; i < tableNames.Length; i++)
{
    Console.WriteLine($"{tableNames[i]}: {counts[i]} rows");
}
```

## Bulk Operations

### Bulk Insert

Efficiently insert thousands of records using batched operations:

```csharp
var users = Enumerable.Range(1, 10000)
    .Select(i => new User { Name = $"User{i}", Email = $"user{i}@example.com" })
    .ToList();

var totalInserted = await users.BulkInsertAsync(
    () => new SqlConnection(connectionString),
    async (batch, cmd, ct) =>
    {
        var sb = new StringBuilder();
        int paramIndex = 0;

        foreach (var user in batch)
        {
            if (sb.Length > 0) sb.Append("; ");
            sb.Append($"INSERT INTO Users (Name, Email) VALUES (@name{paramIndex}, @email{paramIndex})");

            var nameParam = cmd.CreateParameter();
            nameParam.ParameterName = $"@name{paramIndex}";
            nameParam.Value = user.Name;
            cmd.Parameters.Add(nameParam);

            var emailParam = cmd.CreateParameter();
            emailParam.ParameterName = $"@email{paramIndex}";
            emailParam.Value = user.Email;
            cmd.Parameters.Add(emailParam);

            paramIndex++;
        }

        cmd.CommandText = sb.ToString();
        await Task.CompletedTask;
    },
    new BulkOperationOptions
    {
        BatchSize = 1000,
        UseTransaction = true,
        SqlOptions = new SqlOptions
        {
            ParallelOptions = new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 4
            }
        }
    });

Console.WriteLine($"Inserted {totalInserted} users");
```

### Bulk Update

Update multiple records efficiently:

```csharp
var users = await GetUsersToUpdate();

var totalUpdated = await users.BulkUpdateAsync(
    () => new SqlConnection(connectionString),
    async (batch, cmd, ct) =>
    {
        var sb = new StringBuilder();
        int paramIndex = 0;

        foreach (var user in batch)
        {
            if (sb.Length > 0) sb.Append("; ");
            sb.Append($"UPDATE Users SET Name = @name{paramIndex}, Email = @email{paramIndex} WHERE Id = @id{paramIndex}");

            var idParam = cmd.CreateParameter();
            idParam.ParameterName = $"@id{paramIndex}";
            idParam.Value = user.Id;
            cmd.Parameters.Add(idParam);

            var nameParam = cmd.CreateParameter();
            nameParam.ParameterName = $"@name{paramIndex}";
            nameParam.Value = user.Name;
            cmd.Parameters.Add(nameParam);

            var emailParam = cmd.CreateParameter();
            emailParam.ParameterName = $"@email{paramIndex}";
            emailParam.Value = user.Email;
            cmd.Parameters.Add(emailParam);

            paramIndex++;
        }

        cmd.CommandText = sb.ToString();
        await Task.CompletedTask;
    },
    new BulkOperationOptions
    {
        BatchSize = 500,
        UseTransaction = true
    });
```

### Bulk Delete

Delete multiple records in batches:

```csharp
var userIdsToDelete = await GetInactiveUserIds();

var totalDeleted = await userIdsToDelete.BulkDeleteAsync(
    () => new SqlConnection(connectionString),
    async (batch, cmd, ct) =>
    {
        cmd.CommandText = $"DELETE FROM Users WHERE Id IN ({string.Join(",", batch)})";
        await Task.CompletedTask;
    },
    new BulkOperationOptions
    {
        BatchSize = 1000,
        UseTransaction = true
    });

Console.WriteLine($"Deleted {totalDeleted} inactive users");
```

## Automatic Retry Handling

Rivulet.Sql automatically retries transient SQL errors:

### SQL Server Transient Errors
- **-2, -1**: Connection timeout/broken
- **53**: Connection does not exist
- **64**: Error on server
- **40197, 40501, 40613**: Azure SQL transient errors

### PostgreSQL (Npgsql) Transient Errors
- **08000-08006**: Connection exceptions
- **53300**: Too many connections
- **57P03**: Cannot connect now

### MySQL Transient Errors
- **1040**: Too many connections
- **1205**: Lock wait timeout
- **1213**: Deadlock found
- **2006, 2013**: Server gone away/lost connection

```csharp
var options = new SqlOptions
{
    ParallelOptions = new ParallelOptionsRivulet
    {
        MaxRetries = 5,
        BaseDelay = TimeSpan.FromMilliseconds(100),
        BackoffStrategy = BackoffStrategy.ExponentialJitter
    },
    OnSqlErrorAsync = (item, exception, retryAttempt) =>
    {
        Console.WriteLine($"SQL error on retry {retryAttempt}: {exception.Message}");
        return ValueTask.CompletedTask;
    }
};

var results = await queries.ExecuteQueriesParallelAsync(
    () => new SqlConnection(connectionString),
    reader => MapToUser(reader),
    options);
```

## Connection Pool Management

Rivulet.Sql is designed to work with ADO.NET connection pooling:

```csharp
// Connection string with pooling configuration
var connectionString = "Server=localhost;Database=MyDb;User Id=sa;Password=***;" +
                      "Max Pool Size=100;Min Pool Size=10;";

var options = new SqlOptions
{
    AutoManageConnection = true,  // Automatically opens and closes connections
    ParallelOptions = new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 20  // Don't exceed connection pool size
    }
};

// The factory function creates new connections that participate in pooling
var results = await queries.ExecuteQueriesParallelAsync(
    () => new SqlConnection(connectionString),
    reader => MapToUser(reader),
    options);
```

**Best Practice**: Set `MaxDegreeOfParallelism` to be less than your connection pool's `Max Pool Size` to avoid pool exhaustion.

## Batch Operation Callbacks

Monitor bulk operation progress:

```csharp
var totalProcessed = 0;
var options = new BulkOperationOptions
{
    BatchSize = 1000,
    OnBatchStartAsync = (batch, batchNum) =>
    {
        Console.WriteLine($"Starting batch {batchNum} with {batch.Count} items");
        return ValueTask.CompletedTask;
    },
    OnBatchCompleteAsync = (batch, batchNum, affectedRows) =>
    {
        totalProcessed += affectedRows;
        Console.WriteLine($"Batch {batchNum} complete: {affectedRows} rows affected");
        Console.WriteLine($"Total processed so far: {totalProcessed}");
        return ValueTask.CompletedTask;
    },
    OnBatchErrorAsync = (batch, batchNum, exception) =>
    {
        Console.WriteLine($"Batch {batchNum} failed: {exception.Message}");
        return ValueTask.CompletedTask;
    }
};

await items.BulkInsertAsync(
    () => new SqlConnection(connectionString),
    BuildInsertCommand,
    options);
```

## Advanced Features

### Transaction Isolation Levels

Control transaction isolation for bulk operations:

```csharp
var options = new BulkOperationOptions
{
    UseTransaction = true,
    SqlOptions = new SqlOptions
    {
        IsolationLevel = IsolationLevel.Serializable  // Highest isolation
    }
};

await users.BulkInsertAsync(
    () => new SqlConnection(connectionString),
    BuildInsertCommand,
    options);
```

### Custom Command Timeout

Set per-operation timeouts:

```csharp
var options = new SqlOptions
{
    CommandTimeout = 120,  // 2 minutes for long-running queries
    ParallelOptions = new ParallelOptionsRivulet
    {
        PerItemTimeout = TimeSpan.FromSeconds(130)  // Overall timeout per item
    }
};
```

### Provider-Agnostic Code

Works with any ADO.NET provider (SQL Server, PostgreSQL, MySQL, SQLite, etc.):

```csharp
// SQL Server
var results1 = await queries.ExecuteQueriesParallelAsync(
    () => new SqlConnection(sqlServerConnectionString),
    MapToUser);

// PostgreSQL
var results2 = await queries.ExecuteQueriesParallelAsync(
    () => new NpgsqlConnection(postgresConnectionString),
    MapToUser);

// MySQL
var results3 = await queries.ExecuteQueriesParallelAsync(
    () => new MySqlConnection(mysqlConnectionString),
    MapToUser);
```

## Configuration Options

### SqlOptions

SQL-specific configuration:

```csharp
var options = new SqlOptions
{
    CommandTimeout = 30,              // Command timeout in seconds
    AutoManageConnection = true,       // Auto open/close connections
    IsolationLevel = IsolationLevel.ReadCommitted,  // Transaction isolation
    OnSqlErrorAsync = async (item, ex, retry) => { /* custom logging */ },
    ParallelOptions = new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 10,
        MaxRetries = 3,
        BaseDelay = TimeSpan.FromMilliseconds(100),
        BackoffStrategy = BackoffStrategy.ExponentialJitter,
        ErrorMode = ErrorMode.CollectAndContinue
    }
};
```

### BulkOperationOptions

Bulk operation configuration:

```csharp
var options = new BulkOperationOptions
{
    BatchSize = 1000,                  // Items per batch
    UseTransaction = true,              // Wrap each batch in transaction
    SqlOptions = new SqlOptions { /* ... */ },
    OnBatchStartAsync = async (batch, num) => { /* ... */ },
    OnBatchCompleteAsync = async (batch, num, affected) => { /* ... */ },
    OnBatchErrorAsync = async (batch, num, ex) => { /* ... */ }
};
```

## Best Practices

1. **Use Parameterized Queries**: Always use parameters to prevent SQL injection
2. **Set Appropriate Parallelism**: Match `MaxDegreeOfParallelism` to your connection pool size
3. **Enable AutoManageConnection**: Let Rivulet handle connection lifecycle unless you have specific needs
4. **Use Transactions for Bulk Operations**: Enable `UseTransaction = true` for data consistency
5. **Monitor Progress**: Use callbacks for long-running bulk operations
6. **Tune Batch Size**: Experiment with batch sizes (100-2000) for optimal performance
7. **Handle Provider Differences**: Be aware of provider-specific SQL syntax and error codes

## Performance

Rivulet.Sql is designed for high-throughput database operations:

- **Connection Pooling Aware**: Respects connection pool limits to avoid exhaustion
- **Batched Operations**: Reduces round-trips for bulk operations
- **Bounded Concurrency**: Prevents overwhelming the database
- **Automatic Retries**: Handles transient failures without manual intervention
- **Zero Allocations**: Uses `ValueTask<T>` in hot paths

## Examples

See the [samples directory](../../samples) for complete working examples including:

- Parallel report generation from multiple queries
- Bulk data migration between databases
- ETL pipelines with SQL sources
- Database maintenance operations

## Multi-Database Support

Works seamlessly with:

- **SQL Server** (`System.Data.SqlClient`, `Microsoft.Data.SqlClient`)
- **PostgreSQL** (`Npgsql`)
- **MySQL** (`MySql.Data`, `MySqlConnector`)
- **SQLite** (`System.Data.SQLite`, `Microsoft.Data.Sqlite`)
- **Oracle** (`Oracle.ManagedDataAccess`)
- Any ADO.NET provider implementing `IDbConnection`

## License

MIT License - see [LICENSE](../../LICENSE.txt) for details.
