# Rivulet.Csv

<!-- DESCRIPTION_START -->
**Parallel CSV parsing and writing with CsvHelper integration, bounded concurrency, and batching support for high-throughput data processing.**
<!-- DESCRIPTION_END -->

Built on top of Rivulet.Core and CsvHelper, this package provides CSV-aware parallel operators that enable safe and efficient parallel processing of multiple CSV files with automatic error handling, progress tracking, and configurable CSV operations.

## Installation

```bash
dotnet add package Rivulet.Csv
```

Requires `Rivulet.Core` (automatically included) and `CsvHelper` (automatically included).

<!-- KEY_FEATURES_START -->
## Features

- CsvHelper v34 source-generated mappings with `[CsvRecord]`
- Multi-type operations (2-5 generic type parameters)
- Memory-efficient streaming with IAsyncEnumerable
- Per-file CSV reader and writer options
- Progress tracking and lifecycle callbacks
- Error handling modes (FailFast, CollectAndContinue, BestEffort)
- Circuit breaker and retry support
- Ordered and unordered output options
<!-- KEY_FEATURES_END -->

<!-- FEATURES_START -->
## API

- **ParseCsvParallelAsync** - Parse multiple CSV files in parallel and return all records as a flattened list
- **ParseCsvParallelGroupedAsync** - Parse multiple CSV file groups in parallel with per-file configuration, returning records grouped by file path
- **ParseCsvParallelGroupedAsync (Multi-Type)** - Parse 2-5 different record types concurrently with grouped results
- **WriteCsvParallelAsync** - Write collections of records to multiple CSV files in parallel with per-file configuration
- **WriteCsvParallelAsync (Multi-Type)** - Write 2-5 different record types to separate files concurrently
- **TransformCsvParallelAsync** - Transform CSV files in parallel applying sync or async transformation functions
<!-- FEATURES_END -->

## Quick Start

### Parallel CSV Parsing

Parse multiple CSV files in parallel with bounded concurrency. Record types must be `[CsvRecord]` and `partial`:

```csharp
using Rivulet.Csv;

[CsvRecord]
internal sealed partial class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
}

var csvFiles = new[]
{
    "data/products-2023.csv",
    "data/products-2024.csv",
    "data/products-2025.csv"
};

var allProducts = await csvFiles.ParseCsvParallelAsync<Product>(
    new CsvOperationOptions
    {
        ParallelOptions = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4
        }
    });

// allProducts is a flattened list of all products from all files
Console.WriteLine($"Loaded {allProducts.Count} products total");
```

### Parallel CSV Writing

Write collections of records to multiple CSV files in parallel:

```csharp
var productsPerRegion = new Dictionary<string, List<Product>>
{
    ["north"] = northProducts,
    ["south"] = southProducts,
    ["east"] = eastProducts,
    ["west"] = westProducts
};

var fileWrites = productsPerRegion.Select(kvp =>
    new RivuletCsvWriteFile<Product>($"output/products-{kvp.Key}.csv", kvp.Value, null));

await fileWrites.WriteCsvParallelAsync(
    new CsvOperationOptions
    {
        CreateDirectoriesIfNotExist = true,
        OverwriteExisting = true
    });
```

### CSV Transformation Pipeline

Parse CSV files, transform the data, and write to new files:

```csharp
[CsvRecord]
internal sealed partial class EnrichedProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal PriceWithTax { get; set; }
    public DateTime Timestamp { get; set; }
}

var transformations = new[]
{
    (
        Input: new RivuletCsvReadFile<Product>("input/raw-products.csv", null),
        Output: new RivuletCsvWriteFile<EnrichedProduct>("output/processed-products.csv", Array.Empty<EnrichedProduct>(), null)
    )
};

await transformations.TransformCsvParallelAsync<Product, EnrichedProduct>(
    p => new EnrichedProduct
    {
        Id = p.Id,
        Name = p.Name,
        Price = p.Price,
        PriceWithTax = p.Price * 1.2m,
        Timestamp = DateTime.UtcNow
    },
    new CsvOperationOptions
    {
        ParallelOptions = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 8,
            MaxRetries = 3
        }
    });
```

## Source-Generated Mapping

