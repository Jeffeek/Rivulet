namespace Rivulet.IO.Tests;

public class FileParallelExtensionsTests : IDisposable
{
    private readonly string _testDirectory;

    public FileParallelExtensionsTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"RivuletIO_Tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ReadAllTextParallelAsync_WithMultipleFiles_ShouldReadAllCorrectly()
    {
        // Arrange
        var files = new[] { "file1.txt", "file2.txt", "file3.txt" };
        var expectedContents = new[] { "Content 1", "Content 2", "Content 3" };

        for (int i = 0; i < files.Length; i++)
        {
            var path = Path.Combine(_testDirectory, files[i]);
            await File.WriteAllTextAsync(path, expectedContents[i]);
        }

        var filePaths = files.Select(f => Path.Combine(_testDirectory, f));

        // Act
        var results = await filePaths.ReadAllTextParallelAsync();

        // Assert
        results.Should().HaveCount(3);
        results.Should().Contain(expectedContents);
    }

    [Fact]
    public async Task ReadAllTextParallelAsync_WithNullFilePaths_ShouldThrow()
    {
        // Act
        var act = async () => await ((IEnumerable<string>)null!).ReadAllTextParallelAsync();

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReadAllBytesParallelAsync_WithMultipleFiles_ShouldReadAllCorrectly()
    {
        // Arrange
        var files = new[] { "byte1.bin", "byte2.bin" };
        var expectedContents = new[] { new byte[] { 1, 2, 3 }, new byte[] { 4, 5, 6 } };

        for (int i = 0; i < files.Length; i++)
        {
            var path = Path.Combine(_testDirectory, files[i]);
            await File.WriteAllBytesAsync(path, expectedContents[i]);
        }

        var filePaths = files.Select(f => Path.Combine(_testDirectory, f)).ToList();

        // Act
        var results = await filePaths.ReadAllBytesParallelAsync(
            new() { ParallelOptions = new() { OrderedOutput = true } });

        // Assert
        results.Should().HaveCount(2);
        results[0].Should().Equal(expectedContents[0]);
        results[1].Should().Equal(expectedContents[1]);
    }

    [Fact]
    public async Task ReadAllLinesParallelAsync_WithMultipleFiles_ShouldReadAllLines()
    {
        // Arrange
        var file1Path = Path.Combine(_testDirectory, "lines1.txt");
        var file2Path = Path.Combine(_testDirectory, "lines2.txt");

        await File.WriteAllLinesAsync(file1Path, new[] { "Line 1", "Line 2" });
        await File.WriteAllLinesAsync(file2Path, new[] { "Line A", "Line B", "Line C" });

        // Act
        var results = await new[] { file1Path, file2Path }.ReadAllLinesParallelAsync(
            new() { ParallelOptions = new() { OrderedOutput = true } });

        // Assert
        results.Should().HaveCount(2);
        results[0].Should().Equal("Line 1", "Line 2");
        results[1].Should().Equal("Line A", "Line B", "Line C");
    }

    [Fact]
    public async Task WriteAllTextParallelAsync_WithMultipleFiles_ShouldWriteAllCorrectly()
    {
        // Arrange
        var writes = new[]
        {
            (Path.Combine(_testDirectory, "write1.txt"), "Content 1"),
            (Path.Combine(_testDirectory, "write2.txt"), "Content 2")
        };

        var options = new FileOperationOptions { OverwriteExisting = true };

        // Act
        var results = await writes.WriteAllTextParallelAsync(options);

        // Assert
        results.Should().HaveCount(2);

        var content1 = await File.ReadAllTextAsync(writes[0].Item1);
        var content2 = await File.ReadAllTextAsync(writes[1].Item1);

        content1.Should().Be("Content 1");
        content2.Should().Be("Content 2");
    }

    [Fact]
    public async Task WriteAllTextParallelAsync_WithExistingFile_ShouldThrowWhenOverwriteFalse()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "existing.txt");
        await File.WriteAllTextAsync(filePath, "Existing");

        var writes = new[] { (filePath, "New Content") };
        var options = new FileOperationOptions { OverwriteExisting = false };

        // Act
        var act = async () => await writes.WriteAllTextParallelAsync(options);

