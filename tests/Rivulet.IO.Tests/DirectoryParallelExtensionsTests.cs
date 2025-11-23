namespace Rivulet.IO.Tests;

public class DirectoryParallelExtensionsTests : IDisposable
{
    private readonly string _testDirectory;

    public DirectoryParallelExtensionsTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"RivuletIO_DirTests_{Guid.NewGuid()}");
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
    public async Task ProcessFilesParallelAsync_WithMultipleFiles_ShouldProcessAll()
    {
        // Arrange
        var file1 = Path.Combine(_testDirectory, "process1.txt");
        var file2 = Path.Combine(_testDirectory, "process2.txt");

        await File.WriteAllTextAsync(file1, "Content 1");
        await File.WriteAllTextAsync(file2, "Content 2");

        var files = new[] { file1, file2 };

        // Act
        var results = await files.ProcessFilesParallelAsync(
            async (path, ct) =>
            {
                var content = await File.ReadAllTextAsync(path, ct);
                return content.Length;
            });

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(9); // "Content 1".Length
        results.Should().Contain(9); // "Content 2".Length
    }

    [Fact]
    public async Task ProcessFilesParallelAsync_WithNullProcessFunc_ShouldThrow()
    {
        // Arrange
        var files = new[] { "file.txt" };

        // Act
        var act = async () => await files.ProcessFilesParallelAsync<int>(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ProcessDirectoryFilesParallelAsync_WithSearchPattern_ShouldProcessMatchingFiles()
    {
        // Arrange
        var txtFile1 = Path.Combine(_testDirectory, "file1.txt");
        var txtFile2 = Path.Combine(_testDirectory, "file2.txt");
        var csvFile = Path.Combine(_testDirectory, "file3.csv");

        await File.WriteAllTextAsync(txtFile1, "Text 1");
        await File.WriteAllTextAsync(txtFile2, "Text 2");
        await File.WriteAllTextAsync(csvFile, "CSV data");

        // Act
        var results = await DirectoryParallelExtensions.ProcessDirectoryFilesParallelAsync(
            _testDirectory,
            "*.txt",
            async (path, ct) =>
            {
                var content = await File.ReadAllTextAsync(path, ct);
                return content;
            });

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain("Text 1");
        results.Should().Contain("Text 2");
        results.Should().NotContain("CSV data");
    }

    [Fact]
    public async Task ProcessDirectoryFilesParallelAsync_WithNonExistentDirectory_ShouldThrow()
    {
        // Arrange
        var nonExistent = Path.Combine(_testDirectory, "nonexistent");

        // Act
        var act = async () => await DirectoryParallelExtensions.ProcessDirectoryFilesParallelAsync(
            nonExistent,
            "*.*",
            (path, _) => ValueTask.FromResult(path));

        // Assert
        await act.Should().ThrowAsync<DirectoryNotFoundException>();
    }

    [Fact]
    public async Task ProcessDirectoryFilesParallelAsync_WithSubdirectories_ShouldProcessRecursively()
    {
        // Arrange
        var subDir = Path.Combine(_testDirectory, "subdir");
        Directory.CreateDirectory(subDir);

        var file1 = Path.Combine(_testDirectory, "root.txt");
        var file2 = Path.Combine(subDir, "nested.txt");

        await File.WriteAllTextAsync(file1, "Root");
        await File.WriteAllTextAsync(file2, "Nested");

        // Act
        var results = await DirectoryParallelExtensions.ProcessDirectoryFilesParallelAsync(
            _testDirectory,
            "*.txt",
            async (path, ct) =>
            {
                var content = await File.ReadAllTextAsync(path, ct);
                return content;
            },
            SearchOption.AllDirectories);

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain("Root");
        results.Should().Contain("Nested");
    }

    [Fact]
    public async Task ReadDirectoryFilesParallelAsync_WithMultipleFiles_ShouldReturnDictionary()
    {
        // Arrange
        var file1 = Path.Combine(_testDirectory, "read1.txt");
        var file2 = Path.Combine(_testDirectory, "read2.txt");

        await File.WriteAllTextAsync(file1, "Content 1");
        await File.WriteAllTextAsync(file2, "Content 2");

        // Act
        var results = await DirectoryParallelExtensions.ReadDirectoryFilesParallelAsync(
            _testDirectory,
            "*.txt");

        // Assert
        results.Should().HaveCount(2);
        results.Should().ContainKey(file1).WhoseValue.Should().Be("Content 1");
        results.Should().ContainKey(file2).WhoseValue.Should().Be("Content 2");
    }

    [Fact]
    public async Task TransformDirectoryFilesParallelAsync_WithTransformation_ShouldTransformAllFiles()
    {
        // Arrange
        var sourceDir = Path.Combine(_testDirectory, "source");
        var destDir = Path.Combine(_testDirectory, "dest");

        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(destDir);

        var file1 = Path.Combine(sourceDir, "file1.txt");
        var file2 = Path.Combine(sourceDir, "file2.txt");

        await File.WriteAllTextAsync(file1, "hello");
        await File.WriteAllTextAsync(file2, "world");

        // Act
        var results = await DirectoryParallelExtensions.TransformDirectoryFilesParallelAsync(
            sourceDir,
            destDir,
            "*.txt",
            (_, content) => ValueTask.FromResult(content.ToUpper()),
            options: new() { OverwriteExisting = true });

        // Assert
        results.Should().HaveCount(2);

        var destFile1 = Path.Combine(destDir, "file1.txt");
        var destFile2 = Path.Combine(destDir, "file2.txt");

        File.Exists(destFile1).Should().BeTrue();
        File.Exists(destFile2).Should().BeTrue();

        var content1 = await File.ReadAllTextAsync(destFile1);
        var content2 = await File.ReadAllTextAsync(destFile2);

        content1.Should().Be("HELLO");
        content2.Should().Be("WORLD");
    }

    [Fact]
    public async Task CopyDirectoryFilesParallelAsync_WithMultipleFiles_ShouldCopyAll()
    {
        // Arrange
        var sourceDir = Path.Combine(_testDirectory, "copy_source");
        var destDir = Path.Combine(_testDirectory, "copy_dest");

        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(destDir);

        var file1 = Path.Combine(sourceDir, "copy1.txt");
        var file2 = Path.Combine(sourceDir, "copy2.txt");

        await File.WriteAllTextAsync(file1, "Copy 1");
        await File.WriteAllTextAsync(file2, "Copy 2");

        // Act
        var results = await DirectoryParallelExtensions.CopyDirectoryFilesParallelAsync(
            sourceDir,
            destDir,
            options: new() { OverwriteExisting = true });

        // Assert
        results.Should().HaveCount(2);

        var destFile1 = Path.Combine(destDir, "copy1.txt");
        var destFile2 = Path.Combine(destDir, "copy2.txt");

        File.Exists(destFile1).Should().BeTrue();
        File.Exists(destFile2).Should().BeTrue();

        var content1 = await File.ReadAllTextAsync(destFile1);
        var content2 = await File.ReadAllTextAsync(destFile2);

        content1.Should().Be("Copy 1");
        content2.Should().Be("Copy 2");
    }

    [Fact]
    public async Task DeleteDirectoryFilesParallelAsync_WithMatchingPattern_ShouldDeleteMatchingFiles()
    {
        // Arrange
        var file1 = Path.Combine(_testDirectory, "delete1.txt");
        var file2 = Path.Combine(_testDirectory, "delete2.txt");
        var file3 = Path.Combine(_testDirectory, "keep.csv");

        await File.WriteAllTextAsync(file1, "Delete 1");
        await File.WriteAllTextAsync(file2, "Delete 2");
        await File.WriteAllTextAsync(file3, "Keep this");

        // Act
        var results = await DirectoryParallelExtensions.DeleteDirectoryFilesParallelAsync(
            _testDirectory,
            "*.txt");

        // Assert
        results.Should().HaveCount(2);

        File.Exists(file1).Should().BeFalse();
        File.Exists(file2).Should().BeFalse();
        File.Exists(file3).Should().BeTrue(); // CSV file should not be deleted
    }

    [Fact]
    public async Task ProcessMultipleDirectoriesParallelAsync_WithMultipleDirectories_ShouldProcessAllFiles()
    {
        // Arrange
        var dir1 = Path.Combine(_testDirectory, "dir1");
        var dir2 = Path.Combine(_testDirectory, "dir2");

        Directory.CreateDirectory(dir1);
        Directory.CreateDirectory(dir2);

        var file1 = Path.Combine(dir1, "file1.txt");
        var file2 = Path.Combine(dir2, "file2.txt");

        await File.WriteAllTextAsync(file1, "Dir1 Content");
        await File.WriteAllTextAsync(file2, "Dir2 Content");

        var directories = new[] { dir1, dir2 };

        // Act
        var results = await directories.ProcessMultipleDirectoriesParallelAsync(
            "*.txt",
            async (path, ct) =>
            {
                var content = await File.ReadAllTextAsync(path, ct);
                return content.Length;
            });

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(12); // "Dir1 Content".Length
        results.Should().Contain(12); // "Dir2 Content".Length
    }

    [Fact]
    public async Task ProcessMultipleDirectoriesParallelAsync_WithNonExistentDirectory_ShouldSkipNonExistent()
    {
        // Arrange
        var existingDir = Path.Combine(_testDirectory, "existing");
        var nonExistentDir = Path.Combine(_testDirectory, "nonexistent");

        Directory.CreateDirectory(existingDir);

        var file = Path.Combine(existingDir, "file.txt");
        await File.WriteAllTextAsync(file, "Content");

        var directories = new[] { existingDir, nonExistentDir };

        // Act
        var results = await directories.ProcessMultipleDirectoriesParallelAsync(
            "*.txt",
            async (path, ct) =>
            {
                var content = await File.ReadAllTextAsync(path, ct);
                return content;
            });

        // Assert
        results.Should().HaveCount(1);
        results.Should().Contain("Content");
    }

    [Fact]
    public async Task ProcessFilesParallelAsync_WithCallbacks_ShouldInvokeCallbacks()
    {
        // Arrange
        var file = Path.Combine(_testDirectory, "callback.txt");
        await File.WriteAllTextAsync(file, "Callback test");

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
        await new[] { file }.ProcessFilesParallelAsync(
            (path, _) => ValueTask.FromResult(path),
            options);

        // Assert
        startCalled.Should().BeTrue();
        completeCalled.Should().BeTrue();
    }

    [Fact]
    public async Task CopyDirectoryFilesParallelAsync_WithSubdirectories_ShouldPreserveStructure()
    {
        // Arrange
        var sourceDir = Path.Combine(_testDirectory, "source_nested");
        var destDir = Path.Combine(_testDirectory, "dest_nested");
        var subDir = Path.Combine(sourceDir, "subdir");

        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(subDir);
        Directory.CreateDirectory(destDir);

        var rootFile = Path.Combine(sourceDir, "root.txt");
        var nestedFile = Path.Combine(subDir, "nested.txt");

        await File.WriteAllTextAsync(rootFile, "Root content");
        await File.WriteAllTextAsync(nestedFile, "Nested content");

        // Act
        var results = await DirectoryParallelExtensions.CopyDirectoryFilesParallelAsync(
            sourceDir,
            destDir,
            searchOption: SearchOption.AllDirectories,
            options: new() { OverwriteExisting = true });

        // Assert
        results.Should().HaveCount(2);

        var destRootFile = Path.Combine(destDir, "root.txt");
        var destNestedFile = Path.Combine(destDir, "subdir", "nested.txt");

        File.Exists(destRootFile).Should().BeTrue();
        File.Exists(destNestedFile).Should().BeTrue();

        var rootContent = await File.ReadAllTextAsync(destRootFile);
        var nestedContent = await File.ReadAllTextAsync(destNestedFile);

        rootContent.Should().Be("Root content");
        nestedContent.Should().Be("Nested content");
    }
}