CsvHelper v34 uses source-generated mappings. Every record type passed to a read or write operation must be annotated with `[CsvRecord]` and declared `partial`. Use `[CsvColumn]` to rename or index columns, `[CsvFormat]` to control supported parsing formats, and `[CsvIgnore]` to exclude members. Mark input columns optional with `[CsvColumn(Optional = true)]`. Runtime `ClassMap` registration is no longer supported.

```csharp
[CsvRecord]
internal sealed partial class Product
{
    [CsvColumn("product_id")]
    public int Id { get; set; }

    [CsvColumn("product_name")]
    public string Name { get; set; } = string.Empty;

    [CsvFormat("0.00")]
    public decimal Price { get; set; }

    [CsvIgnore]
    public string? InternalNote { get; set; }
}
```

Record mapping is declared on the type and applies to every file using that record type. Per-file options customize CSV syntax:

```csharp
var fileReads = new[]
{
    new RivuletCsvReadFile<Product>("products.csv", new CsvFileConfiguration
    {
        ReaderConfigurationAction = static options => options with
        {
            Separator = ';',
            TrimWhitespace = true
        }
    })
};

var fileWrites = new[]
{
    new RivuletCsvWriteFile<Product>("products.tsv", products, new CsvFileConfiguration
    {
        WriterConfigurationAction = static options => options with
        {
            Separator = '\t',
            HasHeader = false
        }
    })
};

await fileReads.ParseCsvParallelGroupedAsync();
await fileWrites.WriteCsvParallelAsync();
```

`CsvReaderOptions` and `CsvWriterOptions` are immutable records; return a modified copy with `with`. Configure `CsvOperationOptions.Culture` for value parsing/formatting and `Encoding` for text encoding. `BufferSize` controls the underlying file stream buffer.

## CSV Options

```csharp
var options = new CsvOperationOptions
{
    Culture = CultureInfo.InvariantCulture,
    Encoding = Encoding.UTF8,
    BufferSize = 81920,
    FileConfiguration = new CsvFileConfiguration
    {
        ReaderConfigurationAction = static reader => reader with
        {
            Separator = ';',
            TrimWhitespace = true,
            SkipEmptyLines = true
        },
        WriterConfigurationAction = static writer => writer with
        {
            Separator = '\t',
            HasHeader = true
        }
    },
    CreateDirectoriesIfNotExist = true,
    OverwriteExisting = false,
    ParallelOptions = new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 8,
        MaxRetries = 3,
        ErrorMode = ErrorMode.CollectAndContinue
    }
};
```

## Core Features

### CsvHelper Integration

Rivulet.Csv uses CsvHelper v34. Configure parsing and writing independently with immutable options:

```csharp
var configuration = new CsvFileConfiguration
{
    ReaderConfigurationAction = static reader => reader with
    {
        Separator = ';',
        TrimWhitespace = true
    },
    WriterConfigurationAction = static writer => writer with
    {
        Separator = '\t',
        HasHeader = true
    }
};
```

Required columns missing from input cause `KeyNotFoundException`. Use `[CsvColumn(Optional = true)]` for optional input fields. Column names and formats belong on `[CsvRecord]` record types, not per-file options.

### Within-File Parallelism (CsvHelper 34)

CsvHelper 34 adds parallel APIs for single-file operations. Enable with `CsvHelperParallelism`:

```csharp
// Process 4 files in parallel, each using 2 threads for parsing
var results = await files.ParseCsvParallelAsync<Product>(
    new CsvOperationOptions
    {
        ParallelOptions = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 4 }, // across files
        FileConfiguration = new CsvFileConfiguration { CsvHelperParallelism = 2 }     // within each file
    });
```

**Two levels of parallelism:**
- **Rivulet** (`MaxDegreeOfParallelism`): process N files concurrently, each on one thread
- **CsvHelper** (`CsvHelperParallelism`): parse/format records in parallel within each file

Set `CsvHelperParallelism` to `-1` for one thread per processor, or a positive value for a specific count. Omit (default) for sequential within-file processing. Use when parsing/formatting is CPU-bound on large files.

### Progress Tracking

Monitor CSV processing progress:

