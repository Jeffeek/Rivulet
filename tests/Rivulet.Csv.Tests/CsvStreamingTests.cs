using System.Diagnostics.CodeAnalysis;
using CsvHelper.Configuration;

namespace Rivulet.Csv.Tests;

public sealed class CsvStreamingTests : IDisposable
{
    private readonly string _testDirectory;

    public CsvStreamingTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"RivuletCsvStreamingTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            // ReSharper disable once ArgumentsStyleLiteral
            Directory.Delete(_testDirectory, recursive: true);
    }

    [Fact]
    public async Task StreamCsvAsync_WithSingleFile_ShouldStreamAllRecords()
    {
        // Arrange
        var csvPath = Path.Combine(_testDirectory, "products.csv");
        const string csvContent = """
                                  Id,Name,Price
                                  1,Product A,10.50
                                  2,Product B,20.00
                                  3,Product C,15.75
                                  """;
        await File.WriteAllTextAsync(csvPath, csvContent);

        // Act
        var records = new List<Product>();
        await foreach (var record in csvPath.StreamCsvAsync<Product>())
            records.Add(record);

        // Assert
        records.Count.ShouldBe(3);
        records.ShouldContain(static p => p.Id == 1 && p.Name == "Product A" && p.Price == 10.50m);
        records.ShouldContain(static p => p.Id == 2 && p.Name == "Product B" && p.Price == 20.00m);
        records.ShouldContain(static p => p.Id == 3 && p.Name == "Product C" && p.Price == 15.75m);
    }

    [Fact]
    public async Task StreamCsvAsync_WithLargeFile_ShouldStreamWithoutLoadingAllIntoMemory()
    {
        // Arrange
        var csvPath = Path.Combine(_testDirectory, "large.csv");
        const int recordCount = 1000;

        await using (var writer = new StreamWriter(csvPath))
        {
            await writer.WriteLineAsync("Id,Name,Price");
            for (var i = 1; i <= recordCount; i++)
                await writer.WriteLineAsync($"{i},Product {i},{i * 10.5:F2}");
        }

        // Act - stream and process one at a time without materializing all
        var sum = 0;
        var count = 0;
        await foreach (var record in csvPath.StreamCsvAsync<Product>())
        {
            sum += record.Id;
            count++;
        }

        // Assert
        count.ShouldBe(recordCount);
        sum.ShouldBe(recordCount * (recordCount + 1) / 2); // Sum of 1 to 1000
    }

    [Fact]
    public async Task StreamCsvAsync_WithConfiguration_ShouldApplyConfiguration()
    {
        // Arrange
        var csvPath = Path.Combine(_testDirectory, "whitespace.csv");
        // ReSharper disable once GrammarMistakeInStringLiteral
        const string csvContent = """
                                  Id,Name,Price
                                  1,  Product A  ,10.50
                                  2,  Product B  ,20.00
                                  """;
        await File.WriteAllTextAsync(csvPath, csvContent);

        var fileConfig = new CsvFileConfiguration
        {
            ConfigurationAction = static cfg => { cfg.TrimOptions = TrimOptions.Trim; }
        };

        // Act
        var records = new List<Product>();
        await foreach (var record in csvPath.StreamCsvAsync<Product>(fileConfig))
            records.Add(record);

        // Assert
        records.Count.ShouldBe(2);
        records[0].Name.ShouldBe("Product A"); // Trimmed
        records[1].Name.ShouldBe("Product B"); // Trimmed
    }

    [Fact]
    public async Task StreamCsvAsync_WithCancellation_ShouldStopStreaming()
    {
        // Arrange
        var csvPath = Path.Combine(_testDirectory, "products.csv");
        // ReSharper disable once GrammarMistakeInStringLiteral
        const string csvContent = """
                                  Id,Name,Price
                                  1,Product A,10.50
                                  2,Product B,20.00
                                  3,Product C,15.75
                                  4,Product D,25.00
                                  5,Product E,30.00
                                  """;
        await File.WriteAllTextAsync(csvPath, csvContent);

        using var cts = new CancellationTokenSource();
        var records = new List<Product>();

        // Act
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await foreach (var record in csvPath.StreamCsvAsync<Product>(cancellationToken: cts.Token))
            {
                records.Add(record);
                if (records.Count == 2)
                    await cts.CancelAsync();
            }
        });

        // Assert - should have processed at least 2 records before cancellation
        records.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task StreamCsvAsync_WhenFileNotFound_ShouldThrow()
    {
        // Arrange
        var csvPath = Path.Combine(_testDirectory, "nonexistent.csv");

        // Act & Assert
        await Should.ThrowAsync<FileNotFoundException>(async () =>
        {
            await foreach (var _ in csvPath.StreamCsvAsync<Product>())
            {
                // Should throw before yielding any records
            }
        });
    }

    [Fact]
    public async Task StreamCsvSequentialAsync_WithMultipleFiles_ShouldStreamInOrder()
    {
        // Arrange
        var file1 = Path.Combine(_testDirectory, "file1.csv");
        var file2 = Path.Combine(_testDirectory, "file2.csv");
        var file3 = Path.Combine(_testDirectory, "file3.csv");

        await File.WriteAllTextAsync(file1, "Id,Name,Price\n1,Product A,10.50");
        await File.WriteAllTextAsync(file2, "Id,Name,Price\n2,Product B,20.00");
        await File.WriteAllTextAsync(file3, "Id,Name,Price\n3,Product C,30.00");

        var filePaths = new[] { file1, file2, file3 };

        // Act
        var records = new List<Product>();
        await foreach (var record in filePaths.StreamCsvSequentialAsync<Product>())
            records.Add(record);

        // Assert
        records.Count.ShouldBe(3);
        records[0].Id.ShouldBe(1);
        records[1].Id.ShouldBe(2);
        records[2].Id.ShouldBe(3);
    }

    [Fact]
    public async Task StreamCsvSequentialAsync_WithEmptyFile_ShouldHandleGracefully()
    {
        // Arrange
        var file1 = Path.Combine(_testDirectory, "file1.csv");
        var file2 = Path.Combine(_testDirectory, "empty.csv");
        var file3 = Path.Combine(_testDirectory, "file3.csv");

        await File.WriteAllTextAsync(file1, "Id,Name,Price\n1,Product A,10.50");
        await File.WriteAllTextAsync(file2, "Id,Name,Price\n"); // Only header
        await File.WriteAllTextAsync(file3, "Id,Name,Price\n3,Product C,30.00");

        var filePaths = new[] { file1, file2, file3 };

        // Act
        var records = new List<Product>();
        await foreach (var record in filePaths.StreamCsvSequentialAsync<Product>())
            records.Add(record);

        // Assert
        records.Count.ShouldBe(2);
        records[0].Id.ShouldBe(1);
        records[1].Id.ShouldBe(3);
    }

    [Fact]
    public async Task StreamCsvSequentialAsync_WithPerFileConfiguration_ShouldApplyConfigurations()
    {
        // Arrange
        var file1 = Path.Combine(_testDirectory, "trimmed.csv");
        var file2 = Path.Combine(_testDirectory, "untrimmed.csv");

        // ReSharper disable GrammarMistakeInStringLiteral
        await File.WriteAllTextAsync(file1, "Id,Name,Price\n1,  Product A  ,10.50");
        await File.WriteAllTextAsync(file2, "Id,Name,Price\n2,  Product B  ,20.00");
        // ReSharper restore GrammarMistakeInStringLiteral

        var fileReads = new[]
        {
            (file1, new CsvFileConfiguration
            {
                ConfigurationAction = static cfg => { cfg.TrimOptions = TrimOptions.Trim; }
            }),
            (file2, new CsvFileConfiguration
            {
                ConfigurationAction = static cfg => { cfg.TrimOptions = TrimOptions.None; }
            })
        };

        // Act
        var records = new List<Product>();
        await foreach (var record in fileReads.StreamCsvSequentialAsync<Product>())
            records.Add(record);

        // Assert
        records.Count.ShouldBe(2);
        records[0].Name.ShouldBe("Product A");     // Trimmed
        records[1].Name.ShouldBe("  Product B  "); // Not trimmed
    }

    [Fact]
    public async Task StreamCsvAsync_WithLifecycleCallbacks_ShouldInvokeCallbacks()
    {
        // Arrange
        var csvPath = Path.Combine(_testDirectory, "products.csv");
        // ReSharper disable once GrammarMistakeInStringLiteral
        const string csvContent = """
                                  Id,Name,Price
                                  1,Product A,10.50
                                  2,Product B,20.00
                                  """;
        await File.WriteAllTextAsync(csvPath, csvContent);

        var startCalled = false;
        var completeCalled = false;
        string? startFilePath = null;
        string? completeFilePath = null;
        long recordsProcessed = 0;

        var options = new CsvOperationOptions
        {
            OnFileStartAsync = async filePath =>
            {
                startCalled = true;
                startFilePath = filePath;
                await Task.CompletedTask;
            },
            OnFileCompleteAsync = async (filePath, count) =>
            {
                completeCalled = true;
                completeFilePath = filePath;
                recordsProcessed = count;
                await Task.CompletedTask;
            }
        };

        // Act
        var records = new List<Product>();
        await foreach (var record in csvPath.StreamCsvAsync<Product>(options))
            records.Add(record);

        // Assert
        startCalled.ShouldBeTrue();
        completeCalled.ShouldBeTrue();
        startFilePath.ShouldBe(csvPath);
        completeFilePath.ShouldBe(csvPath);
        recordsProcessed.ShouldBe(2);
        records.Count.ShouldBe(2);
    }

    [Fact]
    public async Task StreamCsvSequentialAsync_WithMultipleFiles_ShouldCallLifecycleCallbacksForEach()
    {
        // Arrange
        var file1 = Path.Combine(_testDirectory, "file1.csv");
        var file2 = Path.Combine(_testDirectory, "file2.csv");

        await File.WriteAllTextAsync(file1, "Id,Name,Price\n1,Product A,10.50");
        await File.WriteAllTextAsync(file2, "Id,Name,Price\n2,Product B,20.00");

        var filesStarted = new List<string>();
        var filesCompleted = new List<string>();

        var options = new CsvOperationOptions
        {
            OnFileStartAsync = async filePath =>
            {
                filesStarted.Add(filePath);
                await Task.CompletedTask;
            },
            OnFileCompleteAsync = async (filePath, _) =>
            {
                filesCompleted.Add(filePath);
                await Task.CompletedTask;
            }
        };

        // Act
        var records = new List<Product>();
        await foreach (var record in new[] { file1, file2 }.StreamCsvSequentialAsync<Product>(options))
            records.Add(record);

        // Assert
        filesStarted.Count.ShouldBe(2);
        filesCompleted.Count.ShouldBe(2);
        filesStarted.ShouldContain(file1);
        filesStarted.ShouldContain(file2);
        filesCompleted.ShouldContain(file1);
        filesCompleted.ShouldContain(file2);
        records.Count.ShouldBe(2);
    }

    [Fact]
    public async Task StreamCsvAsync_WithEarlyBreak_ShouldNotProcessRemainingRecords()
    {
        // Arrange
        var csvPath = Path.Combine(_testDirectory, "products.csv");
        // ReSharper disable once GrammarMistakeInStringLiteral
        const string csvContent = """
                                  Id,Name,Price
                                  1,Product A,10.50
                                  2,Product B,20.00
                                  3,Product C,15.75
                                  4,Product D,25.00
                                  5,Product E,30.00
                                  """;
        await File.WriteAllTextAsync(csvPath, csvContent);

        // Act
        var records = new List<Product>();
        await foreach (var record in csvPath.StreamCsvAsync<Product>())
        {
            records.Add(record);
            if (records.Count == 2)
                break; // Early exit
        }

        // Assert
        records.Count.ShouldBe(2);
        records[0].Id.ShouldBe(1);
        records[1].Id.ShouldBe(2);
    }

    [Fact]
    public async Task StreamCsvSequentialAsync_WithNullConfiguration_ShouldThrow()
    {
        // Arrange
        var file1 = Path.Combine(_testDirectory, "file1.csv");
        await File.WriteAllTextAsync(file1, "Id,Name,Price\n1,Product A,10.50");

        var fileReads = new[]
        {
            (file1, (CsvFileConfiguration)null!)
        };

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in fileReads.StreamCsvSequentialAsync<Product>())
            {
                // Should throw before yielding
            }
        });
    }

    [Fact]
    public async Task StreamCsvAsync_ComparingWithParseAsync_ShouldYieldSameResults()
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
        var streamedRecords = new List<Product>();
        await foreach (var record in csvPath.StreamCsvAsync<Product>())
            streamedRecords.Add(record);

        var parsedRecords = await new[] { csvPath }.ParseCsvParallelAsync<Product>();

        // Assert - both methods should yield the same records (order may differ for parallel)
        streamedRecords.Count.ShouldBe(parsedRecords.Count);
        foreach (var streamed in streamedRecords)
        {
            parsedRecords.ShouldContain(p =>
                p.Id == streamed.Id &&
                p.Name == streamed.Name &&
                p.Price == streamed.Price);
        }
    }

    [Fact]
    public async Task StreamCsvSequentialAsync_WithClassMap_ShouldApplyMapping()
    {
        // Arrange
        var csvPath = Path.Combine(_testDirectory, "custom.csv");
        const string csvContent = """
                                  ProductID,ProductName,Price
                                  1,Widget,10.50
                                  2,Gadget,20.00
                                  """;
        await File.WriteAllTextAsync(csvPath, csvContent);

        var fileConfig = new CsvFileConfiguration
        {
            CsvContextAction = static ctx => { ctx.RegisterClassMap<ProductClassMap>(); }
        };

        var fileReads = new[] { (csvPath, fileConfig) };

        // Act
        var records = new List<Product>();
        await foreach (var record in fileReads.StreamCsvSequentialAsync<Product>())
            records.Add(record);

        // Assert
        records.Count.ShouldBe(2);
        records[0].Name.ShouldBe("Widget");
        records[1].Name.ShouldBe("Gadget");
    }

    [Fact]
    public async Task StreamCsvSequentialAsync_ProcessingMultipleFiles_ShouldMaintainOrder()
    {
        // Arrange
        const int fileCount = 5;
        var filePaths = new List<string>();

        for (var i = 1; i <= fileCount; i++)
        {
            var filePath = Path.Combine(_testDirectory, $"file{i}.csv");
            await File.WriteAllTextAsync(filePath, $"Id,Name,Price\n{i},Product {i},{i * 10}.00");
            filePaths.Add(filePath);
        }

        // Act
        var records = new List<Product>();
        await foreach (var record in filePaths.StreamCsvSequentialAsync<Product>())
            records.Add(record);

        // Assert
        records.Count.ShouldBe(fileCount);
        for (var i = 0; i < fileCount; i++)
        {
            records[i].Id.ShouldBe(i + 1);
            records[i].Name.ShouldBe($"Product {i + 1}");
        }
    }

    [
        SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Local"),
        SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local"),
        SuppressMessage("ReSharper", "ReplaceAutoPropertyWithComputedProperty"),
        SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")
    ]
    private sealed class Product
    {
        public int Id { get; set; }
        public string Name { get; } = string.Empty;
        public decimal Price { get; set; }
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private sealed class ProductClassMap : ClassMap<Product>
    {
        public ProductClassMap()
        {
            Map(static m => m.Id).Name("ProductID");
            Map(static m => m.Name).Name("ProductName");
            Map(static m => m.Price).Name("Price");
        }
    }
}
