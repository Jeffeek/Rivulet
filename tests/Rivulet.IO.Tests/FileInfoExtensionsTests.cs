using Rivulet.Base.Tests;

namespace Rivulet.IO.Tests;

public class FileInfoExtensionsTests : TempDirectoryFixture
{
    [Fact]
    public async Task ReadAllTextParallelAsync_WithMultipleFileInfos_ShouldReadCorrectly()
    {
        // Arrange
        var file1 = new FileInfo(Path.Join(TestDirectory, "file1.txt"));
        var file2 = new FileInfo(Path.Join(TestDirectory, "file2.txt"));
        var file3 = new FileInfo(Path.Join(TestDirectory, "file3.txt"));

        await File.WriteAllTextAsync(file1.FullName, "Content 1");
        await File.WriteAllTextAsync(file2.FullName, "Content 2");
        await File.WriteAllTextAsync(file3.FullName, "Content 3");

        var files = new[] { file1, file2, file3 };

        // Act
        var results = await files.ReadAllTextParallelAsync(
            new()
            {
                ParallelOptions = new() { OrderedOutput = true }
            });

        // Assert
        results.Should().HaveCount(3);
        results[0].Should().Be("Content 1");
        results[1].Should().Be("Content 2");
        results[2].Should().Be("Content 3");
    }

    [Fact]
    public async Task ReadAllBytesParallelAsync_WithMultipleFileInfos_ShouldReadCorrectly()
    {
        // Arrange
        var file1 = new FileInfo(Path.Join(TestDirectory, "bytes1.bin"));
        var file2 = new FileInfo(Path.Join(TestDirectory, "bytes2.bin"));

        var data1 = new byte[] { 1, 2, 3 };
        var data2 = new byte[] { 4, 5, 6 };

        await File.WriteAllBytesAsync(file1.FullName, data1);
        await File.WriteAllBytesAsync(file2.FullName, data2);

        var files = new[] { file1, file2 };

        // Act
        var results = await files.ReadAllBytesParallelAsync(
            new()
            {
                ParallelOptions = new() { OrderedOutput = true }
            });

        // Assert
        results.Should().HaveCount(2);
        results[0].Should().BeEquivalentTo(data1);
        results[1].Should().BeEquivalentTo(data2);
    }

    [Fact]
    public async Task ReadAllTextAsync_WithSingleFileInfo_ShouldReadCorrectly()
    {
        // Arrange
        var file = new FileInfo(Path.Join(TestDirectory, "single.txt"));
        await File.WriteAllTextAsync(file.FullName, "Single file content");

        // Act
        var result = await file.ReadAllTextAsync();

        // Assert
        result.Should().Be("Single file content");
    }

    [Fact]
    public async Task ReadAllBytesAsync_WithSingleFileInfo_ShouldReadCorrectly()
    {
        // Arrange
        var file = new FileInfo(Path.Join(TestDirectory, "single.bin"));
        var data = new byte[] { 10, 20, 30, 40 };
        await File.WriteAllBytesAsync(file.FullName, data);

        // Act
        var result = await file.ReadAllBytesAsync();

        // Assert
        result.Should().BeEquivalentTo(data);
    }

    [Fact]
    public async Task WriteAllTextAsync_ShouldWriteCorrectly()
    {
        // Arrange
        var file = new FileInfo(Path.Join(TestDirectory, "write.txt"));
        const string content = "Written content";

        // Act
        await file.WriteAllTextAsync(content);

        // Assert
        var readContent = await File.ReadAllTextAsync(file.FullName);
        readContent.Should().Be(content);
    }

    [Fact]
    public async Task WriteAllTextAsync_WithExistingFile_ShouldThrowWhenOverwriteFalse()
    {
        // Arrange
        var file = new FileInfo(Path.Join(TestDirectory, "existing.txt"));
        await File.WriteAllTextAsync(file.FullName, "Original");

        var options = new FileOperationOptions { OverwriteExisting = false };

        // Act
        var act = async () => await file.WriteAllTextAsync("New content", options);

        // Assert
        await act.Should().ThrowAsync<IOException>();
    }

