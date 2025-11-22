# Rivulet.Sql.MySql

MySQL-specific optimizations for Rivulet.Sql including MySqlBulkCopy and MySqlBulkLoader (LOAD DATA INFILE) integration for **10-100x faster bulk inserts**.

## Features

- **MySqlBulkCopy**: High-performance bulk inserts for in-memory data
- **MySqlBulkLoader**: LOAD DATA LOCAL INFILE for maximum performance with CSV data
- **File-based Loading**: Direct file import support
- **Parallel Operations**: Process multiple batches in parallel
- **Custom Delimiters**: Support for any field separator
- **Automatic Column Mapping**: Maps columns automatically

## Installation

```bash
dotnet add package Rivulet.Sql.MySql
```

## Usage

### MySqlBulkCopy (In-Memory Data)

```csharp
using MySqlConnector;
using Rivulet.Sql.MySql;

var rows = GetRows(); // IEnumerable<object?[]>

await rows.BulkInsertUsingMySqlBulkCopyAsync(
    () => new MySqlConnection(connectionString),
    "users",
    columnNames: new[] { "id", "name", "email" },
    options: new ParallelOptionsRivulet { MaxDegreeOfParallelism = 4 },
    batchSize: 5000
);
```

### MySqlBulkLoader (CSV Data)

```csharp
var csvLines = File.ReadLines("users.csv");

await csvLines.BulkInsertUsingMySqlBulkLoaderAsync(
    () => new MySqlConnection(connectionString),
    "users",
    columnNames: new[] { "id", "name", "email" },
    options: new ParallelOptionsRivulet { MaxDegreeOfParallelism = 2 },
    batchSize: 5000,
    fieldSeparator: ",",
    lineTerminator: "\n"
);
```

### File-Based Bulk Loading

```csharp
var csvFiles = Directory.GetFiles("data", "*.csv");

await csvFiles.BulkInsertFromFilesUsingMySqlBulkLoaderAsync(
    () => new MySqlConnection(connectionString),
    "users",
    columnNames: new[] { "id", "name", "email" },
    options: new ParallelOptionsRivulet { MaxDegreeOfParallelism = 4 },
    fieldSeparator: ",",
    lineTerminator: "\n"
);
```

### Advanced: Custom Data Mapping

```csharp
record User(int Id, string Name, string Email);

var users = GetUsers();

var rows = users.Select(u => new object?[] { u.Id, u.Name, u.Email });

await rows.BulkInsertUsingMySqlBulkCopyAsync(
    () => new MySqlConnection(connectionString),
    "users",
    columnNames: new[] { "id", "name", "email" },
    options: new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 4,
        ErrorMode = ErrorMode.CollectAndContinue
    }
);
```

## Performance

### Comparison: Standard Insert vs Bulk Operations

| Method | Rows/sec | Time for 100k rows |
|--------|----------|-------------------|
| Standard Batched Insert | ~1,000 | ~100 seconds |
| MySqlBulkCopy | ~40,000+ | ~2.5 seconds |
| MySqlBulkLoader (LOAD DATA) | ~50,000+ | ~2 seconds |
| **Performance Gain** | **40-50x faster** | **40-50x faster** |

### Method Selection Guide

| Method | Speed | Use Case |
|--------|-------|----------|
| MySqlBulkCopy | Very Fast | In-memory data, typed objects |
| MySqlBulkLoader | Fastest | CSV files, text data, very large datasets |

### Recommended Settings

- **Batch Size**: 5,000-10,000 rows per batch
- **Max Parallelism**: 2-4 for most workloads
- **MySqlBulkLoader**: Best for files > 10MB or > 50,000 rows

## When to Use

✅ **Use Rivulet.Sql.MySql when:**
- Inserting 1,000+ rows
- Maximum performance is critical
- Using MySQL or MariaDB exclusively
- Working with CSV files

❌ **Use base Rivulet.Sql when:**
- Multi-database support needed
- Smaller datasets (<1,000 rows)
- Cross-platform compatibility required

## Requirements

- .NET 8.0 or .NET 9.0
- MySQL 5.7 or later / MariaDB 10.2 or later
- MySqlConnector
- **Important**: MySQL server must have `local_infile=1` enabled for MySqlBulkLoader

## Configuration

To use MySqlBulkLoader, ensure your MySQL server has local file loading enabled:

```sql
SET GLOBAL local_infile = 1;
```

And in your connection string:

```csharp
"Server=localhost;Database=mydb;User=root;Password=pass;AllowLoadLocalInfile=true"
```

## Related Packages

- **Rivulet.Sql** - Provider-agnostic base package
- **Rivulet.Sql.SqlServer** - SqlBulkCopy integration for SQL Server
- **Rivulet.Sql.PostgreSql** - PostgreSQL COPY command integration

## License

MIT License - see [LICENSE](../../LICENSE) for details.
