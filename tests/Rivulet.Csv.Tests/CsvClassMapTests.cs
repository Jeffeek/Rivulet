using System.Diagnostics.CodeAnalysis;
using CsvHelper.Configuration;
using Rivulet.Core;

namespace Rivulet.Csv.Tests;

public sealed class CsvClassMapTests : IDisposable
{
    private readonly string _testDirectory;

    public CsvClassMapTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"RivuletCsvClassMapTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            // ReSharper disable once ArgumentsStyleLiteral
            Directory.Delete(_testDirectory, recursive: true);
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithSingleClassMap_ShouldApplyToAllFiles()
    {
        // Arrange
        var csvPath1 = Path.Combine(_testDirectory, "products1.csv");
        var csvPath2 = Path.Combine(_testDirectory, "products2.csv");

        await File.WriteAllTextAsync(csvPath1, "ProductID,ProductName,Price\n1,Widget,10.50");
        await File.WriteAllTextAsync(csvPath2, "ProductID,ProductName,Price\n2,Gadget,20.00");

        // Act - Using single ClassMap for all files
        var results = await new[] { csvPath1, csvPath2 }.ParseCsvParallelAsync<Product, ProductMapByName>(
            new CsvOperationOptions
            {
                ParallelOptions = new ParallelOptionsRivulet { OrderedOutput = true }
            });

        // Assert
        results.Count.ShouldBe(2);
        results[0][0].Id.ShouldBe(1);
        results[0][0].Name.ShouldBe("Widget");
        results[1][0].Id.ShouldBe(2);
        results[1][0].Name.ShouldBe("Gadget");
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithPerFileCallback_ShouldApplyDifferentMaps()
    {
        // Arrange
        var csvPath1 = Path.Combine(_testDirectory, "modern_products.csv");
        var csvPath2 = Path.Combine(_testDirectory, "legacy_products.csv");

        await File.WriteAllTextAsync(csvPath1, "ProductID,ProductName,Price\n1,Widget,10.50");
        await File.WriteAllTextAsync(csvPath2, "1|OldWidget|5.25");

        // Act - Configure per file based on file name
        var results = await new[] { csvPath1, csvPath2 }.ParseCsvParallelAsync<Product>(static filePath => ctx =>
            {
                if (filePath.Contains("legacy"))
                {
                    ctx.Configuration.Delimiter = "|";
                    ctx.Configuration.HasHeaderRecord = false;
                    ctx.RegisterClassMap<ProductMapByIndex>();
                }
                else
                    ctx.RegisterClassMap<ProductMapByName>();
            },
            new CsvOperationOptions
            {
                ParallelOptions = new ParallelOptionsRivulet { OrderedOutput = true }
            });

        // Assert
        results.Count.ShouldBe(2);
        results[0][0].Name.ShouldBe("Widget");
        results[1][0].Name.ShouldBe("OldWidget");
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithCsvFileConfig_ShouldUsePerFileClassMaps()
    {
        // Arrange - Your exact scenario: 5 files with 3 different ClassMaps
        var file1 = Path.Combine(_testDirectory, "file1.csv");
        var file2 = Path.Combine(_testDirectory, "file2.csv");
        var file3 = Path.Combine(_testDirectory, "file3.csv");
        var file4 = Path.Combine(_testDirectory, "file4.csv");
        var file5 = Path.Combine(_testDirectory, "file5.csv");

        // Files 1-3 use ProductMapByName
        await File.WriteAllTextAsync(file1, "ProductID,ProductName,Price\n1,Widget,10.00");
        await File.WriteAllTextAsync(file2, "ProductID,ProductName,Price\n2,Gadget,20.00");
        await File.WriteAllTextAsync(file3, "ProductID,ProductName,Price\n3,Doohickey,30.00");

        // File 4 uses ProductMapByIndex (no header)
        await File.WriteAllTextAsync(file4, "4,Thingamajig,40.00");

        // File 5 uses ProductMapWithOptional (has optional Description column)
        await File.WriteAllTextAsync(file5, "ProductID,ProductName,Price,Description\n5,Whatsit,50.00,Special");

        var fileConfigs = new[]
        {
            new CsvFileConfig<Product>(file1, static ctx => ctx.RegisterClassMap<ProductMapByName>()),
            new CsvFileConfig<Product>(file2, static ctx => ctx.RegisterClassMap<ProductMapByName>()),
            new CsvFileConfig<Product>(file3, static ctx => ctx.RegisterClassMap<ProductMapByName>()),
            new CsvFileConfig<Product>(file4,
                static ctx =>
                {
                    ctx.Configuration.HasHeaderRecord = false;
                    ctx.RegisterClassMap<ProductMapByIndex>();
                }),
            new CsvFileConfig<Product>(file5, static ctx => ctx.RegisterClassMap<ProductMapWithOptional>())
        };

        // Act
        var results = await fileConfigs.ParseCsvParallelAsync(
            new CsvOperationOptions
            {
                ParallelOptions = new ParallelOptionsRivulet { OrderedOutput = true }
            });

        // Assert - Verify all 5 files parsed with correct ClassMaps
        results.Count.ShouldBe(5);
        results[0][0].Id.ShouldBe(1);
        results[0][0].Name.ShouldBe("Widget");
        results[1][0].Id.ShouldBe(2);
        results[2][0].Id.ShouldBe(3);
        results[3][0].Id.ShouldBe(4); // Parsed by index
        results[4][0].Id.ShouldBe(5);
        results[4][0].Description.ShouldBe("Special");
    }

    [Fact]
    public async Task WriteCsvParallelAsync_WithSingleClassMap_ShouldApplyToAllFiles()
    {
        // Arrange
        var products1 = new[] { new Product { Id = 1, Name = "Widget", Price = 10.50m } };
        var products2 = new[] { new Product { Id = 2, Name = "Gadget", Price = 20.00m } };

        var csvPath1 = Path.Combine(_testDirectory, "out1.csv");
        var csvPath2 = Path.Combine(_testDirectory, "out2.csv");

        // Act - Using single ClassMap for all writes
        await new[] { (csvPath1, (IEnumerable<Product>)products1), (csvPath2, (IEnumerable<Product>)products2) }
            .WriteCsvParallelAsync<Product, ProductMapByName>(
                new CsvOperationOptions { OverwriteExisting = true });

        // Assert
        var content1 = await File.ReadAllTextAsync(csvPath1);
        var content2 = await File.ReadAllTextAsync(csvPath2);

        content1.ShouldContain("ProductID");
        content1.ShouldContain("ProductName");
        content1.ShouldContain("Widget");

        content2.ShouldContain("ProductID");
        content2.ShouldContain("Gadget");
    }

    [Fact]
    public async Task WriteCsvParallelAsync_WithCsvFileConfig_ShouldUsePerFileClassMaps()
    {
        // Arrange
        var products1 = new[] { new Product { Id = 1, Name = "Widget", Price = 10.50m } };
        var products2 = new[] { new Product { Id = 2, Name = "Gadget", Price = 20.00m, Description = "Special" } };

        var csvPath1 = Path.Combine(_testDirectory, "out_by_name.csv");
        var csvPath2 = Path.Combine(_testDirectory, "out_with_optional.csv");

        var fileConfigs = new[]
        {
            CsvFileConfig<Product>.ForWrite(csvPath1, products1, static ctx => ctx.RegisterClassMap<ProductMapByName>()),
            CsvFileConfig<Product>.ForWrite(csvPath2, products2, static ctx => ctx.RegisterClassMap<ProductMapWithOptional>())
        };

        // Act
        await fileConfigs.WriteCsvParallelAsync(
            options: new CsvOperationOptions { OverwriteExisting = true });

        // Assert
        var content1 = await File.ReadAllTextAsync(csvPath1);
        var content2 = await File.ReadAllTextAsync(csvPath2);

        content1.ShouldContain("ProductName");
        content1.ShouldNotContain("Description");

        content2.ShouldContain("ProductName");
        content2.ShouldContain("Description");
        content2.ShouldContain("Special");
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithConfigureContext_ShouldAccessConfiguration()
    {
        // Arrange
        var csvPath = Path.Combine(_testDirectory, "semicolon.csv");
        await File.WriteAllTextAsync(csvPath, "ProductID;ProductName;Price\n1;Widget;10.50");

        // Act - Configure delimiter via CsvContext
        var results = await new[] { csvPath }.ParseCsvParallelAsync<Product>(static ctx =>
        {
            ctx.Configuration.Delimiter = ";";
            ctx.RegisterClassMap<ProductMapByName>();
        });

        // Assert
        results[0][0].Name.ShouldBe("Widget");
    }

    [Fact]
    public async Task WriteCsvParallelAsync_WithTupleConfiguration_ShouldSupportPerFileSettings()
    {
        // Arrange
        var products1 = new[] { new Product { Id = 1, Name = "Widget", Price = 10.50m } };
        var products2 = new[] { new Product { Id = 2, Name = "Gadget", Price = 20.00m } };

        var csvPath1 = Path.Combine(_testDirectory, "comma.csv");
        var csvPath2 = Path.Combine(_testDirectory, "pipe.csv");

        // Act - Different delimiter per file
        var writes = new[]
        {
            (csvPath1, (IEnumerable<Product>)products1, (Action<CsvContext>)(static ctx =>
            {
                ctx.Configuration.Delimiter = ",";
                ctx.RegisterClassMap<ProductMapByName>();
            })),
            (csvPath2, (IEnumerable<Product>)products2, (Action<CsvContext>)(static ctx =>
            {
                ctx.Configuration.Delimiter = "|";
                ctx.RegisterClassMap<ProductMapByName>();
            }))
        };

        await writes.WriteCsvParallelAsync(
            new CsvOperationOptions { OverwriteExisting = true });

        // Assert
        var content1 = await File.ReadAllTextAsync(csvPath1);
        var content2 = await File.ReadAllTextAsync(csvPath2);

        content1.ShouldContain(",");
        content1.ShouldNotContain("|");

        content2.ShouldContain("|");
        content2.ShouldNotContain(",");
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithClassMapIgnoringFields_ShouldIgnoreCorrectly()
    {
        // Arrange
        var csvPath = Path.Combine(_testDirectory, "with_extra.csv");
        await File.WriteAllTextAsync(csvPath, "ProductID,ProductName,Price,Internal\n1,Widget,10.50,secret");

        // Act - Using ClassMap that ignores Internal field
        var results = await new[] { csvPath }.ParseCsvParallelAsync<ProductWithInternal, ProductMapIgnoringInternal>();

        // Assert
        results[0][0].Id.ShouldBe(1);
        results[0][0].Name.ShouldBe("Widget");
        results[0][0].Internal.ShouldBeNull(); // Should be ignored, remain null
    }

    // Test classes and ClassMaps

    [SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Local")]
    private sealed class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string? Description { get; set; }
    }

    [
        SuppressMessage("ReSharper", "ClassNeverInstantiated.Local"),
        SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local"),
        SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Local")
    ]
    private sealed class ProductWithInternal
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string? Internal { get; set; }
    }

    // ClassMap that maps by name
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private sealed class ProductMapByName : ClassMap<Product>
    {
        public ProductMapByName()
        {
            Map(static m => m.Id).Name("ProductID");
            Map(static m => m.Name).Name("ProductName");
            Map(static m => m.Price);
        }
    }

    // ClassMap that maps by index (for headerless files)
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private sealed class ProductMapByIndex : ClassMap<Product>
    {
        public ProductMapByIndex()
        {
            Map(static m => m.Id).Index(0);
            Map(static m => m.Name).Index(1);
            Map(static m => m.Price).Index(2);
        }
    }

    // ClassMap with optional field
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private sealed class ProductMapWithOptional : ClassMap<Product>
    {
        public ProductMapWithOptional()
        {
            Map(static m => m.Id).Name("ProductID");
            Map(static m => m.Name).Name("ProductName");
            Map(static m => m.Price);
            Map(static m => m.Description).Optional();
        }
    }

    // ClassMap that ignores a field
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private sealed class ProductMapIgnoringInternal : ClassMap<ProductWithInternal>
    {
        public ProductMapIgnoringInternal()
        {
            Map(static m => m.Id).Name("ProductID");
            Map(static m => m.Name).Name("ProductName");
            Map(static m => m.Price);
            Map(static m => m.Internal).Ignore();
        }
    }

    // EnrichedProduct for Transform tests
    [SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Local")]
    private sealed class EnrichedProduct
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal OriginalPrice { get; set; }
        public decimal PriceWithTax { get; set; }
    }

    // ClassMap for EnrichedProduct
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private sealed class EnrichedProductMap : ClassMap<EnrichedProduct>
    {
        public EnrichedProductMap()
        {
            Map(static m => m.Id).Name("ProductID");
            Map(static m => m.Name).Name("ProductName");
            Map(static m => m.OriginalPrice);
            Map(static m => m.PriceWithTax);
        }
    }

    // Transform ClassMap tests

    [Fact]
    public async Task TransformCsvParallelAsync_WithSingleClassMaps_ShouldApplyToInputAndOutput()
    {
        // Arrange
        var inputPath = Path.Combine(_testDirectory, "input.csv");
        var outputPath = Path.Combine(_testDirectory, "output.csv");
        await File.WriteAllTextAsync(inputPath, "ProductID,ProductName,Price\n1,Widget,10.00");

        var transformations = new[] { (inputPath, outputPath) };

        // Act
        await transformations.TransformCsvParallelAsync<Product, EnrichedProduct, ProductMapByName, EnrichedProductMap>(
            static async (_, products) =>
            {
                await Task.CompletedTask;
                return products.Select(static p => new EnrichedProduct
                {
                    Id = p.Id,
                    Name = p.Name,
                    OriginalPrice = p.Price,
                    PriceWithTax = p.Price * 1.2m
                });
            },
            new CsvOperationOptions { OverwriteExisting = true });

        // Assert
        var output = await File.ReadAllTextAsync(outputPath);
        output.ShouldContain("ProductID");
        output.ShouldContain("ProductName");
        output.ShouldContain("OriginalPrice");
        output.ShouldContain("PriceWithTax");
        output.ShouldContain("12"); // 10.00 * 1.2
    }

    [Fact]
    public async Task TransformCsvParallelAsync_WithSeparateContextConfigurations_ShouldApplyCorrectly()
    {
        // Arrange
        var inputPath = Path.Combine(_testDirectory, "legacy.csv");
        var outputPath = Path.Combine(_testDirectory, "modern.csv");
        await File.WriteAllTextAsync(inputPath, "1,Widget,10.00"); // No header, comma-separated

        var transformations = new[] { (inputPath, outputPath) };

        // Act - Input: no header, comma; Output: header, tab-separated
        await transformations.TransformCsvParallelAsync<Product, EnrichedProduct>(
            static async (_, products) =>
            {
                await Task.CompletedTask;
                return products.Select(static p => new EnrichedProduct
                {
                    Id = p.Id,
                    Name = p.Name,
                    OriginalPrice = p.Price,
                    PriceWithTax = p.Price * 1.2m
                });
            },
            static ctx =>
            {
                ctx.Configuration.HasHeaderRecord = false;
                ctx.RegisterClassMap<ProductMapByIndex>();
            },
            static ctx =>
            {
                ctx.Configuration.Delimiter = "\t";
                ctx.RegisterClassMap<EnrichedProductMap>();
            },
            new CsvOperationOptions { OverwriteExisting = true });

        // Assert
        var output = await File.ReadAllTextAsync(outputPath);
        output.ShouldContain("\t"); // Tab-separated
        output.ShouldContain("ProductID");
        output.ShouldContain("12"); // 10.00 * 1.2
    }

    // Error scenario tests

    [Fact]
    public async Task ParseCsvParallelAsync_WithClassMapForNonExistentColumn_ShouldThrow()
    {
        // Arrange
        var csvPath = Path.Combine(_testDirectory, "file.csv");
        await File.WriteAllTextAsync(csvPath, "Id,Name,Price\n1,Widget,10.50");

        // Act & Assert - ClassMap expects "ProductID" but file has "Id"
        await Should.ThrowAsync<CsvHelper.HeaderValidationException>(async () =>
        {
            await new[] { csvPath }.ParseCsvParallelAsync<Product, ProductMapByName>();
        });
    }

    [Fact]
    public void CsvFileConfig_WithNullFilePath_ShouldThrow() =>
        // Act & Assert
        Should.Throw<ArgumentException>(() =>
        {
            _ = new CsvFileConfig<Product>(null!, static ctx => ctx.RegisterClassMap<ProductMapByName>());
        });

    [Fact]
    public void CsvFileConfig_ForWrite_WithNullRecords_ShouldThrow() =>
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
        {
            _ = CsvFileConfig<Product>.ForWrite("output.csv", null!, static ctx => ctx.RegisterClassMap<ProductMapByName>());
        });

    [Fact]
    public async Task WriteCsvParallelAsync_WithCsvFileConfigMissingRecords_ShouldThrow()
    {
        // Arrange - Create config for reading (no records)
        var csvPath = Path.Combine(_testDirectory, "output.csv");
        var fileConfig = new CsvFileConfig<Product>(csvPath, static ctx => ctx.RegisterClassMap<ProductMapByName>());

        // Act & Assert - Should throw because Records is null (not created with ForWrite)
        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await new[] { fileConfig }.WriteCsvParallelAsync(options: new CsvOperationOptions { OverwriteExisting = true });
        });
    }
}
