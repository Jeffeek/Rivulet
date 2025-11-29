using Rivulet.Core;

namespace Rivulet.Diagnostics.Tests;

public class RivuletFileListenerTests : IDisposable
{
    private readonly string _testFilePath = Path.Join(Path.GetTempPath(), $"rivulet-test-{Guid.NewGuid()}.log");

    [Fact]
    public async Task FileListener_ShouldWriteMetricsToFile_WhenOperationsRun()
    {
        await using (new RivuletFileListener(_testFilePath))
        {
            // Use longer operation (200ms per item) to ensure EventCounters poll DURING execution
            // EventCounters have ~1 second polling interval, so operation needs to run for 1-2+ seconds
            // 10 items * 200ms / 2 parallelism = 1000ms (1 second) of operation time
            await Enumerable.Range(1, 10)
                .ToAsyncEnumerable()
                .SelectParallelStreamAsync(async (x, ct) =>
                {
                    await Task.Delay(200, ct);
                    return x * 2;
                }, new()
                {
                    MaxDegreeOfParallelism = 2
                })
                .ToListAsync();

            // Wait for EventCounters to poll and write metrics after operation completes
            // Polling interval is ~1 second, wait 2 seconds to ensure at least 2 polls occur
            await Task.Delay(2000);
        } // Dispose listener to flush and close file

        // Wait for file handle to be fully released
        await Task.Delay(800);

        File.Exists(_testFilePath).ShouldBeTrue();
        var content = await File.ReadAllTextAsync(_testFilePath);
        content.ShouldContain("Items Started");
        content.ShouldContain("Items Completed");
    }

    [Fact]
    public async Task FileListener_ShouldRotateFile_WhenMaxSizeExceeded()
    {
        const long maxSize = 100;
        await using var listener = new RivuletFileListener(_testFilePath, maxSize);

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
                }, new()
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

        rotatedFiles.ShouldNotBeEmpty();
    }

    [Fact]
    public void FileListener_ShouldThrow_WhenFilePathIsNull()
    {
        var act = () => new RivuletFileListener(null!);
        act.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe("filePath");
    }

    [Fact]
    public void FileListener_ShouldCreateDirectory_WhenNotExists()
    {
        var directory = Path.Join(Path.GetTempPath(), $"rivulet-test-dir-{Guid.NewGuid()}");
        var filePath = Path.Join(directory, "test.log");

        using var listener = new RivuletFileListener(filePath);

        Directory.Exists(directory).ShouldBeTrue();

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
