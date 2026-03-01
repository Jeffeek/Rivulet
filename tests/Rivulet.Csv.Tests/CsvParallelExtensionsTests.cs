using System.Diagnostics.CodeAnalysis;
using Rivulet.Core;

namespace Rivulet.Csv.Tests;

public sealed class CsvParallelExtensionsTests : IDisposable
{
    private readonly string _testDirectory;

    public CsvParallelExtensionsTests()
    {
        _testDirectory = Path.Join(Path.GetTempPath(), $"RivuletCsvTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithSingleFile_ShouldParseSuccessfully()
    {
        // Arrange
        var csvPath = Path.Join(_testDirectory, "products.csv");
        // ReSharper disable once GrammarMistakeInStringLiteral
        const string csvContent = """
                                  Id,Name,Price
                                  1,Product A,10.50
                                  2,Product B,20.00
                                  3,Product C,15.75
                                  """;
        await File.WriteAllTextAsync(csvPath, csvContent, TestContext.Current.CancellationToken);

        // Act
        var results = await new[] { csvPath }.ParseCsvParallelAsync<Product>(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        results.Count.ShouldBe(3);
        results.ShouldContain(static p => p.Id == 1 && p.Name == "Product A" && p.Price == 10.50m);
        results.ShouldContain(static p => p.Id == 2 && p.Name == "Product B" && p.Price == 20.00m);
        results.ShouldContain(static p => p.Id == 3 && p.Name == "Product C" && p.Price == 15.75m);
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithMultipleFiles_ShouldParseAllFiles()
    {
        // Arrange
        var csvPath1 = Path.Join(_testDirectory, "products1.csv");
        var csvPath2 = Path.Join(_testDirectory, "products2.csv");

        await File.WriteAllTextAsync(
            csvPath1,
            """
            Id,Name,Price
            1,Product A,10.50
            2,Product B,20.00
            """,
            TestContext.Current.CancellationToken);

        await File.WriteAllTextAsync(
            csvPath2,
            """
            Id,Name,Price
            3,Product C,15.75
            4,Product D,30.00
            5,Product E,25.50
            """,
            TestContext.Current.CancellationToken);

        // Act
        var results = await new[] { csvPath1, csvPath2 }.ParseCsvParallelAsync<Product>(
            new CsvOperationOptions
            {
                ParallelOptions = new ParallelOptionsRivulet
                {
                    MaxDegreeOfParallelism = 2,
                    OrderedOutput = true
                }
            },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        results.Count.ShouldBe(5);
        results.ShouldContain(static p => p.Id == 1);
        results.ShouldContain(static p => p.Id == 2);
        results.ShouldContain(static p => p.Id == 3);
        results.ShouldContain(static p => p.Id == 4);
        results.ShouldContain(static p => p.Id == 5);
    }

    [Fact]
    public async Task WriteCsvParallelAsync_WithSingleFile_ShouldWriteSuccessfully()
    {
        // Arrange
        var products = new[]
        {
            new Product { Id = 1, Name = "Product A", Price = 10.50m },
            new Product { Id = 2, Name = "Product B", Price = 20.00m }
        };

        var csvPath = Path.Join(_testDirectory, "output.csv");
        var fileWrites = new[]
        {
            new RivuletCsvWriteFile<Product>(
                csvPath,
                products,
                new CsvFileConfiguration
                {
                    ConfigurationAction = static cfg => cfg.HasHeaderRecord = true
                })
        };

        // Act
        await fileWrites.WriteCsvParallelAsync(
            new CsvOperationOptions
            {
                OverwriteExisting = true
            },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        File.Exists(csvPath).ShouldBeTrue();
        var content = await File.ReadAllTextAsync(csvPath, TestContext.Current.CancellationToken);
        content.ShouldContain("Product A");
        content.ShouldContain("Product B");
        content.ShouldContain("Id,Name,Price");
    }

    [Fact]
    public async Task WriteCsvParallelAsync_WithMultipleFiles_ShouldWriteAllFiles()
    {
        // Arrange
        var products1 = new[] { new Product { Id = 1, Name = "Product A", Price = 10.50m } };
        var products2 = new[] { new Product { Id = 2, Name = "Product B", Price = 20.00m } };

        var csvPath1 = Path.Join(_testDirectory, "output1.csv");
        var csvPath2 = Path.Join(_testDirectory, "output2.csv");

        var fileWrites = new[]
        {
            new RivuletCsvWriteFile<Product>(csvPath1, products1, null),
            new RivuletCsvWriteFile<Product>(csvPath2, products2, null)
        };

        // Act
        await fileWrites.WriteCsvParallelAsync(
            new CsvOperationOptions
            {
                OverwriteExisting = true,
                ParallelOptions = new ParallelOptionsRivulet
                {
                    MaxDegreeOfParallelism = 2
                }
            },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        File.Exists(csvPath1).ShouldBeTrue();
        File.Exists(csvPath2).ShouldBeTrue();
    }

    [Fact]
    public async Task TransformCsvParallelAsync_ShouldParseTransformAndWrite()
    {
        // Arrange
        var inputPath = Path.Join(_testDirectory, "input.csv");
        var outputPath = Path.Join(_testDirectory, "output.csv");

        await File.WriteAllTextAsync(inputPath,
            """
            Id,Name,Price
            1,Product A,10.00
            2,Product B,20.00
            """,
            TestContext.Current.CancellationToken);

        var transformations = new[]
        {
            (
                Input: new RivuletCsvReadFile<Product>(inputPath, null),
                Output: new RivuletCsvWriteFile<EnrichedProduct>(outputPath, Array.Empty<EnrichedProduct>(), null)
            )
        };

        // Act
        await transformations.TransformCsvParallelAsync<Product, EnrichedProduct>(
            static p => new EnrichedProduct
            {
                Id = p.Id,
                Name = p.Name,
                OriginalPrice = p.Price,
                PriceWithTax = p.Price * 1.2m
            },
            new CsvOperationOptions
            {
                OverwriteExisting = true
            },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        File.Exists(outputPath).ShouldBeTrue();
        var content = await File.ReadAllTextAsync(outputPath, TestContext.Current.CancellationToken);
        content.ShouldContain("PriceWithTax");
        content.ShouldContain("12"); // 10 * 1.2 = 12
        content.ShouldContain("24"); // 20 * 1.2 = 24
    }

    [Fact]
    public async Task TransformCsvParallelAsync_WithAsyncTransform_ShouldParseTransformAndWrite()
    {
        // Arrange
        var inputPath = Path.Join(_testDirectory, "input_async.csv");
        var outputPath = Path.Join(_testDirectory, "output_async.csv");

        await File.WriteAllTextAsync(inputPath,
            // ReSharper disable once GrammarMistakeInStringLiteral
            """
            Id,Name,Price
            1,Product A,10.00
            2,Product B,20.00
            3,Product C,30.00
            """,
            TestContext.Current.CancellationToken);

        var transformations = new[]
        {
            (
                Input: new RivuletCsvReadFile<Product>(inputPath, null),
                Output: new RivuletCsvWriteFile<EnrichedProduct>(outputPath, Array.Empty<EnrichedProduct>(), null)
            )
        };

        // Act - Use async transform function that simulates async I/O
        await transformations.TransformCsvParallelAsync(
            static async (p, ct) =>
            {
                await Task.Delay(1, ct); // Simulate async operation
                return new EnrichedProduct
                {
                    Id = p.Id,
                    Name = p.Name,
                    OriginalPrice = p.Price,
                    PriceWithTax = p.Price * 1.15m // 15% tax
                };
            },
            new CsvOperationOptions
            {
                OverwriteExisting = true
            },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        File.Exists(outputPath).ShouldBeTrue();
        var content = await File.ReadAllTextAsync(outputPath, TestContext.Current.CancellationToken);
        content.ShouldContain("PriceWithTax");
        content.ShouldContain("11.5"); // 10 * 1.15 = 11.5
        content.ShouldContain("23"); // 20 * 1.15 = 23
        content.ShouldContain("34.5"); // 30 * 1.15 = 34.5
    }

    [Fact]
    public async Task TransformCsvParallelAsync_WithAsyncTransformAndCancellation_ShouldRespectCancellation()
    {
        // Arrange
        var inputPath = Path.Join(_testDirectory, "input_cancel.csv");
        var outputPath = Path.Join(_testDirectory, "output_cancel.csv");

        var records = Enumerable.Range(1, 100)
            .Select(static i => $"{i},Product {i},{i * 10.0:F2}")
            .ToArray();
        await File.WriteAllTextAsync(inputPath, $"Id,Name,Price\n{string.Join("\n", records)}", TestContext.Current.CancellationToken);

        var transformations = new[]
        {
            (
                Input: new RivuletCsvReadFile<Product>(inputPath, null),
                Output: new RivuletCsvWriteFile<EnrichedProduct>(outputPath, Array.Empty<EnrichedProduct>(), null)
            )
        };

        using var cts = new CancellationTokenSource();
        var transformCount = 0;

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(() =>
        {
            return transformations.TransformCsvParallelAsync(
                async (p, ct) =>
                {
                    Interlocked.Increment(ref transformCount);
                    if (transformCount > 10)
                        // ReSharper disable once AccessToDisposedClosure
                        await cts.CancelAsync();

                    await Task.Delay(1, ct);
                    return new EnrichedProduct
                    {
                        Id = p.Id,
                        Name = p.Name,
                        OriginalPrice = p.Price,
                        PriceWithTax = p.Price * 1.2m
                    };
                },
                new CsvOperationOptions
                {
                    OverwriteExisting = true
                },
                cts.Token);
        });

        // Verify that cancellation stopped processing early
        transformCount.ShouldBeLessThan(100);
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithCustomDelimiter_ShouldParseCorrectly()
    {
        // Arrange
        var csvPath = Path.Join(_testDirectory, "semicolon.csv");
        // ReSharper disable once GrammarMistakeInStringLiteral
        const string csvContent = """
                                  Id;Name;Price
                                  1;Product A;10.50
                                  2;Product B;20.00
                                  """;
        await File.WriteAllTextAsync(csvPath, csvContent, TestContext.Current.CancellationToken);

        // Act
        var results = await new[] { csvPath }.ParseCsvParallelAsync<Product>(
            new CsvOperationOptions
            {
                FileConfiguration = new CsvFileConfiguration
                {
                    ConfigurationAction = static cfg => cfg.Delimiter = ";"
                }
            },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        results.Count.ShouldBe(2);
        results.ShouldContain(static p => p.Name == "Product A");
        results.ShouldContain(static p => p.Name == "Product B");
    }

    [Fact]
    public async Task WriteCsvParallelAsync_WithOverwriteFalse_ShouldThrowIfFileExists()
    {
        // Arrange
        var csvPath = Path.Join(_testDirectory, "existing.csv");
        await File.WriteAllTextAsync(csvPath, "existing content", TestContext.Current.CancellationToken);

        var products = new[] { new Product { Id = 1, Name = "Test", Price = 10m } };
        var fileWrites = new[]
        {
            new RivuletCsvWriteFile<Product>(csvPath, products, null)
        };

        // Act & Assert
        // Use CollectAndContinue to ensure the IOException is properly collected and re-thrown
        // instead of being masked by TaskCanceledException from FailFast cancellation
        var exception = await Should.ThrowAsync<AggregateException>(() => fileWrites.WriteCsvParallelAsync(
            new CsvOperationOptions
            {
                OverwriteExisting = false,
                ParallelOptions = new ParallelOptionsRivulet
                {
                    ErrorMode = ErrorMode.CollectAndContinue,
                    MaxRetries = 0
                }
            },
            cancellationToken: TestContext.Current.CancellationToken));

        // Verify the exception contains IOException
        exception.InnerExceptions.ShouldContain(static e => e is IOException);
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithCallbacks_ShouldInvokeCallbacks()
    {
        // Arrange
        var csvPath = Path.Join(_testDirectory, "products.csv");
        await File.WriteAllTextAsync(csvPath,
            """
            Id,Name,Price
            1,Product A,10.50
            """,
            TestContext.Current.CancellationToken);

        var startCalled = false;
        var completeCalled = false;
        long bytesProcessed = 0;
        long? recordCount = null;

        // Act
        await new[] { csvPath }.ParseCsvParallelAsync<Product>(
            new CsvOperationOptions
            {
                OnFileStartAsync = async _ =>
                {
                    startCalled = true;
                    await Task.CompletedTask;
                },
                OnFileCompleteAsync = async (_, result) =>
                {
                    completeCalled = true;
                    bytesProcessed = result.BytesProcessed;
                    recordCount = result.RecordCount;
                    await Task.CompletedTask;
                }
            },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        startCalled.ShouldBeTrue();
        completeCalled.ShouldBeTrue();
        bytesProcessed.ShouldBeGreaterThan(0);
        recordCount.ShouldBe(1);
    }

    [Fact]
    public async Task ParseCsvParallelGroupedAsync_WithSingleType_ShouldGroupByFilePath()
    {
        // Arrange
        var csvPath1 = Path.Join(_testDirectory, "file1.csv");
        var csvPath2 = Path.Join(_testDirectory, "file2.csv");

        await File.WriteAllTextAsync(csvPath1, "Id,Name,Price\n1,Product A,10.50", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(csvPath2, "Id,Name,Price\n2,Product B,20.00", TestContext.Current.CancellationToken);

        var fileReads = new[]
        {
            new RivuletCsvReadFile<Product>(csvPath1, null),
            new RivuletCsvReadFile<Product>(csvPath2, null)
        };

        // Act
        var results = await fileReads.ParseCsvParallelGroupedAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        results.Count.ShouldBe(2);
        results[csvPath1].Count.ShouldBe(1);
        results[csvPath1][0].Id.ShouldBe(1);
        results[csvPath2].Count.ShouldBe(1);
        results[csvPath2][0].Id.ShouldBe(2);
    }

    [Fact]
    public async Task ParseCsvParallelGroupedAsync_WithTwoTypes_ShouldGroupBothTypes()
    {
        // Arrange
        var productPath = Path.Join(_testDirectory, "products.csv");
        var customerPath = Path.Join(_testDirectory, "customers.csv");

        await File.WriteAllTextAsync(productPath, "Id,Name,Price\n1,Product A,10.50", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(customerPath, "Id,Name\n1,Customer A", TestContext.Current.CancellationToken);

        var productReads = new[] { new RivuletCsvReadFile<Product>(productPath, null) };
        var customerReads = new[] { new RivuletCsvReadFile<Customer>(customerPath, null) };

        // Act
        var (products, customers) = await CsvParallelExtensions.ParseCsvParallelGroupedAsync(
            productReads,
            customerReads,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        products.Count.ShouldBe(1);
        products[productPath].Count.ShouldBe(1);
        customers.Count.ShouldBe(1);
        customers[customerPath].Count.ShouldBe(1);
    }

    [Fact]
    public async Task WriteCsvParallelAsync_WithCallbacks_ShouldProvideMetrics()
    {
        // Arrange
        var products = new[] { new Product { Id = 1, Name = "Test", Price = 10m } };
        var csvPath = Path.Join(_testDirectory, "metrics.csv");

        var fileWrites = new[] { new RivuletCsvWriteFile<Product>(csvPath, products, null) };

        long bytesWritten = 0;
        long? recordsWritten = null;

        // Act
        await fileWrites.WriteCsvParallelAsync(
            new CsvOperationOptions
            {
                OverwriteExisting = true,
                OnFileCompleteAsync = async (_, result) =>
                {
                    bytesWritten = result.BytesProcessed;
                    recordsWritten = result.RecordCount;
                    await Task.CompletedTask;
                }
            },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        bytesWritten.ShouldBeGreaterThan(0);
        recordsWritten.ShouldBe(1);
    }

    [SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Local")]
    private sealed class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    [
        SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Local"),
        SuppressMessage("ReSharper", "ClassNeverInstantiated.Local"),
        SuppressMessage("ReSharper", "UnusedMember.Local")
    ]
    private sealed class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
    private sealed class EnrichedProduct
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal OriginalPrice { get; set; }
        public decimal PriceWithTax { get; set; }
    }
}
