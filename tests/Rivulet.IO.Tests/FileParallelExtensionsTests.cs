using System.Text;
using Rivulet.Base.Tests;

namespace Rivulet.IO.Tests;

public class FileParallelExtensionsTests : TempDirectoryFixture
{
    [Fact]
    public async Task ReadAllTextParallelAsync_WithMultipleFiles_ShouldReadAllCorrectly()
    {
        // Arrange
        var files = new[] { "file1.txt", "file2.txt", "file3.txt" };
        var expectedContents = new[] { "Content 1", "Content 2", "Content 3" };

        for (var i = 0; i < files.Length; i++)
        {
            var path = Path.Join(TestDirectory, files[i]);
            await File.WriteAllTextAsync(path, expectedContents[i]);
        }

        var filePaths = files.Select(f => Path.Join(TestDirectory, f));

        // Act
        var results = await filePaths.ReadAllTextParallelAsync();

        // Assert
        results.Count.ShouldBe(3);
        foreach (var expected in expectedContents) results.ShouldContain(expected);
    }

    [Fact]
    public async Task ReadAllTextParallelAsync_WithNullFilePaths_ShouldThrow()
    {
        // Act
        var act = async () => await ((IEnumerable<string>)null!).ReadAllTextParallelAsync();

        // Assert
        await act.ShouldThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReadAllBytesParallelAsync_WithMultipleFiles_ShouldReadAllCorrectly()
    {
        // Arrange
        var files = new[] { "byte1.bin", "byte2.bin" };
        var expectedContents = new[] { new byte[] { 1, 2, 3 }, new byte[] { 4, 5, 6 } };

        for (var i = 0; i < files.Length; i++)
        {
            var path = Path.Join(TestDirectory, files[i]);
            await File.WriteAllBytesAsync(path, expectedContents[i]);
        }

        var filePaths = files.Select(f => Path.Join(TestDirectory, f)).ToList();

        // Act
        var results = await filePaths.ReadAllBytesParallelAsync(
            new() { ParallelOptions = new() { OrderedOutput = true } });

        // Assert
        results.Count.ShouldBe(2);
        results[0].ShouldBe(expectedContents[0]);
        results[1].ShouldBe(expectedContents[1]);
    }

    [Fact]
    public async Task ReadAllLinesParallelAsync_WithMultipleFiles_ShouldReadAllLines()
    {
        // Arrange
        var file1Path = Path.Join(TestDirectory, "lines1.txt");
        var file2Path = Path.Join(TestDirectory, "lines2.txt");

        await File.WriteAllLinesAsync(file1Path, ["Line 1", "Line 2"]);
        await File.WriteAllLinesAsync(file2Path, ["Line A", "Line B", "Line C"]);

        // Act
        var results = await new[] { file1Path, file2Path }.ReadAllLinesParallelAsync(
            new() { ParallelOptions = new() { OrderedOutput = true } });

        // Assert
        results.Count.ShouldBe(2);
        results[0].ShouldBe(["Line 1", "Line 2"]);
        results[1].ShouldBe(["Line A", "Line B", "Line C"]);
    }

    [Fact]
    public async Task WriteAllTextParallelAsync_WithMultipleFiles_ShouldWriteAllCorrectly()
    {
        // Arrange
        var writes = new[] { (Path.Join(TestDirectory, "write1.txt"), "Content 1"), (Path.Join(TestDirectory, "write2.txt"), "Content 2") };

        var options = new FileOperationOptions { OverwriteExisting = true };

        // Act
        var results = await writes.WriteAllTextParallelAsync(options);

        // Assert
        results.Count.ShouldBe(2);

        var content1 = await File.ReadAllTextAsync(writes[0].Item1);
        var content2 = await File.ReadAllTextAsync(writes[1].Item1);

        content1.ShouldBe("Content 1");
        content2.ShouldBe("Content 2");
    }

    [Fact]
    public async Task WriteAllTextParallelAsync_WithExistingFile_ShouldThrowWhenOverwriteFalse()
    {
        // Arrange
        var filePath = Path.Join(TestDirectory, "existing.txt");
        await File.WriteAllTextAsync(filePath, "Existing");

        var writes = new[] { (filePath, "New Content") };
        var options = new FileOperationOptions { OverwriteExisting = false };

        // Act
        var act = async () => await writes.WriteAllTextParallelAsync(options);

        // Assert
        await act.ShouldThrowAsync<IOException>();
    }

    [Fact]
    public async Task WriteAllBytesParallelAsync_WithMultipleFiles_ShouldWriteAllCorrectly()
    {
        // Arrange
        var writes = new[]
        {
            (Path.Join(TestDirectory, "bytes1.bin"), [1, 2, 3]), (Path.Join(TestDirectory, "bytes2.bin"), new byte[] { 4, 5, 6 })
        };

        var options = new FileOperationOptions { OverwriteExisting = true };

        // Act
        var results = await writes.WriteAllBytesParallelAsync(options);

        // Assert
        results.Count.ShouldBe(2);

        var bytes1 = await File.ReadAllBytesAsync(writes[0].Item1);
        var bytes2 = await File.ReadAllBytesAsync(writes[1].Item1);

        ((IEnumerable<byte>)bytes1).ShouldBe(new byte[] { 1, 2, 3 });
        ((IEnumerable<byte>)bytes2).ShouldBe(new byte[] { 4, 5, 6 });
    }

    [Fact]
    public async Task TransformFilesParallelAsync_WithTransformFunction_ShouldTransformCorrectly()
    {
        // Arrange
        var sourceFile1 = Path.Join(TestDirectory, "source1.txt");
        var sourceFile2 = Path.Join(TestDirectory, "source2.txt");
        var destFile1 = Path.Join(TestDirectory, "dest1.txt");
        var destFile2 = Path.Join(TestDirectory, "dest2.txt");

        await File.WriteAllTextAsync(sourceFile1, "hello");
        await File.WriteAllTextAsync(sourceFile2, "world");

        var files = new[] { (sourceFile1, destFile1), (sourceFile2, destFile2) };

        // Act
        var results = await files.TransformFilesParallelAsync(static (_, content) => ValueTask.FromResult(content.ToUpper()),
            new() { OverwriteExisting = true });

        // Assert
        results.Count.ShouldBe(2);

        var dest1Content = await File.ReadAllTextAsync(destFile1);
        var dest2Content = await File.ReadAllTextAsync(destFile2);

        dest1Content.ShouldBe("HELLO");
        dest2Content.ShouldBe("WORLD");
    }

    [Fact]
    public async Task CopyFilesParallelAsync_WithMultipleFiles_ShouldCopyAllCorrectly()
    {
        // Arrange
        var source1 = Path.Join(TestDirectory, "copy_source1.txt");
        var source2 = Path.Join(TestDirectory, "copy_source2.txt");
        var dest1 = Path.Join(TestDirectory, "copy_dest1.txt");
        var dest2 = Path.Join(TestDirectory, "copy_dest2.txt");

        await File.WriteAllTextAsync(source1, "Copy Content 1");
        await File.WriteAllTextAsync(source2, "Copy Content 2");

        var files = new[] { (source1, dest1), (source2, dest2) };

        // Act
        var results = await files.CopyFilesParallelAsync(
            new() { OverwriteExisting = true });

        // Assert
        results.Count.ShouldBe(2);

        File.Exists(dest1).ShouldBeTrue();
        File.Exists(dest2).ShouldBeTrue();

        var dest1Content = await File.ReadAllTextAsync(dest1);
        var dest2Content = await File.ReadAllTextAsync(dest2);

        dest1Content.ShouldBe("Copy Content 1");
        dest2Content.ShouldBe("Copy Content 2");
    }

    [Fact]
    public async Task DeleteFilesParallelAsync_WithMultipleFiles_ShouldDeleteAllCorrectly()
    {
        // Arrange
        var file1 = Path.Join(TestDirectory, "delete1.txt");
        var file2 = Path.Join(TestDirectory, "delete2.txt");

        await File.WriteAllTextAsync(file1, "To delete 1");
        await File.WriteAllTextAsync(file2, "To delete 2");

        // Act
        var results = await new[] { file1, file2 }.DeleteFilesParallelAsync();

        // Assert
        results.Count.ShouldBe(2);
        File.Exists(file1).ShouldBeFalse();
        File.Exists(file2).ShouldBeFalse();
    }

    [Fact]
    public async Task ReadAllTextParallelAsync_WithCallbacks_ShouldInvokeCallbacks()
    {
        // Arrange
        var filePath = Path.Join(TestDirectory, "callback.txt");
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
        startCalled.ShouldBeTrue();
        completeCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task WriteAllTextParallelAsync_WithCreateDirectoriesOption_ShouldCreateDirectories()
    {
        // Arrange
        var nestedPath = Path.Join(TestDirectory, "nested", "deep", "file.txt");
        var writes = new[] { (nestedPath, "Content in nested directory") };

        var options = new FileOperationOptions { CreateDirectoriesIfNotExist = true, OverwriteExisting = true };

        // Act
        await writes.WriteAllTextParallelAsync(options);

        // Assert
        File.Exists(nestedPath).ShouldBeTrue();
        var content = await File.ReadAllTextAsync(nestedPath);
        content.ShouldBe("Content in nested directory");
    }

    [Fact]
    public async Task ReadAllTextParallelAsync_WithCustomEncoding_ShouldUseEncoding()
    {
        // Arrange
        var filePath = Path.Join(TestDirectory, "encoding.txt");
        var content = "Test with encoding: Привет мир";

        await File.WriteAllTextAsync(filePath, content, Encoding.UTF8);

        var options = new FileOperationOptions { Encoding = Encoding.UTF8 };

        // Act
        var results = await new[] { filePath }.ReadAllTextParallelAsync(options);

        // Assert
        results[0].ShouldBe(content);
    }
}