        // Assert
        await act.Should().ThrowAsync<IOException>();
    }

    [Fact]
    public async Task WriteAllBytesParallelAsync_WithMultipleFiles_ShouldWriteAllCorrectly()
    {
        // Arrange
        var writes = new[]
        {
            (Path.Combine(_testDirectory, "bytes1.bin"), new byte[] { 1, 2, 3 }),
            (Path.Combine(_testDirectory, "bytes2.bin"), new byte[] { 4, 5, 6 })
        };

        var options = new FileOperationOptions { OverwriteExisting = true };

        // Act
        var results = await writes.WriteAllBytesParallelAsync(options);

        // Assert
        results.Should().HaveCount(2);

        var bytes1 = await File.ReadAllBytesAsync(writes[0].Item1);
        var bytes2 = await File.ReadAllBytesAsync(writes[1].Item1);

        bytes1.Should().Equal(1, 2, 3);
        bytes2.Should().Equal(4, 5, 6);
    }

    [Fact]
    public async Task TransformFilesParallelAsync_WithTransformFunction_ShouldTransformCorrectly()
    {
        // Arrange
        var sourceFile1 = Path.Combine(_testDirectory, "source1.txt");
        var sourceFile2 = Path.Combine(_testDirectory, "source2.txt");
        var destFile1 = Path.Combine(_testDirectory, "dest1.txt");
        var destFile2 = Path.Combine(_testDirectory, "dest2.txt");

        await File.WriteAllTextAsync(sourceFile1, "hello");
        await File.WriteAllTextAsync(sourceFile2, "world");

        var files = new[]
        {
            (sourceFile1, destFile1),
            (sourceFile2, destFile2)
        };

        // Act
        var results = await files.TransformFilesParallelAsync(
            (_, content) => ValueTask.FromResult(content.ToUpper()),
            new() { OverwriteExisting = true });

        // Assert
        results.Should().HaveCount(2);

        var dest1Content = await File.ReadAllTextAsync(destFile1);
        var dest2Content = await File.ReadAllTextAsync(destFile2);

        dest1Content.Should().Be("HELLO");
        dest2Content.Should().Be("WORLD");
    }

    [Fact]
    public async Task CopyFilesParallelAsync_WithMultipleFiles_ShouldCopyAllCorrectly()
    {
        // Arrange
        var source1 = Path.Combine(_testDirectory, "copy_source1.txt");
        var source2 = Path.Combine(_testDirectory, "copy_source2.txt");
        var dest1 = Path.Combine(_testDirectory, "copy_dest1.txt");
        var dest2 = Path.Combine(_testDirectory, "copy_dest2.txt");

        await File.WriteAllTextAsync(source1, "Copy Content 1");
        await File.WriteAllTextAsync(source2, "Copy Content 2");

        var files = new[] { (source1, dest1), (source2, dest2) };

        // Act
        var results = await files.CopyFilesParallelAsync(
            new() { OverwriteExisting = true });

        // Assert
        results.Should().HaveCount(2);

        File.Exists(dest1).Should().BeTrue();
        File.Exists(dest2).Should().BeTrue();

        var dest1Content = await File.ReadAllTextAsync(dest1);
        var dest2Content = await File.ReadAllTextAsync(dest2);

        dest1Content.Should().Be("Copy Content 1");
        dest2Content.Should().Be("Copy Content 2");
    }

    [Fact]
    public async Task DeleteFilesParallelAsync_WithMultipleFiles_ShouldDeleteAllCorrectly()
    {
        // Arrange
        var file1 = Path.Combine(_testDirectory, "delete1.txt");
        var file2 = Path.Combine(_testDirectory, "delete2.txt");

        await File.WriteAllTextAsync(file1, "To delete 1");
        await File.WriteAllTextAsync(file2, "To delete 2");

        // Act
        var results = await new[] { file1, file2 }.DeleteFilesParallelAsync();

        // Assert
        results.Should().HaveCount(2);
        File.Exists(file1).Should().BeFalse();
        File.Exists(file2).Should().BeFalse();
    }

    [Fact]
    public async Task ReadAllTextParallelAsync_WithCallbacks_ShouldInvokeCallbacks()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "callback.txt");
        await File.WriteAllTextAsync(filePath, "Callback test");

        var startCalled = false;
        var completeCalled = false;

        var options = new FileOperationOptions
        {
            OnFileStartAsync = _ =>
            {
                startCalled = true;
                return ValueTask.CompletedTask;
            },
            OnFileCompleteAsync = (_, _) =>
            {
                completeCalled = true;
                return ValueTask.CompletedTask;
            }
        };

        // Act
        await new[] { filePath }.ReadAllTextParallelAsync(options);

        // Assert
        startCalled.Should().BeTrue();
        completeCalled.Should().BeTrue();
    }

    [Fact]
    public async Task WriteAllTextParallelAsync_WithCreateDirectoriesOption_ShouldCreateDirectories()
    {
        // Arrange
        var nestedPath = Path.Combine(_testDirectory, "nested", "deep", "file.txt");
        var writes = new[] { (nestedPath, "Content in nested directory") };

        var options = new FileOperationOptions
        {
            CreateDirectoriesIfNotExist = true,
            OverwriteExisting = true
        };

        // Act
        await writes.WriteAllTextParallelAsync(options);

        // Assert
        File.Exists(nestedPath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(nestedPath);
        content.Should().Be("Content in nested directory");
    }

    [Fact]
    public async Task ReadAllTextParallelAsync_WithCustomEncoding_ShouldUseEncoding()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "encoding.txt");
        var content = "Test with encoding: Привет мир";

        await File.WriteAllTextAsync(filePath, content, System.Text.Encoding.UTF8);

        var options = new FileOperationOptions
        {
            Encoding = System.Text.Encoding.UTF8
        };

        // Act
        var results = await new[] { filePath }.ReadAllTextParallelAsync(options);

        // Assert
        results[0].Should().Be(content);
    }
}
