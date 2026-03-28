# Rivulet.Sql.MySql

<!-- DESCRIPTION_START -->
MySQL-specific optimizations for Rivulet.Sql using MySqlBulkLoader (LOAD DATA LOCAL INFILE) for **10-100x faster bulk inserts**.
<!-- DESCRIPTION_END -->

<!-- KEY_FEATURES_START -->
## Features

- **MySqlBulkLoader**: LOAD DATA LOCAL INFILE for maximum performance
- **File-based Loading**: Direct file import support
- **Parallel Operations**: Process multiple batches in parallel
- **Custom Delimiters**: Support for any field separator
- **Automatic Column Mapping**: Maps columns automatically
<!-- KEY_FEATURES_END -->

<!-- FEATURES_START -->
## API

- **BulkInsertUsingMySqlBulkLoaderAsync** - Parallel bulk insert from CSV strings using MySqlBulkLoader
- **BulkInsertFromFilesUsingMySqlBulkLoaderAsync** - Parallel bulk insert directly from CSV files
<!-- FEATURES_END -->

## Installation

```bash
dotnet add package Rivulet.Sql.MySql
```

## Usage

### Bulk Insert from CSV Strings

```csharp
using MySqlConnector;
using Rivulet.Sql.MySql;

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

### Bulk Insert from Files

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

### Advanced: Object-to-CSV Mapping

```csharp
record User(int Id, string Name, string Email);

var users = GetUsers();

// Convert objects to CSV lines with RFC 4180 quoting, then bulk load
static string CsvQuote(string field) =>
    field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r')
        ? $"\"{field.Replace("\"", "\"\"")}\"" : field;

var csvLines = users.Select(u => $"{u.Id},{CsvQuote(u.Name)},{CsvQuote(u.Email)}");

await csvLines.BulkInsertUsingMySqlBulkLoaderAsync(
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

### Comparison: Standard Insert vs MySqlBulkLoader

| Method | Rows/sec | Time for 100k rows |
|--------|----------|-------------------|
| Standard Batched Insert | ~1,000 | ~100 seconds |
| MySqlBulkLoader (LOAD DATA) | ~50,000+ | ~2 seconds |
| **Performance Gain** | **50x faster** | **50x faster** |

### Recommended Settings

- **Batch Size**: 5,000-10,000 rows per batch
- **Max Parallelism**: 2-4 for most workloads
- **File-based loading**: Best for files > 10MB or > 50,000 rows

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

## Framework Support

- .NET 8.0
- .NET 9.0

## Documentation & Source

- **GitHub Repository**: [https://github.com/Jeffeek/Rivulet](https://github.com/Jeffeek/Rivulet)
- **Report Issues**: [https://github.com/Jeffeek/Rivulet/issues](https://github.com/Jeffeek/Rivulet/issues)
- **License**: MIT

## License

MIT License - see LICENSE file for details

---

**Made with ❤️ by Jeffeek** | [NuGet](https://www.nuget.org/packages/Rivulet.Sql.MySql/) | [GitHub](https://github.com/Jeffeek/Rivulet)
