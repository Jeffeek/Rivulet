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

        // Act - Using single ClassMap for all files via CsvContextAction
        var results = await new[] { csvPath1, csvPath2 }.ParseCsvParallelAsync<Product>(
            new CsvOperationOptions
            {
                FileConfiguration = new CsvFileConfiguration
                {
                    CsvContextAction = ctx => ctx.RegisterClassMap<ProductMapByName>()
                },
                ParallelOptions = new ParallelOptionsRivulet { OrderedOutput = true }
            });

        // Assert - order-independent
        results.Count.ShouldBe(2);
        results.ShouldContain(p => p.Id == 1 && p.Name == "Widget");
        results.ShouldContain(p => p.Id == 2 && p.Name == "Gadget");
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithPerFileCallback_ShouldApplyDifferentMaps()
    {
        // Arrange
        var csvPath1 = Path.Combine(_testDirectory, "modern_products.csv");
        var csvPath2 = Path.Combine(_testDirectory, "legacy_products.csv");

        await File.WriteAllTextAsync(csvPath1, "ProductID,ProductName,Price\n1,Widget,10.50");
        await File.WriteAllTextAsync(csvPath2, "1|OldWidget|5.25");

        // Act - Configure per file using tuple-based approach
        var fileReads = new[]
        {
            (csvPath1, new CsvFileConfiguration
            {
                CsvContextAction = ctx => ctx.RegisterClassMap<ProductMapByName>()
            }),
            (csvPath2, new CsvFileConfiguration
            {
                ReaderConfigurationAction = cfg =>
                {
                    if (cfg is CsvHelper.Configuration.CsvConfiguration csvConfig)
                    {
                        csvConfig.Delimiter = "|";
                        csvConfig.HasHeaderRecord = false;
                    }
                },
                CsvContextAction = ctx => ctx.RegisterClassMap<ProductMapByIndex>()
            })
        };

        var results = await fileReads.ParseCsvParallelAsync(
            new CsvOperationOptions
            {
                ParallelOptions = new ParallelOptionsRivulet { OrderedOutput = true }
            });

        // Assert
        results.Count.ShouldBe(2);
        ((Product)results[csvPath1][0]).Name.ShouldBe("Widget");
        ((Product)results[csvPath2][0]).Name.ShouldBe("OldWidget");
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

        var fileReads = new[]
        {
            (file1, new CsvFileConfiguration { CsvContextAction = ctx => ctx.RegisterClassMap<ProductMapByName>() }),
            (file2, new CsvFileConfiguration { CsvContextAction = ctx => ctx.RegisterClassMap<ProductMapByName>() }),
            (file3, new CsvFileConfiguration { CsvContextAction = ctx => ctx.RegisterClassMap<ProductMapByName>() }),
            (file4, new CsvFileConfiguration
            {
                ReaderConfigurationAction = cfg =>
                {
                    if (cfg is CsvHelper.Configuration.CsvConfiguration csvConfig)
                        csvConfig.HasHeaderRecord = false;
                },
                CsvContextAction = ctx => ctx.RegisterClassMap<ProductMapByIndex>()
            }),
            (file5, new CsvFileConfiguration { CsvContextAction = ctx => ctx.RegisterClassMap<ProductMapWithOptional>() })
        };

        // Act
        var results = await fileReads.ParseCsvParallelAsync(
            new CsvOperationOptions
            {
                ParallelOptions = new ParallelOptionsRivulet { OrderedOutput = true }
            });

        // Assert - Verify all 5 files parsed with correct ClassMaps
        results.Count.ShouldBe(5);
        ((Product)results[file1][0]).Id.ShouldBe(1);
        ((Product)results[file1][0]).Name.ShouldBe("Widget");
        ((Product)results[file2][0]).Id.ShouldBe(2);
        ((Product)results[file3][0]).Id.ShouldBe(3);
        ((Product)results[file4][0]).Id.ShouldBe(4); // Parsed by index
        ((Product)results[file5][0]).Id.ShouldBe(5);
        ((Product)results[file5][0]).Description.ShouldBe("Special");
    }

    [Fact]
    public async Task WriteCsvParallelAsync_WithSingleClassMap_ShouldApplyToAllFiles()
    {
        // Arrange
        var products1 = new[] { new Product { Id = 1, Name = "Widget", Price = 10.50m } };
        var products2 = new[] { new Product { Id = 2, Name = "Gadget", Price = 20.00m } };

        var csvPath1 = Path.Combine(_testDirectory, "out1.csv");
        var csvPath2 = Path.Combine(_testDirectory, "out2.csv");

        // Act - Using single ClassMap for all writes via CsvContextAction
        await new[] { (csvPath1, (IEnumerable<Product>)products1), (csvPath2, (IEnumerable<Product>)products2) }
            .WriteCsvParallelAsync(
                new CsvOperationOptions
                {
                    FileConfiguration = new CsvFileConfiguration
                    {
                        CsvContextAction = ctx => ctx.RegisterClassMap<ProductMapByName>()
                    },
                    OverwriteExisting = true
                });

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

        var fileWrites = new[]
        {
            (csvPath1, (IEnumerable<Product>)products1, new CsvFileConfiguration
            {
                CsvContextAction = ctx => ctx.RegisterClassMap<ProductMapByName>()
            }),
            (csvPath2, (IEnumerable<Product>)products2, new CsvFileConfiguration
            {
                CsvContextAction = ctx => ctx.RegisterClassMap<ProductMapWithOptional>()
            })
        };

        // Act
        await fileWrites.WriteCsvParallelAsync(
            new CsvOperationOptions { OverwriteExisting = true });

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

        // Act - Configure delimiter and ClassMap via CsvFileConfiguration
        var results = await new[] { csvPath }.ParseCsvParallelAsync<Product>(
            new CsvOperationOptions
            {
                FileConfiguration = new CsvFileConfiguration
                {
                    ReaderConfigurationAction = cfg =>
                    {
                        if (cfg is CsvHelper.Configuration.CsvConfiguration csvConfig)
                            csvConfig.Delimiter = ";";
                    },
                    CsvContextAction = ctx => ctx.RegisterClassMap<ProductMapByName>()
                }
            });

        // Assert
        results[0].Name.ShouldBe("Widget");
    }

    [Fact]
    public async Task WriteCsvParallelAsync_WithTupleConfiguration_ShouldSupportPerFileSettings()
    {
        // Arrange
        var products1 = new[] { new Product { Id = 1, Name = "Widget", Price = 10.50m } };
        var products2 = new[] { new Product { Id = 2, Name = "Gadget", Price = 20.00m } };

        var csvPath1 = Path.Combine(_testDirectory, "comma.csv");
        var csvPath2 = Path.Combine(_testDirectory, "pipe.csv");

        // Act - Different delimiter per file using CsvFileConfiguration
        var writes = new[]
        {
            (csvPath1, (IEnumerable<Product>)products1, new CsvFileConfiguration
            {
                WriterConfigurationAction = cfg =>
                {
                    if (cfg is CsvHelper.Configuration.CsvConfiguration csvConfig)
                        csvConfig.Delimiter = ",";
                },
                CsvContextAction = ctx => ctx.RegisterClassMap<ProductMapByName>()
            }),
            (csvPath2, (IEnumerable<Product>)products2, new CsvFileConfiguration
            {
                WriterConfigurationAction = cfg =>
                {
                    if (cfg is CsvHelper.Configuration.CsvConfiguration csvConfig)
                        csvConfig.Delimiter = "|";
                },
                CsvContextAction = ctx => ctx.RegisterClassMap<ProductMapByName>()
            })
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

        // Act - Using ClassMap that ignores Internal field via CsvContextAction
        var results = await new[] { csvPath }.ParseCsvParallelAsync<ProductWithInternal>(
            new CsvOperationOptions
            {
                FileConfiguration = new CsvFileConfiguration
                {
                    CsvContextAction = ctx => ctx.RegisterClassMap<ProductMapIgnoringInternal>()
                }
            });

        // Assert
        results[0].Id.ShouldBe(1);
        results[0].Name.ShouldBe("Widget");
        results[0].Internal.ShouldBeNull(); // Should be ignored, remain null
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

        var transformations = new[]
        {
            (inputPath, outputPath,
             new CsvFileConfiguration { CsvContextAction = ctx => ctx.RegisterClassMap<ProductMapByName>() },
             new CsvFileConfiguration { CsvContextAction = ctx => ctx.RegisterClassMap<EnrichedProductMap>() })
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

        var transformations = new[]
        {
            (inputPath, outputPath,
             new CsvFileConfiguration
             {
                 ReaderConfigurationAction = cfg =>
                 {
                     if (cfg is CsvHelper.Configuration.CsvConfiguration csvConfig)
                         csvConfig.HasHeaderRecord = false;
                 },
                 CsvContextAction = ctx => ctx.RegisterClassMap<ProductMapByIndex>()
             },
             new CsvFileConfiguration
             {
                 WriterConfigurationAction = cfg =>
                 {
                     if (cfg is CsvHelper.Configuration.CsvConfiguration csvConfig)
                         csvConfig.Delimiter = "\t";
                 },
                 CsvContextAction = ctx => ctx.RegisterClassMap<EnrichedProductMap>()
             })
        };

        // Act - Input: no header, comma; Output: header, tab-separated
        await transformations.TransformCsvParallelAsync<Product, EnrichedProduct>(
            static p => new EnrichedProduct
            {
                Id = p.Id,
                Name = p.Name,
                OriginalPrice = p.Price,
                PriceWithTax = p.Price * 1.2m
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
        // Handle exception wrapping in parallel operations
        try
        {
            await new[] { csvPath }.ParseCsvParallelAsync<Product>(
                new CsvOperationOptions
                {
                    FileConfiguration = new CsvFileConfiguration
                    {
                        CsvContextAction = ctx => ctx.RegisterClassMap<ProductMapByName>()
                    }
                });

            // If we get here, the test should fail
            throw new InvalidOperationException("Expected an exception but none was thrown");
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            // The expected exception or one of its inner exceptions should be HeaderValidationException
            var actualException = ex;
            var found = false;
            while (actualException != null)
            {
                if (actualException is CsvHelper.HeaderValidationException)
                {
                    found = true;
                    break;
                }
                actualException = actualException.InnerException;
            }
            found.ShouldBeTrue("Expected HeaderValidationException in exception chain");
        }
    }

}
