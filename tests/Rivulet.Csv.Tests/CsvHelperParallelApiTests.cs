using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using CsvHelper;
using Rivulet.Base.Tests;
using Rivulet.Core;

namespace Rivulet.Csv.Tests;

/// <summary>
/// Tests comparing CsvHelper 34's built-in parallel APIs vs Rivulet's file-level parallelism.
/// CsvHelper parallelizes within one file (record formatting/parsing on multiple threads).
/// Rivulet parallelizes across multiple files (process N files at once, each on one thread).
/// </summary>
public sealed partial class CsvHelperParallelApiTests : TempDirectoryFixture
{
    [Fact]
    public async Task CompareApproaches_SingleLargeFile_VsMultipleSmallFiles()
    {
        // Scenario: Process 10,000 records
        // Approach 1: CsvHelper ParallelEnumerate on 1 file with 10k records
        // Approach 2: Rivulet on 10 files with 1k records each

        var allRecords = Enumerable.Range(1, 10000).Select(static i => new Product { Id = i, Name = $"Product {i}", Price = i * 10m }).ToList();

        // Setup: 1 large file
        var largeFile = Path.Join(TestDirectory, "large.csv");
        await File.WriteAllLinesAsync(
            largeFile,
            new[] { "Id,Name,Price" }
                .Concat(allRecords.Select(static r => $"{r.Id},{r.Name},{r.Price}")),
            TestContext.Current.CancellationToken);

        // Setup: 10 small files
        var smallFiles = new List<string>();
        for (var i = 0; i < 10; i++)
        {
            var file = Path.Join(TestDirectory, $"small_{i}.csv");
            var chunk = allRecords.Skip(i * 1000).Take(1000);
            await File.WriteAllLinesAsync(file, new[] { "Id,Name,Price" }.Concat(chunk.Select(static r => $"{r.Id},{r.Name},{r.Price}")), TestContext.Current.CancellationToken);
            smallFiles.Add(file);
        }

        // Approach 1: CsvHelper ParallelEnumerate (within-file parallelism)
        var sw1 = Stopwatch.StartNew();
        await using (var stream = File.OpenRead(largeFile))
        using (var reader = new StreamReader(stream))
        await using (var csv = CsvReader.Create(reader, new CsvReaderOptions(), leaveOpen: false))
        {
            var results = csv.ParallelEnumerate<Product>(maxDegreeOfParallelism: 4).ToList();
            results.Count.ShouldBe(10000);
        }

        sw1.Stop();

        // Approach 2: Rivulet (across-file parallelism)
        var sw2 = Stopwatch.StartNew();
        var rivuletResults = await smallFiles.ParseCsvParallelAsync<Product>(
            new CsvOperationOptions
            {
                ParallelOptions = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 4 }
            },
            cancellationToken: TestContext.Current.CancellationToken);
        sw2.Stop();
        rivuletResults.Count.ShouldBe(10000);

        // Both approaches work; performance depends on use case
        // CsvHelper: better for single large file
        // Rivulet: better for multiple files, adds retries/progress/error handling per file
    }

    [Fact]
    public async Task RivuletWithCsvHelperParallelism_CombinesBothApproaches()
    {
        // Combined: Rivulet processes 4 files in parallel, each using CsvHelper's within-file parallelism
        var files = new List<string>();
        for (var i = 0; i < 4; i++)
        {
            var file = Path.Join(TestDirectory, $"file_{i}.csv");
            var records = Enumerable.Range(i * 1000, 1000).Select(static j => new Product { Id = j, Name = $"Product {j}", Price = j * 10m });
            await File.WriteAllLinesAsync(file, new[] { "Id,Name,Price" }.Concat(records.Select(static r => $"{r.Id},{r.Name},{r.Price}")), TestContext.Current.CancellationToken);
            files.Add(file);
        }

        // Rivulet: 4 files in parallel (across-file), each file using 2 threads (within-file)
        var results = await files.ParseCsvParallelAsync<Product>(
            new CsvOperationOptions
            {
                ParallelOptions = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 4 }, // 4 files at once
                FileConfiguration = new CsvFileConfiguration { CsvHelperParallelism = 2 } // 2 threads per file
            },
            cancellationToken: TestContext.Current.CancellationToken);

        results.Count.ShouldBe(4000);
    }

    [Fact]
    public async Task RivuletWithCsvHelperParallelism_Write_CombinesBothApproaches()
    {
        var writes = new List<RivuletCsvWriteFile<Product>>();
        for (var i = 0; i < 4; i++)
        {
            var file = Path.Join(TestDirectory, $"output_{i}.csv");
            var records = Enumerable.Range(i * 1000, 1000).Select(static j => new Product { Id = j, Name = $"Product {j}", Price = j * 10m }).ToList();
            writes.Add(new RivuletCsvWriteFile<Product>(file, records, null));
        }

        // Rivulet: 4 files in parallel, each file using 2 threads for formatting
        await writes.WriteCsvParallelAsync(
            new CsvOperationOptions
            {
                ParallelOptions = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 4 },
                FileConfiguration = new CsvFileConfiguration { CsvHelperParallelism = 2 }
            },
            cancellationToken: TestContext.Current.CancellationToken);

        foreach (var write in writes)
        {
            var lines = await File.ReadAllLinesAsync(write.Path, TestContext.Current.CancellationToken);
            lines.Length.ShouldBe(1001); // header + 1000 records
        }
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
    }
}
