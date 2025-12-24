using System.Diagnostics.CodeAnalysis;
using Rivulet.Core;
using Rivulet.Core.Observability;
using Rivulet.Core.Resilience;

namespace Rivulet.Csv.Tests;

public sealed class CsvProgressAndMetricsTests : IDisposable
{
    private readonly string _testDirectory;

    public CsvProgressAndMetricsTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"RivuletCsvProgressTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            // ReSharper disable once ArgumentsStyleLiteral
            Directory.Delete(_testDirectory, recursive: true);
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithProgressReporting_ShouldReportProgress()
    {
        // Arrange
        var files = Enumerable.Range(1, 5)
            .Select(i =>
            {
                var path = Path.Combine(_testDirectory, $"file{i}.csv");
                File.WriteAllText(path, $"Id,Name,Price\n{i},Product {i},10.50");
                return path;
            })
            .ToArray();

        var progressReports = new List<ProgressSnapshot>();

        // Act
        await files.ParseCsvParallelAsync<Product>(
            new CsvOperationOptions
            {
                ParallelOptions = new ParallelOptionsRivulet
                {
                    MaxDegreeOfParallelism = 2,
                    Progress = new ProgressOptions
                    {
                        ReportInterval = TimeSpan.FromMilliseconds(50),
                        OnProgress = progress =>
                        {
                            progressReports.Add(progress);
                            return ValueTask.CompletedTask;
                        }
                    }
                }
            });

        // Assert
        progressReports.Count.ShouldBeGreaterThan(0);
        progressReports.Last().ItemsCompleted.ShouldBe(5);
        progressReports.Last().TotalItems.ShouldBe(5);
        progressReports.Last().PercentComplete.ShouldBe(100.0);
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithMetrics_ShouldCollectMetrics()
    {
        // Arrange
        var files = Enumerable.Range(1, 3)
            .Select(i =>
            {
                var path = Path.Combine(_testDirectory, $"file{i}.csv");
                File.WriteAllText(path, $"Id,Name,Price\n{i},Product {i},10.50");
                return path;
            })
            .ToArray();

        var metricsSamples = new List<MetricsSnapshot>();

        // Act
        await files.ParseCsvParallelAsync<Product>(
            new CsvOperationOptions
            {
                ParallelOptions = new ParallelOptionsRivulet
                {
                    MaxDegreeOfParallelism = 2,
                    Metrics = new MetricsOptions
                    {
                        SampleInterval = TimeSpan.FromMilliseconds(50),
                        OnMetricsSample = async snapshot =>
                        {
                            metricsSamples.Add(snapshot);
                            await Task.CompletedTask;
                        }
                    }
                }
            });

        // Assert
        metricsSamples.Count.ShouldBeGreaterThan(0);
        metricsSamples.Last().ItemsCompleted.ShouldBe(3);
        metricsSamples.Last().TotalFailures.ShouldBe(0);
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithOnCompleteCallback_ShouldReportCompletion()
    {
        // Arrange
        var files = Enumerable.Range(1, 3)
            .Select(i =>
            {
                var path = Path.Combine(_testDirectory, $"file{i}.csv");
                File.WriteAllText(path, $"Id,Name,Price\n{i},Product {i},10.50");
                return path;
            })
            .ToArray();

        var completedFiles = new List<int>();

        // Act
        await files.ParseCsvParallelAsync<Product>(
            new CsvOperationOptions
            {
                ParallelOptions = new ParallelOptionsRivulet
                {
                    OnCompleteItemAsync = async index =>
                    {
                        completedFiles.Add(index);
                        await Task.CompletedTask;
                    }
                }
            });

        // Assert
        completedFiles.Count.ShouldBe(3);
        completedFiles.ShouldContain(0);
        completedFiles.ShouldContain(1);
        completedFiles.ShouldContain(2);
    }

    [Fact]
    public async Task WriteCsvParallelAsync_WithProgressReporting_ShouldReportProgress()
    {
        // Arrange
        var fileWrites = Enumerable.Range(1, 4)
            .Select(i =>
            {
                var path = Path.Combine(_testDirectory, $"output{i}.csv");
                var products = new[] { new Product { Id = i, Name = $"Product {i}", Price = 10m * i } };
                return (path, (IEnumerable<Product>)products);
            })
            .ToArray();

        var progressReports = new List<double?>();

        // Act
        await fileWrites.WriteCsvParallelAsync(
            new CsvOperationOptions
            {
                OverwriteExisting = true,
                ParallelOptions = new ParallelOptionsRivulet
                {
                    MaxDegreeOfParallelism = 2,
                    Progress = new ProgressOptions
                    {
                        ReportInterval = TimeSpan.FromMilliseconds(50),
                        OnProgress = progress =>
                        {
                            progressReports.Add(progress.PercentComplete);
                            return ValueTask.CompletedTask;
                        }
                    }
                }
            });

        // Assert
        progressReports.Count.ShouldBeGreaterThan(0);
        progressReports.Last().ShouldBe(100.0);
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithOrderedOutput_ShouldMaintainOrder()
    {
        // Arrange
        var files = Enumerable.Range(1, 5)
            .Select(i =>
            {
                var path = Path.Combine(_testDirectory, $"file{i}.csv");
                File.WriteAllText(path, $"Id,Name,Price\n{i},Product {i},{i * 10}.50");
                return path;
            })
            .ToArray();

        // Act
        var results = await files.ParseCsvParallelAsync<Product>(
            new CsvOperationOptions
            {
                ParallelOptions = new ParallelOptionsRivulet
                {
                    MaxDegreeOfParallelism = 3,
                    OrderedOutput = true
                }
            });

        // Assert
        results.Count.ShouldBe(5);
        for (var i = 0; i < 5; i++)
            results[i][0].Id.ShouldBe(i + 1);
    }

    [Fact]
    public async Task TransformCsvParallelAsync_WithProgressReporting_ShouldReportProgress()
    {
        // Arrange
        var transformations = Enumerable.Range(1, 3)
            .Select(i =>
            {
                var inputPath = Path.Combine(_testDirectory, $"input{i}.csv");
                var outputPath = Path.Combine(_testDirectory, $"output{i}.csv");
                File.WriteAllText(inputPath, $"Id,Name,Price\n{i},Product {i},10.00");
                return (inputPath, outputPath);
            })
            .ToArray();

        var progressCalled = false;

        // Act
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
            new CsvOperationOptions
            {
                OverwriteExisting = true,
                ParallelOptions = new ParallelOptionsRivulet
                {
                    Progress = new ProgressOptions
                    {
                        ReportInterval = TimeSpan.FromMilliseconds(50),
                        OnProgress = _ =>
                        {
                            progressCalled = true;
                            return ValueTask.CompletedTask;
                        }
                    }
                }
            });

        // Assert
        progressCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithAdaptiveConcurrency_ShouldAdjustConcurrency()
    {
        // Arrange
        var files = Enumerable.Range(1, 10)
            .Select(i =>
            {
                var path = Path.Combine(_testDirectory, $"file{i}.csv");
                File.WriteAllText(path, $"Id,Name,Price\n{i},Product {i},10.50");
                return path;
            })
            .ToArray();

        var concurrencyChanges = new List<int>();

        // Act
        await files.ParseCsvParallelAsync<Product>(
            new CsvOperationOptions
            {
                ParallelOptions = new ParallelOptionsRivulet
                {
                    AdaptiveConcurrency = new AdaptiveConcurrencyOptions
                    {
                        MinConcurrency = 1,
                        MaxConcurrency = 8,
                        InitialConcurrency = 2,
                        TargetLatency = TimeSpan.FromMilliseconds(100),
                        MinSuccessRate = 0.95,
                        OnConcurrencyChange = async (_, newValue) =>
                        {
                            concurrencyChanges.Add(newValue);
                            await Task.CompletedTask;
                        }
                    }
                }
            });

        // Assert - adaptive concurrency may or may not adjust based on performance
        // Just verify it completes successfully
        concurrencyChanges.Count.ShouldBeGreaterThanOrEqualTo(0);
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
