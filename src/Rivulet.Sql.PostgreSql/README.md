# Rivulet.Sql.PostgreSql

PostgreSQL-specific optimizations for Rivulet.Sql including COPY command integration for **10-100x faster bulk inserts**.

## Features

- **COPY Command Integration**: Ultra-high performance bulk inserts using COPY
- **Multiple Formats**: Binary, CSV, and text formats supported
- **Parallel Operations**: Process multiple batches in parallel
- **Streaming Import**: Efficient memory usage with streaming
- **Custom Delimiters**: Support for CSV with custom delimiters
- **Header Support**: Handle CSV files with headers

## Installation

```bash
dotnet add package Rivulet.Sql.PostgreSql
```

## Usage

### Binary COPY (Fastest)

```csharp
using Npgsql;
using Rivulet.Sql.PostgreSql;

var users = GetUsers(); // IEnumerable<User>

await users.BulkInsertUsingCopyAsync(
    () => new NpgsqlConnection(connectionString),
    "users",
    columns: new[] { "id", "name", "email" },
    mapToRow: user => new object?[] { user.Id, user.Name, user.Email },
    options: new ParallelOptionsRivulet { MaxDegreeOfParallelism = 4 },
    batchSize: 5000
);
```

### CSV COPY

```csharp
var csvLines = File.ReadLines("users.csv");

await csvLines.BulkInsertUsingCopyCsvAsync(
    () => new NpgsqlConnection(connectionString),
    "users",
    columns: new[] { "id", "name", "email" },
    options: new ParallelOptionsRivulet { MaxDegreeOfParallelism = 2 },
    hasHeader: true,
    delimiter: ','
);
```

### Text COPY (Tab-Delimited)

```csharp
var textLines = File.ReadLines("users.txt");

await textLines.BulkInsertUsingCopyTextAsync(
    () => new NpgsqlConnection(connectionString),
    "users",
    columns: new[] { "id", "name", "email" },
    options: new ParallelOptionsRivulet { MaxDegreeOfParallelism = 4 },
    batchSize: 10000
);
```

### Advanced: Custom Object Mapping

```csharp
record Product(int Id, string Name, decimal Price, DateTime CreatedAt);

var products = GetProducts();

await products.BulkInsertUsingCopyAsync(
    () => new NpgsqlConnection(connectionString),
    "products",
    columns: new[] { "id", "name", "price", "created_at" },
    mapToRow: product => new object?[]
    {
        product.Id,
        product.Name,
        product.Price,
        product.CreatedAt
    },
    options: new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 4,
        ErrorMode = ErrorMode.CollectAndContinue
    }
);
```

## Performance

### Comparison: Standard Insert vs COPY

| Method | Rows/sec | Time for 100k rows |
|--------|----------|-------------------|
| Standard Batched Insert | ~1,000 | ~100 seconds |
| COPY Binary | ~50,000+ | ~2 seconds |
| **Performance Gain** | **50x faster** | **50x faster** |

### Format Performance

| Format | Speed | Memory Usage | Use Case |
|--------|-------|--------------|----------|
| Binary | Fastest | Low | Typed data, best performance |
| CSV | Fast | Low | CSV files, text data |
| Text | Fast | Low | Tab-delimited data |

### Recommended Settings

- **Batch Size**: 5,000-10,000 rows per batch
- **Max Parallelism**: 2-4 for most workloads
- **Format**: Binary for best performance

## When to Use

✅ **Use Rivulet.Sql.PostgreSql when:**
- Inserting 1,000+ rows
- Maximum performance is critical
- Using PostgreSQL exclusively

❌ **Use base Rivulet.Sql when:**
- Multi-database support needed
- Smaller datasets (<1,000 rows)
- Cross-platform compatibility required

## Requirements

- .NET 8.0 or .NET 9.0
- PostgreSQL 10 or later
- Npgsql

## License

MIT License - see LICENSE file for details

---

**Made with ❤️ by Jeffeek** | [NuGet](https://www.nuget.org/packages/Sql.PostgreSql/) | [GitHub](https://github.com/Jeffeek/Rivulet)
