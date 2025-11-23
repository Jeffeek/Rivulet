# Rivulet.IO

**Parallel file and directory operations with bounded concurrency, resilience, and streaming support for efficient I/O processing.**

Built on top of Rivulet.Core, this package provides file system-aware parallel operators that enable safe and efficient parallel processing of files and directories with automatic error handling, progress tracking, and configurable file operations.

## Installation

```bash
dotnet add package Rivulet.IO
```

Requires `Rivulet.Core` (automatically included).

## Quick Start

### Parallel File Reading

Read multiple files in parallel with bounded concurrency:

```csharp
using Rivulet.IO;

var files = new[]
{
    "data/file1.txt",
    "data/file2.txt",
    "data/file3.txt"
};

var contents = await files.ReadAllTextParallelAsync(
    new FileOperationOptions
    {
        ParallelOptions = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4
        }
    });

foreach (var content in contents)
{
    Console.WriteLine(content);
}
```

### Parallel File Writing

Write to multiple files in parallel:

```csharp
var fileWrites = new[]
{
    ("output/result1.txt", "Content for file 1"),
    ("output/result2.txt", "Content for file 2"),
    ("output/result3.txt", "Content for file 3")
};

await fileWrites.WriteAllTextParallelAsync(
    new FileOperationOptions
    {
        CreateDirectoriesIfNotExist = true,
        OverwriteExisting = true
    });
```

### Process Directory Files

Process all files in a directory in parallel:

```csharp
var results = await Directory.GetFiles("data", "*.csv")
    .ProcessFilesParallelAsync(
        async (filePath, ct) =>
        {
            var content = await File.ReadAllTextAsync(filePath, ct);
            return ParseCsvData(content);
        },
        new FileOperationOptions
        {
            ParallelOptions = new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 8,
                MaxRetries = 3
            }
        });
```

## Core Features

### Idiomatic .NET API with FileInfo/DirectoryInfo

Rivulet.IO provides idiomatic .NET extension methods for working with `FileInfo` and `DirectoryInfo` objects, making it easy to integrate with existing code:

```csharp
// Work with FileInfo objects
var file = new FileInfo("data.txt");
var content = await file.ReadAllTextAsync();
await file.WriteAllTextAsync("Updated content");
await file.CopyToAsync("backup/data.txt");
await file.DeleteAsync();

// Work with DirectoryInfo objects
var directory = new DirectoryInfo("logs");
var logContents = await directory.ReadAllFilesParallelAsync("*.log");

// Process multiple FileInfo objects in parallel
var files = new[] { new FileInfo("file1.txt"), new FileInfo("file2.txt") };
var contents = await files.ReadAllTextParallelAsync();

// Transform files in a directory
await directory.TransformFilesParallelAsync(
    "processed",
    async (sourcePath, content) => content.ToUpper(),
    "*.txt");
```

**Key Benefits:**
- Seamlessly integrates with existing `System.IO` types
- Type-safe with compile-time checking
- Follows .NET naming conventions
- Works with all existing Rivulet.IO options

### File Operations

#### Read Operations

```csharp
// Read text files
var textContents = await files.ReadAllTextParallelAsync();

// Read binary files
var byteContents = await files.ReadAllBytesParallelAsync();

// Read files as lines
var linesPerFile = await files.ReadAllLinesParallelAsync();
```

#### Write Operations

```csharp
// Write text files
var writes = new[]
{
    ("file1.txt", "Text content"),
    ("file2.txt", "More content")
};
await writes.WriteAllTextParallelAsync();

// Write binary files
var binaryWrites = new[]
{
    ("file1.bin", new byte[] { 1, 2, 3 }),
    ("file2.bin", new byte[] { 4, 5, 6 })
};
await binaryWrites.WriteAllBytesParallelAsync();
```

### File Transformations

Transform files in parallel by applying a transformation function:

```csharp
var transformations = new[]
{
    ("input/data1.txt", "output/processed1.txt"),
    ("input/data2.txt", "output/processed2.txt")
};

await transformations.TransformFilesParallelAsync(
    async (sourcePath, content) =>
    {
        // Transform the content
        return content.ToUpper();
    },
    new FileOperationOptions { OverwriteExisting = true });
```

### File Copy and Delete

```csharp
// Copy files in parallel
var copies = new[]
{
    ("source/file1.txt", "backup/file1.txt"),
    ("source/file2.txt", "backup/file2.txt")
};
await copies.CopyFilesParallelAsync();

// Delete files in parallel
var filesToDelete = new[] { "temp/file1.tmp", "temp/file2.tmp" };
await filesToDelete.DeleteFilesParallelAsync();
```

## Directory Operations

### Process Directory Files

Process all files matching a pattern in a directory:

```csharp
var results = await DirectoryParallelExtensions.ProcessDirectoryFilesParallelAsync(
    "data",
    "*.json",
    async (filePath, ct) =>
    {
        var json = await File.ReadAllTextAsync(filePath, ct);
        return JsonSerializer.Deserialize<DataModel>(json);
    },
    SearchOption.AllDirectories,
    new FileOperationOptions
    {
        ParallelOptions = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 10
        }
    });
```