```csharp
var options = new CsvOperationOptions
{
    ParallelOptions = new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 4,
        Progress = new ProgressOptions
        {
            ReportInterval = TimeSpan.FromSeconds(2),
            OnProgress = progress =>
            {
                Console.WriteLine($"Processed: {progress.ItemsCompleted}/{progress.TotalItems} files");
                Console.WriteLine($"Rate: {progress.ItemsPerSecond:F1} files/sec");
                Console.WriteLine($"ETA: {progress.EstimatedTimeRemaining}");
                return ValueTask.CompletedTask;
            }
        }
    }
};

await csvFiles.ParseCsvParallelAsync<Product>(options);
```

### Error Handling and Retries

Robust error handling with automatic retries:

```csharp
var options = new CsvOperationOptions
{
    ParallelOptions = new ParallelOptionsRivulet
    {
        MaxRetries = 3,
        BackoffStrategy = BackoffStrategy.ExponentialJitter,
        ErrorMode = ErrorMode.CollectAndContinue, // Continue on errors

        OnErrorAsync = async (index, ex) =>
        {
            Console.WriteLine($"Error processing file {index}: {ex.Message}");
            await LogErrorAsync(ex);
        }
    },

    // Lifecycle callbacks
    OnFileStartAsync = async filePath =>
    {
        Console.WriteLine($"Starting: {filePath}");
        await Task.CompletedTask;
    },

    OnFileCompleteAsync = async (filePath, result) =>
    {
        Console.WriteLine($"Completed: {filePath} ({result.RecordCount} records)");
        await Task.CompletedTask;
    },

    OnFileErrorAsync = async (filePath, ex) =>
    {
        Console.WriteLine($"Failed: {filePath} - {ex.Message}");
        await Task.CompletedTask;
    }
};

try
{
    var results = await csvFiles.ParseCsvParallelAsync<Product>(options);
}
catch (AggregateException ex)
{
    // Handle collected errors
    foreach (var innerEx in ex.InnerExceptions)
    {
        Console.WriteLine($"Error: {innerEx.Message}");
    }
}
```

## Advanced Features

### Circuit Breaker Pattern

Protect against cascading failures:

```csharp
var options = new CsvOperationOptions
{
    ParallelOptions = new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 8,
        CircuitBreaker = new CircuitBreakerOptions
        {
            FailureThreshold = 5,              // Open after 5 consecutive failures
            SuccessThreshold = 2,               // Close after 2 consecutive successes
            OpenTimeout = TimeSpan.FromSeconds(30), // Test recovery after 30 seconds
            OnStateChange = async (from, to) =>
            {
                Console.WriteLine($"Circuit breaker: {from} → {to}");
                await Task.CompletedTask;
            }
        }
    }
};

await csvFiles.ParseCsvParallelAsync<Product>(options);
```

### Rate Limiting

Control the rate of file processing:

```csharp
var options = new CsvOperationOptions
{
    ParallelOptions = new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 16,
        RateLimit = new RateLimitOptions
        {
            TokensPerSecond = 10,    // Process 10 files per second
            BurstCapacity = 20       // Allow brief bursts
        }
    }
};

await csvFiles.ParseCsvParallelAsync<Product>(options);
```

### Adaptive Concurrency

Automatically adjust parallelism based on performance:

```csharp
var options = new CsvOperationOptions
{
    ParallelOptions = new ParallelOptionsRivulet
    {
        AdaptiveConcurrency = new AdaptiveConcurrencyOptions
        {
            MinConcurrency = 1,
            MaxConcurrency = 32,
            InitialConcurrency = 8,
            TargetLatency = TimeSpan.FromSeconds(2), // Target 2 sec per file
            MinSuccessRate = 0.95,                    // 95% success rate
            OnConcurrencyChange = async (old, current) =>
            {
                Console.WriteLine($"Concurrency: {old} → {current}");
                await Task.CompletedTask;
            }
        }
    }
};

await csvFiles.ParseCsvParallelAsync<Product>(options);
```

## Common Use Cases

### ETL Pipeline

