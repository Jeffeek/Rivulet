# Rivulet.Csv

**Parallel CSV parsing and writing with CsvHelper integration, bounded concurrency, and batching support for high-throughput data processing.**

Built on top of Rivulet.Core and CsvHelper, this package provides CSV-aware parallel operators that enable safe and efficient parallel processing of multiple CSV files with automatic error handling, progress tracking, and configurable CSV operations.

## Installation

```bash
dotnet add package Rivulet.Csv
```

Requires `Rivulet.Core` (automatically included) and `CsvHelper` (automatically included).

## Quick Start

### Parallel CSV Parsing

Parse multiple CSV files in parallel with bounded concurrency:

```csharp
using Rivulet.Csv;

public class Product
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
        HasHeaderRecord = true,
        ParallelOptions = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4
        }
    });

// allProducts is a list of lists (one per file)
foreach (var fileProducts in allProducts)
{
    Console.WriteLine($"Loaded {fileProducts.Count} products");
}
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
    ($"output/products-{kvp.Key}.csv", (IEnumerable<Product>)kvp.Value));

await fileWrites.WriteCsvParallelAsync(
    new CsvOperationOptions
    {
        HasHeaderRecord = true,
        CreateDirectoriesIfNotExist = true,
        OverwriteExisting = true
    });
```

### CSV Transformation Pipeline

Parse CSV files, transform the data, and write to new files:

```csharp
var transformations = new[]
{
    ("input/raw-products.csv", "output/processed-products.csv"),
    ("input/raw-customers.csv", "output/processed-customers.csv")
};

await transformations.TransformCsvParallelAsync<Product, EnrichedProduct>(
    async (filePath, products) =>
    {
        // Transform each product
        return products.Select(p => new EnrichedProduct
        {
            Id = p.Id,
            Name = p.Name,
            Price = p.Price,
            PriceWithTax = p.Price * 1.2m,
            Timestamp = DateTime.UtcNow
        });
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

## ClassMap Support

Rivulet.Csv provides full integration with CsvHelper's ClassMap feature, enabling powerful column mapping scenarios. Use ClassMaps when you need to map by column index, handle optional fields, ignore properties, or apply custom type converters.

### Single ClassMap for All Files

Apply the same ClassMap to all files in a batch operation:

```csharp
public class ProductMap : ClassMap<Product>
{
    public ProductMap()
    {
        Map(m => m.Id).Name("ProductID");
        Map(m => m.Name).Name("ProductName");
        Map(m => m.Price).Index(2);
    }
}

// Parse all files using the same ClassMap
var results = await csvFiles.ParseCsvParallelAsync<Product, ProductMap>(
    new CsvOperationOptions
    {
        ParallelOptions = new() { MaxDegreeOfParallelism = 4 }
    });

// Write all files using the same ClassMap
var writes = new[]
{
    ("output1.csv", productsGroup1),
    ("output2.csv", productsGroup2)
};

await writes.WriteCsvParallelAsync<Product, ProductMap>(
    new CsvOperationOptions { OverwriteExisting = true });
```

### Per-File Configuration Based on File Path

Configure each file differently based on its path or characteristics:

```csharp
var files = new[] { "modern_format.csv", "legacy_format.csv" };

var results = await files.ParseCsvParallelAsync<Product>(
    filePath => ctx =>
    {
        if (filePath.Contains("legacy"))
        {
            // Legacy files use pipe delimiters and no header
            ctx.Configuration.Delimiter = "|";
            ctx.Configuration.HasHeaderRecord = false;
            ctx.RegisterClassMap<ProductMapByIndex>();
        }
        else
        {
            // Modern files use standard format
            ctx.RegisterClassMap<ProductMapByName>();
        }
    });
```

### Explicit Per-File Configuration (Your Complex Scenario)

Handle scenarios where you have multiple files with different schemas - like 5 files using 3 different ClassMaps:

```csharp
public class ProductMapByName : ClassMap<Product>
{
    public ProductMapByName()
    {
        Map(m => m.Id).Name("ProductID");
        Map(m => m.Name).Name("ProductName");
        Map(m => m.Price);
    }
}

public class ProductMapByIndex : ClassMap<Product>
{
    public ProductMapByIndex()
    {
        Map(m => m.Id).Index(0);
        Map(m => m.Name).Index(1);
        Map(m => m.Price).Index(2);
    }
}

public class ProductMapWithOptional : ClassMap<Product>
{
    public ProductMapWithOptional()
    {
        Map(m => m.Id).Name("ProductID");
        Map(m => m.Name).Name("ProductName");
        Map(m => m.Price);
        Map(m => m.Description).Optional();  // Optional field
    }
}

