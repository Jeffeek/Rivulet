using System.Diagnostics.CodeAnalysis;
using Rivulet.Core;

namespace Rivulet.Csv.Tests;

public sealed class CsvParallelExtensionsTests : IDisposable
{
    private readonly string _testDirectory;

    public CsvParallelExtensionsTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"RivuletCsvTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            // ReSharper disable once ArgumentsStyleLiteral
            Directory.Delete(_testDirectory, recursive: true);
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithSingleFile_ShouldParseSuccessfully()
    {
        // Arrange
        var csvPath = Path.Combine(_testDirectory, "products.csv");
        // ReSharper disable once GrammarMistakeInStringLiteral
        const string csvContent = """
                                  Id,Name,Price
                                  1,Product A,10.50
                                  2,Product B,20.00
                                  3,Product C,15.75
                                  """;
        await File.WriteAllTextAsync(csvPath, csvContent);

        // Act
        var results = await new[] { csvPath }.ParseCsvParallelAsync<Product>();

        // Assert - order-independent
        results.Count.ShouldBe(3);
        results.ShouldContain(p => p.Id == 1 && p.Name == "Product A" && p.Price == 10.50m);
        results.ShouldContain(p => p.Id == 2 && p.Name == "Product B" && p.Price == 20.00m);
        results.ShouldContain(p => p.Id == 3 && p.Name == "Product C" && p.Price == 15.75m);
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithMultipleFiles_ShouldParseAllFiles()
    {
        // Arrange
        var csvPath1 = Path.Combine(_testDirectory, "products1.csv");
        var csvPath2 = Path.Combine(_testDirectory, "products2.csv");

        await File.WriteAllTextAsync(
            csvPath1,
            // ReSharper disable once GrammarMistakeInStringLiteral
            """
            Id,Name,Price
            1,Product A,10.50
            2,Product B,20.00
            """);

        await File.WriteAllTextAsync(
            csvPath2,
            // ReSharper disable once GrammarMistakeInStringLiteral
            """
            Id,Name,Price
            3,Product C,15.75
            4,Product D,30.00
            5,Product E,25.50
            """);

        // Act
        var results = await new[] { csvPath1, csvPath2 }.ParseCsvParallelAsync<Product>(
            new CsvOperationOptions
            {
                ParallelOptions = new ParallelOptionsRivulet
                {
                    MaxDegreeOfParallelism = 2,
                    OrderedOutput = true
                }
            });

        // Assert - order-independent
        results.Count.ShouldBe(5); // Total records from both files
        results.ShouldContain(p => p.Id == 1);
        results.ShouldContain(p => p.Id == 2);
        results.ShouldContain(p => p.Id == 3);
        results.ShouldContain(p => p.Id == 4);
        results.ShouldContain(p => p.Id == 5);
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

        var csvPath = Path.Combine(_testDirectory, "output.csv");
        var fileWrites = new[] { (csvPath, (IEnumerable<Product>)products) };

        // Act
        await fileWrites.WriteCsvParallelAsync(
            new CsvOperationOptions
            {
                FileConfiguration = new CsvFileConfiguration
                {
                    WriterConfigurationAction = cfg =>
                    {
                        if (cfg is CsvHelper.Configuration.CsvConfiguration csvConfig)
                            csvConfig.HasHeaderRecord = true;
                    }
                },
                OverwriteExisting = true
            });

        // Assert
        File.Exists(csvPath).ShouldBeTrue();
        var content = await File.ReadAllTextAsync(csvPath);
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

        var csvPath1 = Path.Combine(_testDirectory, "output1.csv");
        var csvPath2 = Path.Combine(_testDirectory, "output2.csv");

        var fileWrites = new[]
        {
            (csvPath1, (IEnumerable<Product>)products1),
            (csvPath2, (IEnumerable<Product>)products2)
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
            });

        // Assert
        File.Exists(csvPath1).ShouldBeTrue();
        File.Exists(csvPath2).ShouldBeTrue();
    }

