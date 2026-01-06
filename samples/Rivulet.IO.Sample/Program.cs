using Rivulet.Core;
using Rivulet.Core.Observability;
using Rivulet.IO;

// ReSharper disable ArgumentsStyleLiteral
// ReSharper disable ArrangeObjectCreationWhenTypeNotEvident

Console.WriteLine("=== Rivulet.IO Sample ===\n");

// Setup sample directories
var sampleDir = Path.Join(Path.GetTempPath(), "RivuletIO.Sample");
var inputDir = Path.Join(sampleDir, "input");
var outputDir = Path.Join(sampleDir, "output");

Directory.CreateDirectory(inputDir);
Directory.CreateDirectory(outputDir);

try
{
    // Sample 1: Write files in parallel
    Console.WriteLine("1. WriteAllTextParallelAsync - Create sample files");
    var files = Enumerable.Range(1, 10)
        .Select(i => (Path.Join(inputDir, $"file{i}.txt"), $"Content for file {i}"))
        .ToList();

    await files.WriteAllTextParallelAsync(
        new FileOperationOptions
        {
            ParallelOptions = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 4 },
            CreateDirectoriesIfNotExist = true
        });

    Console.WriteLine($"✓ Created {files.Count} files\n");

    // Sample 2: Read files in parallel
    Console.WriteLine("2. ReadAllTextParallelAsync - Read files");
    var filePaths = Directory.GetFiles(inputDir, "*.txt");
    var contents = await filePaths.ReadAllTextParallelAsync(
        new FileOperationOptions
        {
            ParallelOptions = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 8 }
        });

    Console.WriteLine($"✓ Read {contents.Count} files\n");

    // Sample 3: Transform files in parallel
    Console.WriteLine("3. TransformFilesParallelAsync - Transform and save");
    var transformations = filePaths.Select(f =>
            (f, Path.Join(outputDir, Path.GetFileName(f))))
        .ToList();

    await transformations.TransformFilesParallelAsync(static async (_, content) =>
        {
            await Task.Delay(10); // Simulate processing
            return content.ToUpperInvariant();
        },
        new FileOperationOptions
        {
            OverwriteExisting = true,
            ParallelOptions = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 4 }
        });

    Console.WriteLine($"✓ Transformed {transformations.Count} files\n");

    // Sample 4: Process directory files
    Console.WriteLine("4. ProcessDirectoryFilesParallelAsync - Process all files in directory");
    var results = await DirectoryParallelExtensions.ProcessDirectoryFilesParallelAsync(
        inputDir,
        "*.txt",
        static async (filePath, ct) =>
        {
            var content = await File.ReadAllTextAsync(filePath, ct);
            return (filePath, length: content.Length);
        },
        SearchOption.TopDirectoryOnly,
        new FileOperationOptions
        {
            ParallelOptions = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 6 }
        });

    Console.WriteLine($"✓ Processed {results.Count} files\n");

    // Sample 5: Copy files in parallel
    Console.WriteLine("5. CopyFilesParallelAsync - Copy to backup");
    var backupDir = Path.Join(sampleDir, "backup");
    var copyPairs = filePaths.Select(f =>
            (f, Path.Join(backupDir, Path.GetFileName(f))))
        .ToList();

    await copyPairs.CopyFilesParallelAsync(
        new FileOperationOptions
        {
            CreateDirectoriesIfNotExist = true,
            ParallelOptions = new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 4,
                Progress = new ProgressOptions
                {
                    ReportInterval = TimeSpan.FromMilliseconds(100),
                    OnProgress = static progress =>
                    {
                        Console.WriteLine($"  Copied: {progress.ItemsCompleted}/{progress.TotalItems}");
                        return ValueTask.CompletedTask;
                    }
                }
            }
        });

    Console.WriteLine($"✓ Copied {copyPairs.Count} files\n");

    // Sample 6: FileInfo extensions
    Console.WriteLine("6. FileInfo extensions - Idiomatic .NET API");
    var fileInfo = new FileInfo(Path.Join(inputDir, "file1.txt"));
    var fileContent = await fileInfo.ReadAllTextAsync();
    Console.WriteLine($"✓ Read via FileInfo: {fileContent}\n");

    // Sample 7: DirectoryInfo extensions
    Console.WriteLine("7. DirectoryInfo extensions - Read all files");
    var dirInfo = new DirectoryInfo(inputDir);
    var dirContents = await dirInfo.ReadAllFilesParallelAsync("*.txt");
    Console.WriteLine($"✓ Read {dirContents.Count} files via DirectoryInfo\n");

    Console.WriteLine("=== Sample Complete ===");
}
finally
{
    // Cleanup
    if (Directory.Exists(sampleDir)) Directory.Delete(sampleDir, recursive: true);
}