// Your exact scenario: 5 files with 3 different ClassMaps
var fileConfigs = new[]
{
    // Files 1-3 use ProductMapByName
    new CsvFileConfig<Product>("file1.csv", ctx => ctx.RegisterClassMap<ProductMapByName>()),
    new CsvFileConfig<Product>("file2.csv", ctx => ctx.RegisterClassMap<ProductMapByName>()),
    new CsvFileConfig<Product>("file3.csv", ctx => ctx.RegisterClassMap<ProductMapByName>()),

    // File 4 uses ProductMapByIndex (headerless)
    new CsvFileConfig<Product>("file4.csv", ctx =>
    {
        ctx.Configuration.HasHeaderRecord = false;
        ctx.RegisterClassMap<ProductMapByIndex>();
    }),

    // File 5 uses ProductMapWithOptional
    new CsvFileConfig<Product>("file5.csv", ctx => ctx.RegisterClassMap<ProductMapWithOptional>())
};

var results = await fileConfigs.ParseCsvParallelAsync(
    new CsvOperationOptions
    {
        ParallelOptions = new()
        {
            MaxDegreeOfParallelism = 3,
            OrderedOutput = true  // Maintain input order
        }
    });
```

### Writing with Per-File ClassMaps

```csharp
var fileConfigs = new[]
{
    CsvFileConfig<Product>.ForWrite(
        "standard_output.csv",
        standardProducts,
        ctx => ctx.RegisterClassMap<ProductMapByName>()
    ),

    CsvFileConfig<Product>.ForWrite(
        "detailed_output.csv",
        detailedProducts,
        ctx => ctx.RegisterClassMap<ProductMapWithOptional>()
    )
};

await fileConfigs.WriteCsvParallelAsync(
    new CsvOperationOptions { OverwriteExisting = true });
```

### Advanced ClassMap Features

ClassMaps support many advanced scenarios:

```csharp
public class AdvancedProductMap : ClassMap<Product>
{
    public AdvancedProductMap()
    {
        // Map by column name
        Map(m => m.Id).Name("ProductID");

        // Map by index for headerless files
        Map(m => m.Name).Index(1);

        // Optional fields (won't error if missing)
        Map(m => m.Description).Optional();

        // Ignore properties during read/write
        Map(m => m.InternalNotes).Ignore();

        // Auto-map with overrides
        AutoMap(CultureInfo.InvariantCulture);
        Map(m => m.Name).Name("ProductName");  // Override auto-mapped name
    }
}
```

### Combining CsvContext with CsvOperationOptions

Separate CsvHelper-specific configuration from Rivulet parallel execution settings:

```csharp
var results = await csvFiles.ParseCsvParallelAsync<Product>(
    ctx =>
    {
        // CsvHelper configuration
        ctx.Configuration.Delimiter = ";";
        ctx.Configuration.TrimOptions = TrimOptions.Trim;
        ctx.RegisterClassMap<ProductMap>();
    },
    new CsvOperationOptions
    {
        // Rivulet configuration
        Encoding = Encoding.UTF8,
        BufferSize = 4096,
        ParallelOptions = new()
        {
            MaxDegreeOfParallelism = 8,
            ErrorMode = ErrorMode.CollectAndContinue,
            Progress = new() { /* progress tracking */ }
        }
    });
```

## Core Features

### CsvHelper Integration

Rivulet.Csv is built on top of [CsvHelper](https://joshclose.github.io/CsvHelper/), providing full access to CsvHelper's powerful features:

```csharp
// Use custom CsvHelper configuration
await csvFiles.ParseCsvParallelAsync<Product>(
    config =>
    {
        config.HasHeaderRecord = true;
        config.Delimiter = ";";
        config.TrimOptions = TrimOptions.Trim;
        config.MissingFieldFound = null; // Ignore missing fields
    },
    new CsvOperationOptions
    {
        ParallelOptions = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 4 }
    });

// Write with custom configuration
await fileWrites.WriteCsvParallelAsync(
    config =>
    {
        config.Delimiter = "\t"; // Tab-separated values
        config.HasHeaderRecord = true;
        config.ShouldQuote = args => true; // Quote all fields
    },
    new CsvOperationOptions { OverwriteExisting = true });
