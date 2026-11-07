using System.Diagnostics.CodeAnalysis;
using CsvHelper;
using Rivulet.Base.Tests;
using Rivulet.Core;

namespace Rivulet.Csv.Tests;

/// <summary>
///     Tests for CSV parsing and writing using CsvHelper v34 source-generated records.
/// </summary>
public sealed partial class CsvClassMapTests : TempDirectoryFixture
{
    [Fact]
    public async Task ParseCsvParallelAsync_WithMatchingColumnNames_ShouldMapAllFiles()
    {
        // Arrange - columns match property names exactly
        var csvPath1 = Path.Join(TestDirectory, "products1.csv");
        var csvPath2 = Path.Join(TestDirectory, "products2.csv");

        await File.WriteAllTextAsync(csvPath1, "Id,Name,Price\n1,Widget,10.50", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(csvPath2, "Id,Name,Price\n2,Gadget,20.00", TestContext.Current.CancellationToken);

        // Act
        var results = await new[] { csvPath1, csvPath2 }.ParseCsvParallelAsync<Product>(
            new CsvOperationOptions
            {
                ParallelOptions = new ParallelOptionsRivulet { OrderedOutput = true }
            },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        results.Count.ShouldBe(2);
        results.ShouldContain(static p => p.Id == 1 && p.Name == "Widget");
        results.ShouldContain(static p => p.Id == 2 && p.Name == "Gadget");
    }

    [Fact]
    public async Task WriteCsvParallelAsync_ThenParseCsvParallelAsync_ShouldRoundTrip()
    {
        // Arrange
        var products1 = new[] { new Product { Id = 1, Name = "Widget", Price = 10.50m } };
        var products2 = new[] { new Product { Id = 2, Name = "Gadget", Price = 20.00m } };

        var csvPath1 = Path.Join(TestDirectory, "out1.csv");
        var csvPath2 = Path.Join(TestDirectory, "out2.csv");

        // Act - write
        await new[]
            {
                new RivuletCsvWriteFile<Product>(csvPath1, products1, null),
                new RivuletCsvWriteFile<Product>(csvPath2, products2, null)
            }
            .WriteCsvParallelAsync(
                new CsvOperationOptions { OverwriteExisting = true },
                cancellationToken: TestContext.Current.CancellationToken);

        // Act - read back
        var results = await new[] { csvPath1, csvPath2 }.ParseCsvParallelAsync<Product>(
            new CsvOperationOptions
            {
                ParallelOptions = new ParallelOptionsRivulet { OrderedOutput = true }
            },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        results.Count.ShouldBe(2);
        results.ShouldContain(static p => p.Id == 1 && p.Name == "Widget" && p.Price == 10.50m);
        results.ShouldContain(static p => p.Id == 2 && p.Name == "Gadget" && p.Price == 20.00m);
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithSemicolonSeparator_ShouldParse()
    {
        // Arrange
        var csvPath = Path.Join(TestDirectory, "semicolon.csv");
        await File.WriteAllTextAsync(csvPath, "Id;Name;Price\n1;Widget;10.50", TestContext.Current.CancellationToken);

        // Act
        var results = await new[] { csvPath }.ParseCsvParallelAsync<Product>(
            new CsvOperationOptions
            {
                FileConfiguration = new CsvFileConfiguration
                {
                    ReaderConfigurationAction = static opts => opts with { Separator = ';' }
                }
            },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        results.Count.ShouldBe(1);
        results[0].Id.ShouldBe(1);
        results[0].Name.ShouldBe("Widget");
        results[0].Price.ShouldBe(10.50m);
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithPerFileSeparator_ShouldApplyCorrectly()
    {
        // Arrange - different separators per file
        var commaPath = Path.Join(TestDirectory, "comma.csv");
        var pipePath = Path.Join(TestDirectory, "pipe.csv");

        await File.WriteAllTextAsync(commaPath, "Id,Name,Price\n1,Widget,10.50", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(pipePath, "Id|Name|Price\n2|Gadget|20.00", TestContext.Current.CancellationToken);

        var fileReads = new[]
        {
            new RivuletCsvReadFile<Product>(commaPath, null),
            new RivuletCsvReadFile<Product>(pipePath,
                new CsvFileConfiguration
                {
                    ReaderConfigurationAction = static opts => opts with { Separator = '|' }
                })
        };

        // Act
        var results = await fileReads.ParseCsvParallelGroupedAsync(
            new CsvOperationOptions
            {
                ParallelOptions = new ParallelOptionsRivulet { OrderedOutput = true }
            },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        results.Count.ShouldBe(2);
        results[commaPath][0].Name.ShouldBe("Widget");
        results[pipePath][0].Name.ShouldBe("Gadget");
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithoutHeaders_ShouldMapByPosition()
    {
        // Arrange - headerless file, properties mapped by index attribute (Id=0, Name=1, Price=2)
        var csvPath = Path.Join(TestDirectory, "no_header.csv");
        await File.WriteAllTextAsync(csvPath, "1,Widget,10.50\n2,Gadget,20.00", TestContext.Current.CancellationToken);

        // Act
        var results = await new[] { csvPath }.ParseCsvParallelAsync<HeaderlessProduct>(
            new CsvOperationOptions
            {
                FileConfiguration = new CsvFileConfiguration
                {
                    ReaderConfigurationAction = static opts => opts with { HasHeader = false }
                }
            },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        results.Count.ShouldBe(2);
        results.ShouldContain(static p => p.Id == 1 && p.Name == "Widget" && p.Price == 10.50m);
        results.ShouldContain(static p => p.Id == 2 && p.Name == "Gadget" && p.Price == 20.00m);
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithMissingOptionalColumn_ShouldUseDefaultValue()
    {
        // Arrange - CSV has no Description column; auto-mapper skips it, leaves default null
        var csvPath = Path.Join(TestDirectory, "no_description.csv");
        await File.WriteAllTextAsync(csvPath, "Id,Name,Price\n1,Widget,10.50", TestContext.Current.CancellationToken);

        // Act
        var results = await new[] { csvPath }.ParseCsvParallelAsync<Product>(
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        results.Count.ShouldBe(1);
        results[0].Id.ShouldBe(1);
        results[0].Description.ShouldBeNull();
    }

    [Fact]
    public async Task WriteCsvParallelAsync_ThenParseCsvParallelAsync_WithPerFileSettings_ShouldRoundTrip()
    {
        // Arrange - write two files with different separators, read back
        var products1 = new[] { new Product { Id = 1, Name = "Widget", Price = 10.50m } };
        var products2 = new[] { new Product { Id = 2, Name = "Gadget", Price = 20.00m } };

        var csvPath1 = Path.Join(TestDirectory, "comma.csv");
        var csvPath2 = Path.Join(TestDirectory, "pipe.csv");

        var writes = new[]
        {
            new RivuletCsvWriteFile<Product>(csvPath1, products1, null),
            new RivuletCsvWriteFile<Product>(
                csvPath2,
                products2,
                new CsvFileConfiguration
                {
                    WriterConfigurationAction = static opts => opts with { Separator = '|' }
                })
        };

        await writes.WriteCsvParallelAsync(
            new CsvOperationOptions { OverwriteExisting = true },
            cancellationToken: TestContext.Current.CancellationToken);

        // Verify separators
        var content1 = await File.ReadAllTextAsync(csvPath1, TestContext.Current.CancellationToken);
        var content2 = await File.ReadAllTextAsync(csvPath2, TestContext.Current.CancellationToken);

        content1.ShouldContain(",");
        content1.ShouldNotContain("|");
        content2.ShouldContain("|");
        content2.ShouldNotContain(",");

        // Read back pipe-separated file
        var results = await new[] { csvPath2 }.ParseCsvParallelAsync<Product>(
            new CsvOperationOptions
            {
                FileConfiguration = new CsvFileConfiguration
                {
                    ReaderConfigurationAction = static opts => opts with { Separator = '|' }
                }
            },
            cancellationToken: TestContext.Current.CancellationToken);

        results.Count.ShouldBe(1);
        results[0].Id.ShouldBe(2);
        results[0].Name.ShouldBe("Gadget");
    }

    [Fact]
    public async Task TransformCsvParallelAsync_WithAutoMapping_ShouldApplyToInputAndOutput()
    {
        // Arrange - columns match property names
        var inputPath = Path.Join(TestDirectory, "input.csv");
        var outputPath = Path.Join(TestDirectory, "output.csv");
        await File.WriteAllTextAsync(inputPath, "Id,Name,Price\n1,Widget,10.00", TestContext.Current.CancellationToken);

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
            new CsvOperationOptions { OverwriteExisting = true },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var output = await File.ReadAllTextAsync(outputPath, TestContext.Current.CancellationToken);
        output.ShouldContain("Id");
        output.ShouldContain("Name");
        output.ShouldContain("OriginalPrice");
        output.ShouldContain("PriceWithTax");
        output.ShouldContain("12"); // 10.00 * 1.2
    }

    [Fact]
    public async Task TransformCsvParallelAsync_FromHeaderlessInput_ShouldMapByPosition()
    {
        // Arrange - headerless input, tab-separated output
        var inputPath = Path.Join(TestDirectory, "legacy.csv");
        var outputPath = Path.Join(TestDirectory, "modern.csv");
        await File.WriteAllTextAsync(inputPath, "1,Widget,10.00", TestContext.Current.CancellationToken);

        var transformations = new[]
        {
            (
                Input: new RivuletCsvReadFile<HeaderlessProduct>(inputPath,
                    new CsvFileConfiguration
                    {
                        ReaderConfigurationAction = static opts => opts with { HasHeader = false }
                    }),
                Output: new RivuletCsvWriteFile<EnrichedProduct>(outputPath,
                    Array.Empty<EnrichedProduct>(),
                    new CsvFileConfiguration
                    {
                        WriterConfigurationAction = static opts => opts with { Separator = '\t' }
                    })
            )
        };

        // Act
        await transformations.TransformCsvParallelAsync<HeaderlessProduct, EnrichedProduct>(
            static p => new EnrichedProduct
            {
                Id = p.Id,
                Name = p.Name,
                OriginalPrice = p.Price,
                PriceWithTax = p.Price * 1.2m
            },
            new CsvOperationOptions { OverwriteExisting = true },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var output = await File.ReadAllTextAsync(outputPath, TestContext.Current.CancellationToken);
        output.ShouldContain("\t"); // Tab-separated
        output.ShouldContain("12"); // 10.00 * 1.2
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithCustomColumnNames_ShouldIgnoreUnmappedProperty()
    {
        var csvPath = Path.Join(TestDirectory, "custom.csv");
        await File.WriteAllTextAsync(csvPath,
            "ProductName,Price,ProductID,InternalNotes\nWidget,10.50,1,Not imported",
            TestContext.Current.CancellationToken);

        var results = await new[] { csvPath }.ParseCsvParallelAsync<CustomProduct>(
            cancellationToken: TestContext.Current.CancellationToken);

        results.Count.ShouldBe(1);
        results[0].Id.ShouldBe(1);
        results[0].Name.ShouldBe("Widget");
        results[0].Price.ShouldBe(10.50m);
        results[0].InternalNotes.ShouldBeNull();
    }

    [Fact]
    public async Task WriteCsvParallelAsync_WithCustomColumnNames_ShouldRoundTrip()
    {
        var csvPath = Path.Join(TestDirectory, "custom_roundtrip.csv");
        var products = new[]
        {
            new CustomProduct { Id = 1, Name = "Widget", Price = 10.5m, InternalNotes = "Not exported" }
        };

        await new[] { new RivuletCsvWriteFile<CustomProduct>(csvPath, products, null) }
            .WriteCsvParallelAsync(cancellationToken: TestContext.Current.CancellationToken);

        var content = await File.ReadAllTextAsync(csvPath, TestContext.Current.CancellationToken);
        content.ShouldContain("ProductID,ProductName,Price");
        content.ShouldContain("1,Widget,10.5");
        content.ShouldNotContain("InternalNotes");
        content.ShouldNotContain("Not exported");

        var results = await new[] { csvPath }.ParseCsvParallelAsync<CustomProduct>(
            cancellationToken: TestContext.Current.CancellationToken);
        results.Count.ShouldBe(1);
        results[0].Id.ShouldBe(1);
        results[0].Name.ShouldBe("Widget");
        results[0].Price.ShouldBe(10.5m);
        results[0].InternalNotes.ShouldBeNull();
    }

    [Fact]
    public async Task TransformCsvParallelAsync_WithCustomColumnNames_ShouldReadAndWriteMappedNames()
    {
        var inputPath = Path.Join(TestDirectory, "custom_input.csv");
        var outputPath = Path.Join(TestDirectory, "custom_output.csv");
        await File.WriteAllTextAsync(inputPath, "ProductID,ProductName,Price\n1,Widget,10.00", TestContext.Current.CancellationToken);
        var transformations = new[]
        {
            (
                Input: new RivuletCsvReadFile<CustomProduct>(inputPath, null),
                Output: new RivuletCsvWriteFile<CustomProduct>(outputPath, Array.Empty<CustomProduct>(), null)
            )
        };

        await transformations.TransformCsvParallelAsync(
            static p => new CustomProduct { Id = p.Id, Name = p.Name, Price = p.Price * 1.2m },
            cancellationToken: TestContext.Current.CancellationToken);

        var content = await File.ReadAllTextAsync(outputPath, TestContext.Current.CancellationToken);
        content.ShouldContain("ProductID,ProductName,Price");
        content.ShouldContain("1,Widget,12.00");
    }

    [CsvRecord]
    internal sealed partial class CustomProduct
    {
        [CsvColumn("ProductID")]
        public int Id { get; set; }

        [CsvColumn("ProductName")]
        public string Name { get; set; } = string.Empty;

        public decimal Price { get; set; }

        [
            CsvColumn(Optional = true),
            CsvFormat("yyyy-MM-dd")
        ]
        public DateTime CreatedDate { get; set; }

        [CsvIgnore]
        public string? InternalNotes { get; set; }
    }

    [
        CsvRecord,
        SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Local")
    ]
    internal sealed partial class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }

        [CsvColumn(Optional = true)]
        public string? Description { get; set; }
    }

    [
        CsvRecord,
        SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Local")
    ]
    internal sealed partial class HeaderlessProduct
    {
        [CsvColumn(Index = 0)]
        public int Id { get; set; }

        [CsvColumn(Index = 1)]
        public string Name { get; set; } = string.Empty;

        [CsvColumn(Index = 2)]
        public decimal Price { get; set; }
    }

    [
        CsvRecord,
        SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Local")
    ]
    internal sealed partial class EnrichedProduct
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal OriginalPrice { get; set; }
        public decimal PriceWithTax { get; set; }
    }
}
