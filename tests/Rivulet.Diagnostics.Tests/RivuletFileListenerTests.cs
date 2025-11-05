using FluentAssertions;
using Rivulet.Core;

namespace Rivulet.Diagnostics.Tests;

public class RivuletFileListenerTests : IDisposable
{
    private readonly string _testFilePath = Path.Join(Path.GetTempPath(), $"rivulet-test-{Guid.NewGuid()}.log");

    [Fact]
    public async Task FileListener_ShouldWriteMetricsToFile_WhenOperationsRun()
    {
        using (new RivuletFileListener(_testFilePath))
        {
            await Enumerable.Range(1, 10)
                .ToAsyncEnumerable()
                .SelectParallelStreamAsync(async (x, ct) =>
                {
                    await Task.Delay(10, ct);
                    return x * 2;
                }, new ParallelOptionsRivulet
                {
                    MaxDegreeOfParallelism = 2
                })
                .ToListAsync();

            // Increased delay for CI/CD environments where EventCounters fire slower
            await Task.Delay(1800);
        } // Dispose listener to flush and close file

        // Wait a moment for file handle to be fully released
        // Increased for CI/CD environments
        await Task.Delay(500);

        File.Exists(_testFilePath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(_testFilePath);
        content.Should().Contain("Items Started");
        content.Should().Contain("Items Completed");
    }

    [Fact]
    public async Task FileListener_ShouldRotateFile_WhenMaxSizeExceeded()
    {
        const long maxSize = 100;
        using var listener = new RivuletFileListener(_testFilePath, maxSize);

        // Generate enough operations to trigger file rotation
        // Use fewer iterations with more items each to reduce total time
        for (var i = 0; i < 20; i++)
        {
            await Enumerable.Range(1, 10)
                .ToAsyncEnumerable()
                .SelectParallelStreamAsync(async (x, ct) =>
                {
                    await Task.Delay(1, ct);
                    return x;
                }, new ParallelOptionsRivulet
                {
                    MaxDegreeOfParallelism = 4
                })
                .ToListAsync();

            // Wait for EventCounters to fire and write to file
            await Task.Delay(100);
        }

        // Wait for final flush and rotation to complete
        await Task.Delay(1200);

        var directory = Path.GetDirectoryName(_testFilePath) ?? string.Empty;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(_testFilePath);
        var rotatedFiles = Directory.GetFiles(directory, $"{fileNameWithoutExtension}-*");

        rotatedFiles.Should().NotBeEmpty();
    }

    [Fact]
    public void FileListener_ShouldThrow_WhenFilePathIsNull()
    {
        var act = () => new RivuletFileListener(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("filePath");
    }

    [Fact]
    public void FileListener_ShouldCreateDirectory_WhenNotExists()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"rivulet-test-dir-{Guid.NewGuid()}");
        var filePath = Path.Combine(directory, "test.log");

        using var listener = new RivuletFileListener(filePath);

        Directory.Exists(directory).Should().BeTrue();

        TestCleanupHelper.RetryDeleteDirectory(directory);
    }

    public void Dispose()
    {
        TestCleanupHelper.RetryDeleteFile(_testFilePath);

        var directory = Path.GetDirectoryName(_testFilePath) ?? string.Empty;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(_testFilePath);
        var rotatedFiles = Directory.GetFiles(directory, $"{fileNameWithoutExtension}-*");
        
        foreach (var file in rotatedFiles)
        {
            TestCleanupHelper.RetryDeleteFile(file);
        }
    }
}