```

### CSV Options

Configure CSV operations with `CsvOperationOptions`:

```csharp
var options = new CsvOperationOptions
{
    // CSV format settings
    HasHeaderRecord = true,           // CSV has header row
    Delimiter = ",",                   // Field delimiter (default: comma)
    TrimWhitespace = false,           // Trim whitespace from fields
    IgnoreBlankLines = true,          // Skip blank lines

    // Encoding and culture
    Encoding = Encoding.UTF8,         // Text encoding
    Culture = CultureInfo.InvariantCulture, // Culture for parsing

    // File I/O settings
    BufferSize = 81920,               // Buffer size (80 KB)
    CreateDirectoriesIfNotExist = true, // Auto-create directories
    OverwriteExisting = false,        // Overwrite existing files

    // Parallel execution
    ParallelOptions = new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 8,
        MaxRetries = 3,
        ErrorMode = ErrorMode.CollectAndContinue
    }
};
```

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

    OnFileCompleteAsync = async (filePath, recordCount) =>
    {
        Console.WriteLine($"Completed: {filePath} ({recordCount} records)");
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

### Custom Type Mapping

Use CsvHelper's attribute-based mapping:

```csharp
using CsvHelper.Configuration.Attributes;

public class Product
{
    [Name("product_id")]
    public int Id { get; set; }

    [Name("product_name")]
    public string Name { get; set; }

    [Name("price")]
    [NumberStyles(NumberStyles.Currency)]
    public decimal Price { get; set; }

    [Name("created_date")]
    [Format("yyyy-MM-dd")]
    public DateTime CreatedDate { get; set; }

    [Ignore]
    public string InternalNote { get; set; } // Not in CSV
}

var products = await csvFiles.ParseCsvParallelAsync<Product>();
```

Or use ClassMap for complex scenarios:

```csharp
using CsvHelper.Configuration;

public class ProductMap : ClassMap<Product>
{
    public ProductMap()
    {
        Map(m => m.Id).Name("ProductID");
        Map(m => m.Name).Name("ProductName");
        Map(m => m.Price).Name("Price").TypeConverter<DecimalConverter>();
        Map(m => m.CreatedDate).Name("CreatedDate").TypeConverterOption.Format("yyyy-MM-dd");
    }
}

// Register the map globally
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    HasHeaderRecord = true
};
config.RegisterClassMap<ProductMap>();
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

await migrationTasks.TransformCsvParallelAsync<OldFormat, NewFormat>(
    async (filePath, oldRecords) =>
    {
        return oldRecords.Select(old => new NewFormat
        {
            Id = old.LegacyId,
            Name = old.CustomerName,
            Email = old.EmailAddress,
            CreatedDate = old.RegistrationDate
        });
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
// Generate reports from CSV data
var reportTasks = new[]
{
    (source: "sales-Q1.csv", report: "reports/Q1-summary.csv"),
    (source: "sales-Q2.csv", report: "reports/Q2-summary.csv"),
    (source: "sales-Q3.csv", report: "reports/Q3-summary.csv"),
    (source: "sales-Q4.csv", report: "reports/Q4-summary.csv")
};

await reportTasks.TransformCsvParallelAsync<SalesRecord, SalesSummary>(
    async (source, records) =>
    {
        return records
            .GroupBy(r => r.ProductCategory)
            .Select(g => new SalesSummary
            {
                Category = g.Key,
                TotalSales = g.Sum(r => r.Amount),
                AveragePrice = g.Average(r => r.Price),
                TransactionCount = g.Count()
            });
    });
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
- Returns: `Task<List<T>>` - Flattened list of all records from all files

**`ParseCsvParallelGroupedAsync<T>`**
- Parse multiple CSV files in parallel, preserving file source
- Returns: `Task<IReadOnlyDictionary<string, IReadOnlyList<T>>>` - Records grouped by file path

**`StreamCsvParallelAsync<T>`**
- Stream CSV records as they're parsed (memory-efficient)
- Returns: `IAsyncEnumerable<T>` - Stream of records

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
- `HasHeaderRecord`, `Delimiter`, `TrimWhitespace`, `IgnoreBlankLines`
- `Encoding`, `Culture`, `BufferSize`
- `CreateDirectoriesIfNotExist`, `OverwriteExisting`
- `ParallelOptions`
- `OnFileStartAsync`, `OnFileCompleteAsync`, `OnFileErrorAsync`

## See Also

- [CsvHelper Documentation](https://joshclose.github.io/CsvHelper/)
- [Rivulet.Core Documentation](../Rivulet.Core/README.md)
- [Rivulet.IO Documentation](../Rivulet.IO/README.md)

## License

MIT License - see [LICENSE](../../LICENSE.txt) for details.