```csharp
// Extract: Parse CSV files from multiple sources
var sourceFiles = Directory.GetFiles("input", "*.csv");
var allRecords = await sourceFiles.ParseCsvParallelAsync<RawRecord>();

// Transform: Process and enrich data
var enrichedRecords = allRecords
    .SelectMany(records => records)
    .Select(record => EnrichRecord(record))
    .ToList();

// Load: Write to database in batches
await enrichedRecords.BatchParallelAsync(
    batchSize: 1000,
    async (batch, ct) =>
    {
        await database.BulkInsertAsync(batch, ct);
        return batch.Count;
    },
    new ParallelOptionsRivulet { MaxDegreeOfParallelism = 4 });
```

### Data Migration

```csharp
// Migrate data from old format to new format
var migrationTasks = Directory.GetFiles("legacy-data", "*.csv")
    .Select(oldFile => (oldFile, $"migrated-data/{Path.GetFileName(oldFile)}"));

var transformations = migrationTasks.Select(pair =>
    (
        Input: new RivuletCsvReadFile<OldFormat>(pair.oldFile, null),
        Output: new RivuletCsvWriteFile<NewFormat>(pair.Item2, Array.Empty<NewFormat>(), null)
    ));

await transformations.TransformCsvParallelAsync<OldFormat, NewFormat>(
    old => new NewFormat
    {
        Id = old.LegacyId,
        Name = old.CustomerName,
        Email = old.EmailAddress,
        CreatedDate = old.RegistrationDate
    },
    new CsvOperationOptions
    {
        ParallelOptions = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 8,
            Progress = new ProgressOptions
            {
                ReportInterval = TimeSpan.FromSeconds(5),
                OnProgress = progress =>
                {
                    Console.WriteLine($"Migrated: {progress.PercentComplete:F1}%");
                    return ValueTask.CompletedTask;
                }
            }
        }
    });
```

### Report Generation

```csharp
// Generate reports from CSV data - note: TransformCsvParallelAsync operates per-record,
// so for aggregations like this, it's better to read, transform in memory, then write
var reportTasks = new[]
{
    (source: "sales-Q1.csv", report: "reports/Q1-summary.csv"),
    (source: "sales-Q2.csv", report: "reports/Q2-summary.csv"),
    (source: "sales-Q3.csv", report: "reports/Q3-summary.csv"),
    (source: "sales-Q4.csv", report: "reports/Q4-summary.csv")
};

// Read all sales records
var salesByQuarter = await reportTasks
    .Select(task => task.source)
    .ToArray()
    .ParseCsvParallelAsync<SalesRecord>();

// Transform to summaries (this would need proper grouping logic per file)
var summaries = salesByQuarter
    .GroupBy(r => r.ProductCategory)
    .Select(g => new SalesSummary
    {
        Category = g.Key,
        TotalSales = g.Sum(r => r.Amount),
        AveragePrice = g.Average(r => r.Price),
        TransactionCount = g.Count()
    })
    .ToList();

// Write summaries
var writes = new[] { new RivuletCsvWriteFile<SalesSummary>("reports/summary.csv", summaries, null) };
await writes.WriteCsvParallelAsync();
```

## Performance Considerations

### Optimal Parallelism

- **For I/O-bound CSV operations** (reading from disk/network): Use `MaxDegreeOfParallelism` between 8-32
- **For CPU-bound transformations**: Use `MaxDegreeOfParallelism` equal to `Environment.ProcessorCount`
- **For mixed workloads**: Start with 8 and use adaptive concurrency

### Memory Management

- Large CSV files are read entirely into memory by default
- For very large files, consider processing in batches or use streaming approaches

### Benchmarking

Run performance benchmarks with different concurrency levels:

```csharp
using System.Diagnostics;

var concurrencyLevels = new[] { 1, 2, 4, 8, 16, 32 };

foreach (var concurrency in concurrencyLevels)
{
    var sw = Stopwatch.StartNew();

    await csvFiles.ParseCsvParallelAsync<Product>(
        new CsvOperationOptions
        {
            ParallelOptions = new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = concurrency
            }
        });

    sw.Stop();
    Console.WriteLine($"Concurrency {concurrency}: {sw.ElapsedMilliseconds}ms");
}
```

## Multi-Type Operations

Process multiple CSV file groups of different types concurrently. Supports 2-5 generic type parameters for maximum flexibility.

### Reading Multiple Types Concurrently

