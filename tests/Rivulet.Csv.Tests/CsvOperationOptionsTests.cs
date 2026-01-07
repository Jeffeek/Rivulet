using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using CsvHelper.Configuration;
using Rivulet.Core;

namespace Rivulet.Csv.Tests;

public sealed class CsvOperationOptionsTests : IDisposable
{
    private readonly string _testDirectory;

    public CsvOperationOptionsTests()
    {
        _testDirectory = Path.Join(Path.GetTempPath(), $"RivuletCsvOptionsTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithTrimWhitespace_ShouldTrimFields()
    {
        // Arrange
        var csvPath = Path.Join(_testDirectory, "whitespace.csv");
        // ReSharper disable once GrammarMistakeInStringLiteral
        const string csvContent = """
                                  Id,Name,Price
                                  1,  Product A  ,10.50
                                  2,  Product B  ,20.00
                                  """;
        await File.WriteAllTextAsync(csvPath, csvContent);

        // Act
        var results = await new[] { csvPath }.ParseCsvParallelAsync<Product>(
            new CsvOperationOptions
            {
                FileConfiguration = new CsvFileConfiguration
                {
                    ConfigurationAction = static cfg =>
                    {
                        cfg.TrimOptions = TrimOptions.Trim;
                    }
                }
            });

        // Assert
        results.Count.ShouldBe(2);
        results.ShouldContain(static p => p.Name == "Product A");
        results.ShouldContain(static p => p.Name == "Product B");
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithoutTrimWhitespace_ShouldPreserveWhitespace()
    {
        // Arrange
        var csvPath = Path.Join(_testDirectory, "whitespace.csv");
        const string csvContent = """
                                  Id,Name,Price
                                  1,  Product A  ,10.50
                                  """;
        await File.WriteAllTextAsync(csvPath, csvContent);

        // Act
        var results = await new[] { csvPath }.ParseCsvParallelAsync<Product>(
            new CsvOperationOptions
            {
                FileConfiguration = new CsvFileConfiguration
                {
                    ConfigurationAction = static cfg =>
                    {
                        cfg.TrimOptions = TrimOptions.None;
                    }
                }
            });

        // Assert
        results[0].Name.ShouldBe("  Product A  ");
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithDifferentEncoding_ShouldParseCorrectly()
    {
        // Arrange
        var csvPath = Path.Join(_testDirectory, "utf16.csv");
        const string csvContent = """
                                  Id,Name,Price
                                  1,Prøduct Â,10.50
                                  2,Prödüct B,20.00
                                  """;
        await File.WriteAllTextAsync(csvPath, csvContent, Encoding.Unicode);

        // Act
        var results = await new[] { csvPath }.ParseCsvParallelAsync<Product>(
            new CsvOperationOptions
            {
                Encoding = Encoding.Unicode
            });

        // Assert
        results.Count.ShouldBe(2);
        results.ShouldContain(static p => p.Name == "Prøduct Â");
        results.ShouldContain(static p => p.Name == "Prödüct B");
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithDifferentCulture_ShouldParseDecimalsCorrectly()
    {
        // Arrange
        var csvPath = Path.Join(_testDirectory, "culture.csv");
        const string csvContent = """
                                  Id,Name,Price
                                  1,Product A,10.50
                                  2,Product B,20.00
                                  """;
        await File.WriteAllTextAsync(csvPath, csvContent);

        // Act
        var results = await new[] { csvPath }.ParseCsvParallelAsync<Product>(
            new CsvOperationOptions
            {
                Culture = CultureInfo.InvariantCulture
            });

        // Assert
        results.Count.ShouldBe(2);
        results.ShouldContain(static p => p.Price == 10.50m);
        results.ShouldContain(static p => p.Price == 20.00m);
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithBlankLines_ShouldIgnoreByDefault()
    {
        // Arrange
        var csvPath = Path.Join(_testDirectory, "blank_lines.csv");
        const string csvContent = """
                                  Id,Name,Price
                                  1,Product A,10.50

                                  2,Product B,20.00

                                  """;
        await File.WriteAllTextAsync(csvPath, csvContent);

        // Act
        var results = await new[] { csvPath }.ParseCsvParallelAsync<Product>(
            new CsvOperationOptions
            {
                FileConfiguration = new CsvFileConfiguration
                {
                    ConfigurationAction = static cfg =>
                    {
                        cfg.IgnoreBlankLines = true;
                    }
                }
            });

        // Assert
        results.Count.ShouldBe(2);
    }

    [Fact]
    public async Task WriteCsvParallelAsync_WithDifferentEncoding_ShouldWriteCorrectly()
    {
        // Arrange
        var products = new[]
        {
            new Product { Id = 1, Name = "Prøduct Â", Price = 10.50m },
            new Product { Id = 2, Name = "Prödüct B", Price = 20.00m }
        };

        var csvPath = Path.Join(_testDirectory, "utf16-output.csv");
        var fileWrites = new[]
        {
            new RivuletCsvWriteFile<Product>(csvPath, products, null)
        };

        // Act
        await fileWrites.WriteCsvParallelAsync(
            new CsvOperationOptions
            {
                Encoding = Encoding.Unicode,
                OverwriteExisting = true
            });

        // Assert
        var content = await File.ReadAllTextAsync(csvPath, Encoding.Unicode);
        content.ShouldContain("Prøduct Â");
        content.ShouldContain("Prödüct B");
    }

    [Fact]
    public async Task WriteCsvParallelAsync_WithTabDelimiter_ShouldWriteTSV()
    {
        // Arrange
        var products = new[]
        {
            new Product { Id = 1, Name = "Product A", Price = 10.50m }
        };

        var csvPath = Path.Join(_testDirectory, "output.tsv");
        var fileWrites = new[]
        {
            new RivuletCsvWriteFile<Product>(
                csvPath,
                products,
                new CsvFileConfiguration
                {
                    ConfigurationAction = static cfg =>
                    {
                        cfg.Delimiter = "\t";
                        cfg.HasHeaderRecord = true;
                    }
                })
        };

        // Act
        await fileWrites.WriteCsvParallelAsync(
            new CsvOperationOptions
            {
                OverwriteExisting = true
            });

        // Assert
        var content = await File.ReadAllTextAsync(csvPath);
        content.ShouldContain("Id\tName\tPrice");
        content.ShouldContain("1\tProduct A\t10.50");
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithoutHeaders_ShouldParseByPosition()
    {
        // Arrange
        var csvPath = Path.Join(_testDirectory, "no_header.csv");
        const string csvContent = """
                                  1,Product A,10.50
                                  2,Product B,20.00
                                  """;
        await File.WriteAllTextAsync(csvPath, csvContent);

        // Act
        var results = await new[] { csvPath }.ParseCsvParallelAsync<Product>(
            new CsvOperationOptions
            {
                FileConfiguration = new CsvFileConfiguration
                {
                    ConfigurationAction = static cfg =>
                    {
                        cfg.HasHeaderRecord = false;
                    }
                }
            });

        // Assert
        results.Count.ShouldBe(2);
        results.ShouldContain(static p => p.Id == 1 && p.Name == "Product A");
        results.ShouldContain(static p => p.Id == 2 && p.Name == "Product B");
    }

    [Fact]
    public async Task WriteCsvParallelAsync_WithoutHeaders_ShouldWriteDataOnly()
    {
        // Arrange
        var products = new[]
        {
            new Product { Id = 1, Name = "Product A", Price = 10.50m }
        };

        var csvPath = Path.Join(_testDirectory, "no_header-output.csv");
        var fileWrites = new[]
        {
            new RivuletCsvWriteFile<Product>(
                csvPath,
                products,
                new CsvFileConfiguration
                {
                    ConfigurationAction = static cfg =>
                    {
                        cfg.HasHeaderRecord = false;
                    }
                })
        };

        // Act
        await fileWrites.WriteCsvParallelAsync(
            new CsvOperationOptions
            {
                OverwriteExisting = true
            });

        // Assert
        var content = await File.ReadAllTextAsync(csvPath);
        content.ShouldNotContain("Id,Name,Price");
        content.ShouldContain("1,Product A,10.50");
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithBufferSize_ShouldUseCustomBuffer()
    {
        // Arrange
        var csvPath = Path.Join(_testDirectory, "buffer.csv");
        const string csvContent = """
                                  Id,Name,Price
                                  1,Product A,10.50
                                  """;
        await File.WriteAllTextAsync(csvPath, csvContent);

        // Act
        var results = await new[] { csvPath }.ParseCsvParallelAsync<Product>(
            new CsvOperationOptions
            {
                BufferSize = 4096 // Small buffer
            });

        // Assert
        results.Count.ShouldBe(1);
    }

    [Fact]
    public async Task WriteCsvParallelAsync_WithCreateDirectories_ShouldCreatePath()
    {
        // Arrange
        var products = new[]
        {
            new Product { Id = 1, Name = "Product A", Price = 10.50m }
        };

        var csvPath = Path.Join(_testDirectory, "sub_dir1", "sub_dir2", "output.csv");
        var fileWrites = new[]
        {
            new RivuletCsvWriteFile<Product>(csvPath, products, null)
        };

        // Act
        await fileWrites.WriteCsvParallelAsync(
            new CsvOperationOptions
            {
                CreateDirectoriesIfNotExist = true,
                OverwriteExisting = true
            });

        // Assert
        File.Exists(csvPath).ShouldBeTrue();
    }

    [Fact]
    public async Task WriteCsvParallelAsync_WithoutCreateDirectories_ShouldThrowIfMissing()
    {
        // Arrange
        var products = new[]
        {
            new Product { Id = 1, Name = "Product A", Price = 10.50m }
        };

        var csvPath = Path.Join(_testDirectory, "nonexistent", "output.csv");
        var fileWrites = new[]
        {
            new RivuletCsvWriteFile<Product>(csvPath, products, null)
        };

        // Act & Assert
        // Use CollectAndContinue to ensure the DirectoryNotFoundException is properly collected and re-thrown
        // instead of being masked by TaskCanceledException from FailFast cancellation
        var exception = await Should.ThrowAsync<AggregateException>(() => fileWrites.WriteCsvParallelAsync(
            new CsvOperationOptions
            {
                CreateDirectoriesIfNotExist = false,
                ParallelOptions = new ParallelOptionsRivulet
                {
                    ErrorMode = ErrorMode.CollectAndContinue,
                    MaxRetries = 0
                }
            }));

        // Verify the exception contains DirectoryNotFoundException
        exception.InnerExceptions.ShouldContain(static e => e is DirectoryNotFoundException);
    }

    [SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Local")]
    private sealed class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }
}