    [Fact]
    public async Task WriteAllBytesAsync_ShouldWriteCorrectly()
    {
        // Arrange
        var file = new FileInfo(Path.Join(TestDirectory, "writebytes.bin"));
        var data = "def"u8.ToArray();

        // Act
        await file.WriteAllBytesAsync(data);

        // Assert
        var readData = await File.ReadAllBytesAsync(file.FullName);
        readData.Should().BeEquivalentTo(data);
    }

    [Fact]
    public async Task CopyToAsync_ShouldCopyFileCorrectly()
    {
        // Arrange
        var sourceFile = new FileInfo(Path.Join(TestDirectory, "source.txt"));
        var destPath = Path.Join(TestDirectory, "dest.txt");

        await File.WriteAllTextAsync(sourceFile.FullName, "Source content");

        // Act
        await sourceFile.CopyToAsync(destPath);

        // Assert
        File.Exists(destPath).Should().BeTrue();
        var destContent = await File.ReadAllTextAsync(destPath);
        destContent.Should().Be("Source content");
    }

    [Fact]
    public async Task CopyToAsync_WithExistingDestination_ShouldThrowWhenOverwriteFalse()
    {
        // Arrange
        var sourceFile = new FileInfo(Path.Join(TestDirectory, "source2.txt"));
        var destPath = Path.Join(TestDirectory, "dest2.txt");

        await File.WriteAllTextAsync(sourceFile.FullName, "Source");
        await File.WriteAllTextAsync(destPath, "Existing");

        var options = new FileOperationOptions { OverwriteExisting = false };

        // Act
        var act = async () => await sourceFile.CopyToAsync(destPath, options);

        // Assert
        await act.Should().ThrowAsync<IOException>();
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteFileCorrectly()
    {
        // Arrange
        var file = new FileInfo(Path.Join(TestDirectory, "todelete.txt"));
        await File.WriteAllTextAsync(file.FullName, "Will be deleted");

        // Act
        await file.DeleteAsync();

        // Assert
        File.Exists(file.FullName).Should().BeFalse();
    }

    [Fact]
    public async Task ReadWriteAsync_WithCustomEncoding_ShouldWorkCorrectly()
    {
        // Arrange
        var file = new FileInfo(Path.Join(TestDirectory, "encoding.txt"));
        const string content = "Special chars: ñ, é, ü";
        var options = new FileOperationOptions { Encoding = System.Text.Encoding.Unicode };

        // Act
        await file.WriteAllTextAsync(content, options);
        var readContent = await file.ReadAllTextAsync(options);

        // Assert
        readContent.Should().Be(content);
    }

    [Fact]
    public async Task WriteAllTextAsync_WithCallbacks_ShouldInvokeCallbacks()
    {
        // Arrange
        var file = new FileInfo(Path.Join(TestDirectory, "callbacks.txt"));
        var startCalled = false;
        var completeCalled = false;
        long bytesProcessed = 0;

        var options = new FileOperationOptions
        {
            OnFileStartAsync = _ =>
            {
                startCalled = true;
                return ValueTask.CompletedTask;
            },
            OnFileCompleteAsync = (_, bytes) =>
            {
                completeCalled = true;
                bytesProcessed = bytes;
                return ValueTask.CompletedTask;
            }
        };

        // Act
        await file.WriteAllTextAsync("Test content", options);

        // Assert
        startCalled.Should().BeTrue();
        completeCalled.Should().BeTrue();
        bytesProcessed.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ReadAllTextAsync_WithError_ShouldInvokeErrorCallback()
    {
        // Arrange
        var file = new FileInfo(Path.Join(TestDirectory, "nonexistent.txt"));
        var errorCalled = false;
        Exception? capturedException = null;

        var options = new FileOperationOptions
        {
            OnFileErrorAsync = (_, ex) =>
            {
                errorCalled = true;
                capturedException = ex;
                return ValueTask.CompletedTask;
            }
        };

        // Act
        var act = async () => await file.ReadAllTextAsync(options);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>();
        errorCalled.Should().BeTrue();
        capturedException.Should().NotBeNull();
    }
}