```csharp
// Read products and customers concurrently
var productFiles = new[] { new RivuletCsvReadFile<Product>("products.csv", null) };
var customerFiles = new[] { new RivuletCsvReadFile<Customer>("customers.csv", null) };

var (products, customers) = await CsvParallelExtensions.ParseCsvParallelGroupedAsync(
    productFiles,
    customerFiles);

// Results are grouped by file path
foreach (var (filePath, productList) in products)
    Console.WriteLine($"{filePath}: {productList.Count} products");

foreach (var (filePath, customerList) in customers)
    Console.WriteLine($"{filePath}: {customerList.Count} customers");
```

### Writing Multiple Types Concurrently

```csharp
// Write products and customers concurrently
var productWrites = new[] { new RivuletCsvWriteFile<Product>("output/products.csv", products, null) };
var customerWrites = new[] { new RivuletCsvWriteFile<Customer>("output/customers.csv", customers, null) };

await CsvParallelExtensions.WriteCsvParallelAsync(
    productWrites,
    customerWrites);
```

### Up to 5 Types Supported

```csharp
// Read 5 different entity types concurrently
var (products, customers, orders, categories, suppliers) =
    await CsvParallelExtensions.ParseCsvParallelGroupedAsync(
        productFiles,
        customerFiles,
        orderFiles,
        categoryFiles,
        supplierFiles);

// Write 5 different entity types concurrently
await CsvParallelExtensions.WriteCsvParallelAsync(
    productWrites,
    customerWrites,
    orderWrites,
    categoryWrites,
    supplierWrites);
```

## API Reference

### Extension Methods

#### Single-Type Operations

**`ParseCsvParallelAsync<T>`**
- Parse multiple CSV files in parallel
- Returns: `Task<IReadOnlyList<T>>` - Flattened list of all records from all files

**`ParseCsvParallelGroupedAsync<T>`**
- Parse multiple CSV files in parallel, preserving file source
- Returns: `Task<IReadOnlyDictionary<string, IReadOnlyList<T>>>` - Records grouped by file path

**`WriteCsvParallelAsync<T>`**
- Write collections of records to multiple CSV files in parallel
- Returns: `Task`

**`TransformCsvParallelAsync<TIn, TOut>`**
- Parse, transform, and write CSV files in parallel
- Returns: `Task`

#### Multi-Type Operations (2-5 Types)

**`ParseCsvParallelGroupedAsync<T1, T2>`** (and 3, 4, 5 type variants)
- Parse multiple CSV file groups of different types concurrently
- Returns: Tuple of dictionaries, one per type

**`WriteCsvParallelAsync<T1, T2>`** (and 3, 4, 5 type variants)
- Write multiple CSV file groups of different types concurrently
- Returns: `Task`

### Options Classes

**`CsvOperationOptions`**
- `Culture`, `FileConfiguration`
- `Encoding`, `BufferSize`
- `CreateDirectoriesIfNotExist`, `OverwriteExisting`
- `ParallelOptions`
- `OnFileStartAsync`, `OnFileCompleteAsync`, `OnFileErrorAsync`

**`CsvFileConfiguration`**
- `ReaderConfigurationAction` and `WriterConfigurationAction` - Transform immutable CsvHelper options with `with` expressions

CSV record types must be annotated with `[CsvRecord]` and declared `partial`. Use `[CsvColumn]`, `[CsvFormat]`, and `[CsvIgnore]` for generated mappings.

## See Also

- [CsvHelper Documentation](https://joshclose.github.io/CsvHelper/)
- [Rivulet.Core Documentation](../Rivulet.Core/README.md)
- [Rivulet.IO Documentation](../Rivulet.IO/README.md)

## Framework Support

- .NET 8.0
- .NET 9.0
- .NET 10.0

## Documentation & Source

- **GitHub Repository**: [https://github.com/Jeffeek/Rivulet](https://github.com/Jeffeek/Rivulet)
- **Report Issues**: [https://github.com/Jeffeek/Rivulet/issues](https://github.com/Jeffeek/Rivulet/issues)
- **License**: MIT

## License

MIT License - see LICENSE file for details

---

**Made with ❤️ by Jeffeek** | [NuGet](https://www.nuget.org/packages/Rivulet.Csv/) | [GitHub](https://github.com/Jeffeek/Rivulet)