    [Fact]
    public async Task TransformCsvParallelAsync_ShouldParseTransformAndWrite()
    {
        // Arrange
        var inputPath = Path.Combine(_testDirectory, "input.csv");
        var outputPath = Path.Combine(_testDirectory, "output.csv");

        await File.WriteAllTextAsync(inputPath,
            // ReSharper disable once GrammarMistakeInStringLiteral
            """
            Id,Name,Price
            1,Product A,10.00
            2,Product B,20.00
            """);

        var transformations = new[] { (inputPath, outputPath) };

        // Act
        await transformations.TransformCsvParallelAsync<Product, EnrichedProduct>(static p => new EnrichedProduct
            {
                Id = p.Id,
                Name = p.Name,
                OriginalPrice = p.Price,
                PriceWithTax = p.Price * 1.2m
            },
            new CsvOperationOptions
            {
                OverwriteExisting = true
            });

        // Assert
        File.Exists(outputPath).ShouldBeTrue();
        var content = await File.ReadAllTextAsync(outputPath);
        content.ShouldContain("PriceWithTax");
        content.ShouldContain("12"); // 10 * 1.2 = 12
        content.ShouldContain("24"); // 20 * 1.2 = 24
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithCustomDelimiter_ShouldParseCorrectly()
    {
        // Arrange
        var csvPath = Path.Combine(_testDirectory, "semicolon.csv");
        // ReSharper disable once GrammarMistakeInStringLiteral
        const string csvContent = """
                                  Id;Name;Price
                                  1;Product A;10.50
                                  2;Product B;20.00
                                  """;
        await File.WriteAllTextAsync(csvPath, csvContent);

        // Act
        var results = await new[] { csvPath }.ParseCsvParallelAsync<Product>(
            new CsvOperationOptions
            {
                FileConfiguration = new CsvFileConfiguration
                {
                    ReaderConfigurationAction = cfg =>
                    {
                        if (cfg is CsvHelper.Configuration.CsvConfiguration csvConfig)
                            csvConfig.Delimiter = ";";
                    }
                }
            });

        // Assert - order-independent
        results.Count.ShouldBe(2);
        results.ShouldContain(p => p.Name == "Product A");
        results.ShouldContain(p => p.Name == "Product B");
    }

    [Fact]
    public async Task WriteCsvParallelAsync_WithOverwriteFalse_ShouldThrowIfFileExists()
    {
        // Arrange
        var csvPath = Path.Combine(_testDirectory, "existing.csv");
        await File.WriteAllTextAsync(csvPath, "existing content");

        var products = new[] { new Product { Id = 1, Name = "Test", Price = 10m } };
        var fileWrites = new[] { (csvPath, (IEnumerable<Product>)products) };

        // Act & Assert - handle exception wrapping in parallel operations
        try
        {
            await fileWrites.WriteCsvParallelAsync(
                new CsvOperationOptions
                {
                    OverwriteExisting = false
                });

            // If we get here, the test should fail
            throw new InvalidOperationException("Expected an exception but none was thrown");
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            // The expected exception or one of its inner exceptions should be IOException
            var actualException = ex;
            var found = false;
            while (actualException != null)
            {
                if (actualException is IOException)
                {
                    found = true;
                    break;
                }
                actualException = actualException.InnerException;
            }
            found.ShouldBeTrue("Expected IOException in exception chain");
        }
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithCallbacks_ShouldInvokeCallbacks()
    {
        // Arrange
        var csvPath = Path.Combine(_testDirectory, "products.csv");
        await File.WriteAllTextAsync(csvPath,
            """
            Id,Name,Price
            1,Product A,10.50
            """);

        var startCalled = false;
        var completeCalled = false;
        long recordCount = 0;

        // Act
        await new[] { csvPath }.ParseCsvParallelAsync<Product>(
            new CsvOperationOptions
            {
                OnFileStartAsync = async _ =>
                {
                    startCalled = true;
                    await Task.CompletedTask;
                },
                OnFileCompleteAsync = async (_, count) =>
                {
                    completeCalled = true;
                    recordCount = count;
                    await Task.CompletedTask;
                }
            });

        // Assert
        startCalled.ShouldBeTrue();
        completeCalled.ShouldBeTrue();
        recordCount.ShouldBe(1);
    }

    [SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Local")]
    private sealed class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
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