### Read Directory Files

Read all files in a directory into a dictionary:

```csharp
var fileContents = await DirectoryParallelExtensions.ReadDirectoryFilesParallelAsync(
    "logs",
    "*.log",
    SearchOption.TopDirectoryOnly);

foreach (var (filePath, content) in fileContents)
{
    Console.WriteLine($"{filePath}: {content.Length} characters");
}
```

### Transform Directory Files

Transform all files in a directory:

```csharp
await DirectoryParallelExtensions.TransformDirectoryFilesParallelAsync(
    sourceDirectory: "raw_data",
    destinationDirectory: "processed_data",
    searchPattern: "*.txt",
    transformFunc: async (sourcePath, content) =>
    {
        // Apply transformation
        return content.Replace("\r\n", "\n");
    },
    SearchOption.AllDirectories,
    new FileOperationOptions { OverwriteExisting = true });
```

### Copy and Delete Directory Files

```csharp
// Copy all files from one directory to another
await DirectoryParallelExtensions.CopyDirectoryFilesParallelAsync(
    "source_directory",
    "backup_directory",
    "*.dat",
    SearchOption.AllDirectories,
    new FileOperationOptions { OverwriteExisting = true });

// Delete all files matching a pattern
await DirectoryParallelExtensions.DeleteDirectoryFilesParallelAsync(
    "temp",
    "*.tmp",
    SearchOption.AllDirectories);
```

### Process Multiple Directories

Process files from multiple directories in parallel:

```csharp
var directories = new[] { "data/2023", "data/2024", "data/2025" };

var allResults = await directories.ProcessMultipleDirectoriesParallelAsync(
    "*.csv",
    async (filePath, ct) =>
    {
        return await ProcessCsvFileAsync(filePath, ct);
    },
    SearchOption.TopDirectoryOnly);
```

## Configuration Options

### FileOperationOptions

Configure how file operations are performed:

```csharp
var options = new FileOperationOptions
{
    // Buffer size for file I/O operations (default: 81920 bytes / 80 KB)
    BufferSize = 81920,

    // Text encoding (default: UTF-8)
    Encoding = System.Text.Encoding.UTF8,

    // Auto-create directories if they don't exist (default: true)
    CreateDirectoriesIfNotExist = true,

    // Overwrite existing files (default: false)
    OverwriteExisting = true,

    // File share mode for reading (default: FileShare.Read)
    ReadFileShare = FileShare.Read,

    // File share mode for writing (default: FileShare.None)
    WriteFileShare = FileShare.None,

    // Parallel execution options
    ParallelOptions = new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 8,
        MaxRetries = 3,
        IsTransient = ex => ex is IOException
    }
};
```

### Lifecycle Callbacks

Track file processing with callbacks:

```csharp
var options = new FileOperationOptions
{
    OnFileStartAsync = async (filePath) =>
    {
        Console.WriteLine($"Starting: {filePath}");
    },
    OnFileCompleteAsync = async (filePath, bytesProcessed) =>
    {
        Console.WriteLine($"Completed: {filePath} ({bytesProcessed:N0} bytes)");
    },
    OnFileErrorAsync = async (filePath, exception) =>
    {
        Console.WriteLine($"Error in {filePath}: {exception.Message}");
    }
};
```

## Advanced Scenarios

### ETL Pipeline

Process CSV files, transform them, and write to a database:

```csharp
var csvFiles = Directory.GetFiles("input", "*.csv");

await csvFiles.ProcessFilesParallelAsync(
    async (filePath, ct) =>
    {
        // Read CSV
        var records = await ReadCsvAsync(filePath, ct);

        // Transform
        var transformed = records.Select(r => Transform(r)).ToList();

        // Write to database
        await SaveToDatabaseAsync(transformed, ct);

        return transformed.Count;
    },
    new FileOperationOptions
    {
        ParallelOptions = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            MaxRetries = 2,
            Progress = new ProgressOptions
            {
                ReportInterval = TimeSpan.FromSeconds(5),
                OnProgress = progress =>
                {
                    Console.WriteLine($"Processed {progress.ItemsCompleted} files");
                    return ValueTask.CompletedTask;
                }
            }
        }
    });
```

### Log File Processing

Process log files with error handling:

```csharp
var results = await DirectoryParallelExtensions.ProcessDirectoryFilesParallelAsync(
    "logs",
    "*.log",
    async (filePath, ct) =>
    {
        try
        {
            var lines = await File.ReadAllLinesAsync(filePath, ct);
            return lines.Count(line => line.Contains("ERROR"));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to process {filePath}: {ex.Message}");
            return 0;
        }
    },
    SearchOption.AllDirectories,
    new FileOperationOptions
    {
        ParallelOptions = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 16,
            ErrorMode = ErrorMode.BestEffort
        }
    });

var totalErrors = results.Sum();
Console.WriteLine($"Total errors found: {totalErrors}");
```

### Batch File Conversion

Convert image files in parallel with progress tracking:

```csharp
var imageFiles = Directory.GetFiles("images", "*.png", SearchOption.AllDirectories);

await imageFiles.ProcessFilesParallelAsync(
    async (filePath, ct) =>
    {
        var destPath = Path.ChangeExtension(filePath, ".jpg");

        // Simulate image conversion
        var bytes = await File.ReadAllBytesAsync(filePath, ct);
        var convertedBytes = await ConvertToJpegAsync(bytes, ct);
        await File.WriteAllBytesAsync(destPath, convertedBytes, ct);

        return destPath;
    },
    new FileOperationOptions
    {
        ParallelOptions = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            Progress = new ProgressOptions
            {
                ReportInterval = TimeSpan.FromSeconds(2),
                OnProgress = progress =>
                {
                    var percent = (progress.ItemsCompleted * 100.0) / progress.TotalItems;
                    Console.WriteLine($"Converting: {percent:F1}% ({progress.ItemsCompleted}/{progress.TotalItems})");
                    return ValueTask.CompletedTask;
                }
            }
        }
    });
```

## Error Handling

### Retry on Transient Errors

```csharp
var options = new FileOperationOptions
{
    ParallelOptions = new ParallelOptionsRivulet
    {
        MaxRetries = 3,
        BaseDelay = TimeSpan.FromMilliseconds(100),
        IsTransient = ex => ex is IOException
    }
};

var results = await files.ReadAllTextParallelAsync(options);
```

### Collect and Continue on Errors

```csharp
try
{
    var results = await files.ReadAllTextParallelAsync(
        new FileOperationOptions
        {
            ParallelOptions = new ParallelOptionsRivulet
            {
                ErrorMode = ErrorMode.CollectAndContinue
            }
        });
}
catch (AggregateException ex)
{
    foreach (var inner in ex.InnerExceptions)
    {
        Console.WriteLine($"Error: {inner.Message}");
    }
}
```

## Performance Tips

1. **Optimal Parallelism**: For I/O-bound file operations, use higher parallelism (16-32). For CPU-intensive transformations, use lower parallelism (4-8).

2. **Buffer Size**: Increase `BufferSize` for large files (512 KB - 1 MB) to reduce system calls.

3. **Order Output**: Use `OrderedOutput = true` only when result order matters, as it adds overhead.

4. **File Share Mode**: Use `FileShare.Read` for reading multiple times, `FileShare.None` for exclusive access.

5. **Progress Tracking**: Increase `ReportInterval` for better performance when processing many small files.

## Integration with Rivulet.Core

Rivulet.IO inherits all features from Rivulet.Core:

- **Retry Policies**: Automatic retries with backoff strategies
- **Circuit Breaker**: Fail-fast when file system is unresponsive
- **Rate Limiting**: Control file operation throughput
- **Adaptive Concurrency**: Auto-adjust parallelism based on performance
- **Progress Tracking**: Real-time metrics and ETA
- **Metrics Export**: EventCounters integration

```csharp
var options = new FileOperationOptions
{
    ParallelOptions = new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 8,
        MaxRetries = 3,
        CircuitBreaker = new CircuitBreakerOptions
        {
            FailureThreshold = 5,
            OpenTimeout = TimeSpan.FromSeconds(30)
        },
        RateLimit = new RateLimitOptions
        {
            TokensPerSecond = 100,
            BurstCapacity = 100
        }
    }
};
```

## Architecture

### Efficient Code Design

Rivulet.IO uses an internal `FileOperationHelper` utility to eliminate code duplication and ensure consistent behavior across all file operations:

- **Centralized Lifecycle Management**: All file operations use a single helper for start/complete/error callbacks
- **Consistent Error Handling**: Unified exception handling and callback invocation
- **Validation Helpers**: Shared validation logic for directory creation and overwrite protection
- **~60% Less Duplication**: Significantly reduced code duplication across all file operation methods

This design ensures that:
- All file operations behave consistently
- Changes to lifecycle management apply everywhere automatically
- Code is easier to maintain and test
- Performance optimizations benefit all operations

### Extension Method Architecture

The library provides three layers of extension methods:

1. **String Path Extensions** (`FileParallelExtensions`, `DirectoryParallelExtensions`):
   - Work with file/directory paths as strings
   - Direct access to all operations

2. **FileInfo/DirectoryInfo Extensions** (`FileInfoExtensions`, `DirectoryInfoExtensions`):
   - Idiomatic .NET API
   - Work with `System.IO` types
   - Delegate to string-based implementations

3. **Internal Helpers** (`FileOperationHelper`):
   - Shared logic for all operations
   - Lifecycle callback management
   - Validation and error handling

This layered architecture provides flexibility while maintaining a single source of truth for core functionality.

## Requirements

- .NET 8.0 or .NET 9.0
- Rivulet.Core (included)

## License

MIT License - see [LICENSE](../../LICENSE.txt) for details.

## Links

- [GitHub Repository](https://github.com/Jeffeek/Rivulet)
- [Documentation](https://github.com/Jeffeek/Rivulet/tree/master/docs)
- [NuGet Package](https://www.nuget.org/packages/Rivulet.IO/)
