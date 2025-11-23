# Rivulet.Sql.SqlServer

SQL Server-specific optimizations for Rivulet.Sql including SqlBulkCopy integration for **10-100x faster bulk inserts**.

## Features

- **SqlBulkCopy Integration**: Ultra-high performance bulk inserts (50,000+ rows/sec)
- **Parallel Bulk Operations**: Process multiple batches in parallel
- **Automatic Column Mapping**: Maps DataTable columns to SQL Server table columns
- **Custom Column Mappings**: Support for explicit source-to-destination column mappings
- **DataReader Support**: Bulk insert from IDataReader sources
- **Configurable Batching**: Control batch size and timeout settings

## Installation

```bash
dotnet add package Rivulet.Sql.SqlServer
```

## Usage

### Basic SqlBulkCopy

```csharp
using Rivulet.Sql.SqlServer;

var users = GetUsers(); // IEnumerable<User>

await users.BulkInsertUsingSqlBulkCopyAsync(
    () => new SqlConnection(connectionString),
    "Users",
    batch => MapToDataTable(batch), // Convert to DataTable
    options: new ParallelOptionsRivulet { MaxDegreeOfParallelism = 4 },
    batchSize: 5000
);

// Helper method to map objects to DataTable
DataTable MapToDataTable(IEnumerable<User> users)
{
    var table = new DataTable();
    table.Columns.Add("Id", typeof(int));
    table.Columns.Add("Name", typeof(string));
    table.Columns.Add("Email", typeof(string));

    foreach (var user in users)
    {
        table.Rows.Add(user.Id, user.Name, user.Email);
    }

    return table;
}
```

### Custom Column Mappings

```csharp
var columnMappings = new Dictionary<string, string>
{
    ["UserId"] = "Id",
    ["FullName"] = "Name",
    ["EmailAddress"] = "Email"
};

await users.BulkInsertUsingSqlBulkCopyAsync(
    () => new SqlConnection(connectionString),
    "Users",
    batch => MapToDataTable(batch),
    columnMappings,
    options: new ParallelOptionsRivulet { MaxDegreeOfParallelism = 4 }
);
```

### DataReader Bulk Insert

```csharp
var readers = GetDataReaders(); // IEnumerable<IDataReader>

await readers.BulkInsertUsingSqlBulkCopyAsync(
    () => new SqlConnection(connectionString),
    "Users",
    options: new ParallelOptionsRivulet { MaxDegreeOfParallelism = 2 },
    batchSize: 10000
);
```

### Advanced Options

```csharp
await users.BulkInsertUsingSqlBulkCopyAsync(
    () => new SqlConnection(connectionString),
    "Users",
    batch => MapToDataTable(batch),
    options: new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 4,
        ErrorMode = ErrorMode.CollectAndContinue
    },
    bulkCopyOptions: SqlBulkCopyOptions.KeepIdentity | SqlBulkCopyOptions.CheckConstraints,
    batchSize: 5000,
    bulkCopyTimeout: 60
);
```

## Performance

### Comparison: Standard Insert vs SqlBulkCopy

| Method | Rows/sec | Time for 100k rows |
|--------|----------|-------------------|
| Standard Batched Insert | ~1,000 | ~100 seconds |
| SqlBulkCopy | ~50,000+ | ~2 seconds |
| **Performance Gain** | **50x faster** | **50x faster** |

### Recommended Settings

- **Batch Size**: 5,000-10,000 rows per batch
- **Max Parallelism**: 2-4 for most workloads
- **Timeout**: 30-60 seconds depending on data size

## When to Use

✅ **Use Rivulet.Sql.SqlServer when:**
- Inserting 1,000+ rows
- Maximum performance is critical
- Using SQL Server exclusively

❌ **Use base Rivulet.Sql when:**
- Multi-database support needed
- Smaller datasets (<1,000 rows)
- Cross-platform compatibility required

## Requirements

- .NET 8.0 or .NET 9.0
- SQL Server 2012 or later
- Microsoft.Data.SqlClient

## License

MIT License - see [LICENSE](../../LICENSE) for details.
