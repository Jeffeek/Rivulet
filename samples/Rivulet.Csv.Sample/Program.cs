using System.Diagnostics.CodeAnalysis;
using Rivulet.Core;
using Rivulet.Core.Observability;
using Rivulet.Core.Resilience;
using Rivulet.Csv;

// ReSharper disable ArgumentsStyleLiteral

Console.WriteLine("=== Rivulet.Csv Sample ===\n");

// Setup sample directory
var sampleDir = Path.Join(Path.GetTempPath(), "RivuletCsv.Sample");
var inputDir = Path.Join(sampleDir, "input");
var outputDir = Path.Join(sampleDir, "output");

Directory.CreateDirectory(inputDir);
Directory.CreateDirectory(outputDir);

{
    // Sample 1: Write CSV files in parallel
    Console.WriteLine("1. WriteCsvParallelAsync - Create sample CSV files");

    var products1 = new[]
    {
        new Product { Id = 1, Name = "Laptop", Price = 999.99m, Category = "Electronics" },
        new Product { Id = 2, Name = "Mouse", Price = 29.99m, Category = "Electronics" },
        new Product { Id = 3, Name = "Keyboard", Price = 79.99m, Category = "Electronics" }
    };

    var products2 = new[]
    {
        new Product { Id = 4, Name = "Desk", Price = 299.99m, Category = "Furniture" },
        new Product { Id = 5, Name = "Chair", Price = 199.99m, Category = "Furniture" }
    };

    var fileWrites = new[]
    {
        new RivuletCsvWriteFile<Product>(Path.Join(inputDir, "products1.csv"), products1, null),
        new RivuletCsvWriteFile<Product>(Path.Join(inputDir, "products2.csv"), products2, null)
    };

    await fileWrites.WriteCsvParallelAsync(
        new CsvOperationOptions
        {
            ParallelOptions = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 2 }
        });

    Console.WriteLine($"✓ Created {fileWrites.Length} CSV files with {products1.Length + products2.Length} total products\n");

    // Sample 2: Read CSV files in parallel
    Console.WriteLine("2. ParseCsvParallelAsync - Read CSV files");

    var csvPaths = new[] { Path.Join(inputDir, "products1.csv"), Path.Join(inputDir, "products2.csv") };
    var allProducts = await csvPaths.ParseCsvParallelAsync<Product>(
        new CsvOperationOptions
        {
            ParallelOptions = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 4 }
        });

    Console.WriteLine($"✓ Read {allProducts.Count} products from {csvPaths.Length} files");
    Console.WriteLine($"  Sample: {allProducts[0].Name} - ${allProducts[0].Price}\n");

    // Sample 3: Parse CSV files with grouped results (preserving file source)
    Console.WriteLine("3. ParseCsvParallelGroupedAsync - Read with file tracking");

    var productReads = new[]
    {
        new RivuletCsvReadFile<Product>(Path.Join(inputDir, "products1.csv"), null),
        new RivuletCsvReadFile<Product>(Path.Join(inputDir, "products2.csv"), null)
    };

    var groupedResults = await productReads.ParseCsvParallelGroupedAsync();

    Console.WriteLine($"✓ Read {groupedResults.Count} files (results grouped by source):");
    foreach (var (filePath, products) in groupedResults)
        Console.WriteLine($"  {Path.GetFileName(filePath)}: {products.Count} products");

    Console.WriteLine();

    // Sample 4: Multi-type operations (read different types in parallel)
    Console.WriteLine("4. ParseCsvParallelGroupedAsync (Multi-Type) - Read multiple CSV types concurrently");

    // Create customers CSV
    var customers = new[]
    {
        new Customer { Id = 1, Name = "John Doe", Email = "john@example.com", Country = "USA" },
        new Customer { Id = 2, Name = "Jane Smith", Email = "jane@example.com", Country = "UK" }
    };

    var customerWrites = new[]
    {
        new RivuletCsvWriteFile<Customer>(Path.Join(inputDir, "customers.csv"), customers, null)
    };

    await customerWrites.WriteCsvParallelAsync();

    // Read products and customers concurrently with different types
    var customerReads = new[]
    {
        new RivuletCsvReadFile<Customer>(Path.Join(inputDir, "customers.csv"), null)
    };

    var (productDict, customerDict) = await CsvParallelExtensions.ParseCsvParallelGroupedAsync(
        productReads,
        customerReads);

    Console.WriteLine($"✓ Read {productDict.Values.Sum(static p => p.Count)} products and {customerDict.Values.Sum(static c => c.Count)} customers concurrently\n");

    // Sample 5: Transform CSV files
    Console.WriteLine("5. TransformCsvParallelAsync - Transform products with price increase");

    var transformations = new[]
    {
        (
            new RivuletCsvReadFile<Product>(Path.Join(inputDir, "products1.csv"), null),
            new RivuletCsvWriteFile<Product>(Path.Join(outputDir, "products1_increased.csv"), Array.Empty<Product>(), null)
        ),
        (
            new RivuletCsvReadFile<Product>(Path.Join(inputDir, "products2.csv"), null),
            new RivuletCsvWriteFile<Product>(Path.Join(outputDir, "products2_increased.csv"), Array.Empty<Product>(), null)
        )
    };

    await transformations.TransformCsvParallelAsync(static product => new Product
    {
        Id = product.Id,
        Name = product.Name,
        Price = product.Price * 1.10m, // 10% price increase
        Category = product.Category
    });

    Console.WriteLine("✓ Transformed products with 10% price increase\n");

    // Sample 6: CSV operations with progress tracking
    Console.WriteLine("6. ParseCsvParallelAsync - With progress tracking");

    // Create more files for progress demo
    var largeDataWrites = Enumerable.Range(3, 5).Select(i =>
    {
        var data = Enumerable.Range((i - 1) * 100, 100)
            .Select(static j => new Product { Id = j, Name = $"Product {j}", Price = j * 1.5m, Category = "Test" })
            .ToArray();

        return new RivuletCsvWriteFile<Product>(Path.Join(inputDir, $"products{i}.csv"), data, null);
    }).ToArray();

    await largeDataWrites.WriteCsvParallelAsync();

    var largeDataPaths = Enumerable.Range(1, 7)
        .Select(i => Path.Join(inputDir, $"products{i}.csv"))
        .ToArray();

    var progressReported = 0;
    var largeDataResults = await largeDataPaths.ParseCsvParallelAsync<Product>(
        new CsvOperationOptions
        {
            ParallelOptions = new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 3,
                Progress = new ProgressOptions
                {
                    ReportInterval = TimeSpan.FromMilliseconds(100),
                    OnProgress = async snapshot =>
                    {
                        progressReported++;
                        if (progressReported % 2 == 0) // Report every other update to reduce noise
                        {
                            Console.WriteLine($"  Progress: {snapshot.ItemsCompleted}/{snapshot.TotalItems} files " +
                                              $"({snapshot.PercentComplete:F1}%) - {snapshot.ItemsPerSecond:F0} items/sec");
                        }

                        await Task.CompletedTask;
                    }
                }
            }
        });

    Console.WriteLine($"✓ Read {largeDataResults.Count} products with progress tracking\n");

    // Sample 7: CSV operations with error handling and retries
    Console.WriteLine("7. ParseCsvParallelAsync - With error handling");

    var mixedPaths = new[]
    {
        Path.Join(inputDir, "products1.csv"),
        Path.Join(inputDir, "nonexistent.csv"), // This will fail
        Path.Join(inputDir, "products2.csv")
    };

    try
    {
        await mixedPaths.ParseCsvParallelAsync<Product>(
            new CsvOperationOptions
            {
                ParallelOptions = new ParallelOptionsRivulet
                {
                    MaxRetries = 2,
                    BaseDelay = TimeSpan.FromMilliseconds(100),
                    ErrorMode = ErrorMode.CollectAndContinue,
                    OnErrorAsync = static async (idx, ex) =>
                    {
                        Console.WriteLine($"  Error processing file {idx}: {ex.Message}");
                        await Task.CompletedTask;
                        return true; // Continue processing other files
                    }
                }
            });
    }
    catch (AggregateException ex)
    {
        Console.WriteLine($"✓ Caught expected errors: {ex.InnerExceptions.Count} file(s) failed");
        Console.WriteLine("  (Retried transient errors, collected all failures)\n");
    }

    // Sample 8: Custom CSV configuration (per-file)
    Console.WriteLine("8. WriteCsvParallelAsync - With custom CSV configuration");

    var customConfig = new CsvFileConfiguration
    {
        ConfigurationAction = static config =>
        {
            config.Delimiter = "|"; // Use pipe delimiter instead of comma
            config.HasHeaderRecord = true;
        }
    };

    var customWrite = new[]
    {
        new RivuletCsvWriteFile<Product>(
            Path.Join(outputDir, "products_custom.csv"),
            products1,
            customConfig)
    };

    await customWrite.WriteCsvParallelAsync();

    var customContent = await File.ReadAllTextAsync(Path.Join(outputDir, "products_custom.csv"));
    Console.WriteLine("✓ Created custom CSV with pipe delimiter:");
    Console.WriteLine($"  {customContent.Split('\n')[0]}\n");

    // Sample 9: Circuit breaker for resilient operations
    Console.WriteLine("9. ParseCsvParallelAsync - With circuit breaker");

    var circuitBreakerPaths = Enumerable.Range(1, 10)
        .Select(i => Path.Join(inputDir, i <= 5 ? $"products{Math.Min(i, 3)}.csv" : $"missing{i}.csv"))
        .ToArray();

    var circuitBreakerOpened = false;
    try
    {
        await circuitBreakerPaths.ParseCsvParallelAsync<Product>(
            new CsvOperationOptions
            {
                ParallelOptions = new ParallelOptionsRivulet
                {
                    MaxRetries = 0,
                    MaxDegreeOfParallelism = 1,
                    ErrorMode = ErrorMode.CollectAndContinue,
                    CircuitBreaker = new CircuitBreakerOptions
                    {
                        FailureThreshold = 3,
                        OpenTimeout = TimeSpan.FromSeconds(1),
                        OnStateChange = (_, newState) =>
                        {
                            if (newState != CircuitBreakerState.Open)
                                return ValueTask.CompletedTask;

                            circuitBreakerOpened = true;
                            Console.WriteLine("  Circuit breaker opened after 3 failures!");

                            return ValueTask.CompletedTask;
                        }
                    }
                }
            });
    }
    catch (AggregateException)
    {
        if (circuitBreakerOpened)
            Console.WriteLine("✓ Circuit breaker prevented further failures\n");
    }

    // Sample 10: Lifecycle callbacks
    Console.WriteLine("10. ParseCsvParallelAsync - With lifecycle callbacks");

    var callbackFiles = 0;
    await csvPaths.ParseCsvParallelAsync<Product>(
        new CsvOperationOptions
        {
            OnFileStartAsync = static async path =>
            {
                Console.WriteLine($"  Starting: {Path.GetFileName(path)}");
                await Task.CompletedTask;
            },
            OnFileCompleteAsync = async (path, result) =>
            {
                callbackFiles++;
                Console.WriteLine($"  Completed: {Path.GetFileName(path)} - {result.RecordCount} records, {result.BytesProcessed} bytes");
                await Task.CompletedTask;
            }
        });

    Console.WriteLine($"✓ Processed {callbackFiles} files with lifecycle callbacks\n");

    Console.WriteLine("=== All samples completed successfully ===");
    Console.WriteLine($"\nSample files located at: {sampleDir}");
}

#region Model Classes

[
    SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local"),
    SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Local")
]
file sealed class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
}

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
file sealed class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

#endregion
