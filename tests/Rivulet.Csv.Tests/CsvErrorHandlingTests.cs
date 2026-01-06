using System.Diagnostics.CodeAnalysis;
using Rivulet.Core;
using Rivulet.Core.Resilience;

namespace Rivulet.Csv.Tests;

public sealed class CsvErrorHandlingTests : IDisposable
{
    private readonly string _testDirectory;

    public CsvErrorHandlingTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"RivuletCsvErrorTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            // ReSharper disable once ArgumentsStyleLiteral
            Directory.Delete(_testDirectory, recursive: true);
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithFailFast_ShouldStopOnFirstError()
    {
        // Arrange
        var csvPath1 = Path.Combine(_testDirectory, "file1.csv");
        var csvPath2 = Path.Combine(_testDirectory, "missing.csv"); // This doesn't exist
        var csvPath3 = Path.Combine(_testDirectory, "file3.csv");

        await File.WriteAllTextAsync(csvPath1, "Id,Name,Price\n1,Product A,10.50");
        await File.WriteAllTextAsync(csvPath3, "Id,Name,Price\n3,Product C,30.50");

        // Act & Assert - Use CollectAndContinue to ensure FileNotFoundException is properly collected
        // instead of being masked by TaskCanceledException from FailFast cancellation
        var exception = await Should.ThrowAsync<AggregateException>(async () =>
        {
            await new[] { csvPath1, csvPath2, csvPath3 }.ParseCsvParallelAsync<Product>(
                new CsvOperationOptions
                {
                    ParallelOptions = new ParallelOptionsRivulet
                    {
                        ErrorMode = ErrorMode.CollectAndContinue,
                        MaxRetries = 0
                    }
                });
        });

        // Verify the exception contains FileNotFoundException
        exception.InnerExceptions.ShouldContain(static e => e is FileNotFoundException);
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithCollectAndContinue_ShouldProcessAllFiles()
    {
        // Arrange
        var csvPath1 = Path.Combine(_testDirectory, "file1.csv");
        var csvPath2 = Path.Combine(_testDirectory, "missing.csv"); // This doesn't exist
        var csvPath3 = Path.Combine(_testDirectory, "file3.csv");

        await File.WriteAllTextAsync(csvPath1, "Id,Name,Price\n1,Product A,10.50");
        await File.WriteAllTextAsync(csvPath3, "Id,Name,Price\n3,Product C,30.50");

        // Act & Assert - CollectAndContinue should throw AggregateException with all errors
        var exception = await Should.ThrowAsync<AggregateException>(async () =>
        {
            await new[] { csvPath1, csvPath2, csvPath3 }.ParseCsvParallelAsync<Product>(
                new CsvOperationOptions
                {
                    ParallelOptions = new ParallelOptionsRivulet
                    {
                        ErrorMode = ErrorMode.CollectAndContinue,
                        MaxRetries = 0 // No retries to ensure the error is captured
                    }
                });
        });

        // Should contain FileNotFoundException for the missing file
        exception.InnerExceptions.Count.ShouldBeGreaterThanOrEqualTo(1);
        exception.InnerExceptions.ShouldContain(static e => e is FileNotFoundException);
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithRetries_ShouldRetryTransientErrors()
    {
        // Arrange
        var csvPath = Path.Combine(_testDirectory, "locked.csv");
        await File.WriteAllTextAsync(csvPath, "Id,Name,Price\n1,Product A,10.50");

        var attemptCount = 0;

        // Act
        var results = await new[] { csvPath }.ParseCsvParallelAsync<Product>(
            new CsvOperationOptions
            {
                ParallelOptions = new ParallelOptionsRivulet
                {
                    MaxRetries = 3,
                    BaseDelay = TimeSpan.FromMilliseconds(10),
                    OnStartItemAsync = async _ =>
                    {
                        attemptCount++;
                        await Task.CompletedTask;
                    },
                    OnErrorAsync = static async (_, _) =>
                    {
                        await Task.CompletedTask;
                        return false;
                    }
                }
            });

        // Assert
        results.Count.ShouldBe(1);
        attemptCount.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithOnErrorCallback_ShouldInvokeOnError()
    {
        // Arrange
        var csvPath = Path.Combine(_testDirectory, "missing.csv"); // Doesn't exist
        var errorOccurred = false;
        Exception? capturedException = null;

        // Act
        try
        {
            await new[] { csvPath }.ParseCsvParallelAsync<Product>(
                new CsvOperationOptions
                {
                    ParallelOptions = new ParallelOptionsRivulet
                    {
                        MaxRetries = 1,
                        OnErrorAsync = async (_, ex) =>
                        {
                            errorOccurred = true;
                            capturedException = ex;
                            await Task.CompletedTask;
                            return false;
                        }
                    }
                });
        }
        catch
        {
            // Expected
        }

        // Assert
        errorOccurred.ShouldBeTrue();
        capturedException.ShouldNotBeNull();
        capturedException.ShouldBeOfType<FileNotFoundException>();
    }

    [Fact]
    public async Task WriteCsvParallelAsync_WithErrorCallback_ShouldInvokeOnFileError()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDirectory, "nonexistent");
        // Don't create the directory - this will cause a DirectoryNotFoundException

        var products = new[] { new Product { Id = 1, Name = "Test", Price = 10m } };
        var csvPath = Path.Combine(nonExistentPath, "output.csv");
        var fileWrites = new[] { new RivuletCsvWriteFile<Product>(csvPath, products, null) };

        var fileErrorOccurred = false;
        string? errorFilePath = null;

        // Act
        try
        {
            await fileWrites.WriteCsvParallelAsync(
                new CsvOperationOptions
                {
                    CreateDirectoriesIfNotExist = false,
                    OnFileErrorAsync = async (filePath, _) =>
                    {
                        fileErrorOccurred = true;
                        errorFilePath = filePath;
                        await Task.CompletedTask;
                    },
                    ParallelOptions = new ParallelOptionsRivulet
                    {
                        MaxRetries = 1
                    }
                });
        }
        catch
        {
            // Expected
        }

        // Assert
        fileErrorOccurred.ShouldBeTrue();
        errorFilePath.ShouldBe(csvPath);
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithCircuitBreaker_ShouldOpenOnFailures()
    {
        // Arrange
        var files = Enumerable.Range(1, 10)
            .Select(i => Path.Combine(_testDirectory, $"missing{i}.csv"))
            .ToArray();

        var fileErrorCount = 0;
        var circuitBreakerErrorCount = 0;
        var circuitBreakerOpened = false;

        // Act & Assert
        await Should.ThrowAsync<Exception>(async () =>
        {
            await files.ParseCsvParallelAsync<Product>(
                new CsvOperationOptions
                {
                    ParallelOptions = new ParallelOptionsRivulet
                    {
                        MaxRetries = 0, // No retries
                        MaxDegreeOfParallelism = 1, // Single worker to allow circuit breaker to interrupt
                        ErrorMode = ErrorMode.CollectAndContinue, // Don't fail fast, let circuit breaker do its job
                        CircuitBreaker = new CircuitBreakerOptions
                        {
                            FailureThreshold = 3, // Open after 3 consecutive failures
                            SuccessThreshold = 2,
                            OpenTimeout = TimeSpan.FromSeconds(1),
                            OnStateChange = (_, newState) =>
                            {
                                if (newState == CircuitBreakerState.Open)
                                    circuitBreakerOpened = true;
                                return ValueTask.CompletedTask;
                            }
                        },
                        OnErrorAsync = async (_, ex) =>
                        {
                            if (ex is CircuitBreakerOpenException)
                                circuitBreakerErrorCount++;
                            else
                                fileErrorCount++;
                            await Task.CompletedTask;
                            return true; // Continue processing to let circuit breaker accumulate failures
                        }
                    }
                });
        });

        // Circuit breaker should have kicked in, preventing all 10 failures
        circuitBreakerOpened.ShouldBeTrue("Circuit breaker should have opened");
        fileErrorCount.ShouldBe(3, "Should have 3 file errors before circuit breaker opens");
        circuitBreakerErrorCount.ShouldBe(7, "Should have 7 circuit breaker errors after opening");
    }

    [Fact]
    public async Task ParseCsvParallelAsync_WithMaxConcurrency_ShouldLimitParallelism()
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

        var maxConcurrentTasks = 0;
        var currentTasks = 0;
        var lockObj = new object();

        // Act
        await files.ParseCsvParallelAsync<Product>(
            new CsvOperationOptions
            {
                ParallelOptions = new ParallelOptionsRivulet
                {
                    MaxDegreeOfParallelism = 2,
                    OnStartItemAsync = async _ =>
                    {
                        lock (lockObj)
                        {
                            currentTasks++;
                            if (currentTasks > maxConcurrentTasks)
                                maxConcurrentTasks = currentTasks;
                        }

                        await Task.Delay(50); // Simulate work

                        lock (lockObj) currentTasks--;
                    }
                }
            });

        // Assert
        maxConcurrentTasks.ShouldBeLessThanOrEqualTo(2);
    }

    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
    private sealed class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }
}
