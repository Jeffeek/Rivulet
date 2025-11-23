namespace Rivulet.IO.Tests;

public class DirectoryInfoExtensionsTests : IDisposable
{
    private readonly string _testDirectory;

    public DirectoryInfoExtensionsTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"RivuletTests_{Guid.NewGuid()}");
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
    public async Task ProcessFilesParallelAsync_WithDirectoryInfo_ShouldProcessAllFiles()
    {
        // Arrange
        var directory = new DirectoryInfo(_testDirectory);

        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "file1.txt"), "Content1");
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "file2.txt"), "Content2");
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "file3.txt"), "Content3");

        // Act
        var results = await directory.ProcessFilesParallelAsync(
            async (filePath, ct) =>
            {
                var content = await File.ReadAllTextAsync(filePath, ct);
                return content.Length;
            },
            "*.txt");

        // Assert
        results.Should().HaveCount(3);
        results.Should().AllSatisfy(r => r.Should().BeGreaterThan(0));
    }

    [Fact]
    public async Task ReadAllFilesParallelAsync_WithDirectoryInfo_ShouldReturnDictionary()
    {
        // Arrange
        var directory = new DirectoryInfo(_testDirectory);

        var file1 = Path.Combine(_testDirectory, "read1.txt");
        var file2 = Path.Combine(_testDirectory, "read2.txt");

        await File.WriteAllTextAsync(file1, "Content A");
        await File.WriteAllTextAsync(file2, "Content B");

        // Act
        var results = await directory.ReadAllFilesParallelAsync("*.txt");

        // Assert
        results.Should().HaveCount(2);
        results.Should().ContainKey(file1).WhoseValue.Should().Be("Content A");
        results.Should().ContainKey(file2).WhoseValue.Should().Be("Content B");
    }

    [Fact]
    public void GetFilesEnumerable_WithDirectoryInfo_ShouldReturnFileInfos()
    {
        // Arrange
        var directory = new DirectoryInfo(_testDirectory);

        File.WriteAllText(Path.Combine(_testDirectory, "enum1.txt"), "Test1");
        File.WriteAllText(Path.Combine(_testDirectory, "enum2.txt"), "Test2");
        File.WriteAllText(Path.Combine(_testDirectory, "skip.dat"), "Skip");

        // Act
        var files = directory.GetFilesEnumerable("*.txt").ToList();

        // Assert
        files.Should().HaveCount(2);
        files.Should().AllSatisfy(f => f.Extension.Should().Be(".txt"));
    }

    [Fact]
    public async Task TransformFilesParallelAsync_WithDirectoryInfo_ShouldTransformFiles()
    {
        // Arrange
        var sourceDirectory = new DirectoryInfo(_testDirectory);
        var destDirectory = Path.Combine(_testDirectory, "transformed");

        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "trans1.txt"), "lower");
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "trans2.txt"), "case");

        // Act
        var results = await sourceDirectory.TransformFilesParallelAsync(
            destDirectory,
            async (_, content) => await ValueTask.FromResult(content.ToUpperInvariant()),
            "*.txt");

        // Assert
        results.Should().HaveCount(2);
        var destFile1 = Path.Combine(destDirectory, "trans1.txt");
        var destFile2 = Path.Combine(destDirectory, "trans2.txt");

        File.Exists(destFile1).Should().BeTrue();
        File.Exists(destFile2).Should().BeTrue();

        var content1 = await File.ReadAllTextAsync(destFile1);
        var content2 = await File.ReadAllTextAsync(destFile2);

        content1.Should().Be("LOWER");
        content2.Should().Be("CASE");
    }

    [Fact]
    public async Task CopyFilesToParallelAsync_WithDirectoryInfo_ShouldCopyFiles()
    {
        // Arrange
        var sourceDirectory = new DirectoryInfo(_testDirectory);
        var destDirectory = Path.Combine(_testDirectory, "copied");

        var file1 = Path.Combine(_testDirectory, "copy1.txt");
        var file2 = Path.Combine(_testDirectory, "copy2.txt");

        await File.WriteAllTextAsync(file1, "Copy Content 1");
        await File.WriteAllTextAsync(file2, "Copy Content 2");

        // Act
        var results = await sourceDirectory.CopyFilesToParallelAsync(destDirectory, "*.txt");

        // Assert
        results.Should().HaveCount(2);

        var destFile1 = Path.Combine(destDirectory, "copy1.txt");
        var destFile2 = Path.Combine(destDirectory, "copy2.txt");

        File.Exists(destFile1).Should().BeTrue();
        File.Exists(destFile2).Should().BeTrue();

        var destContent1 = await File.ReadAllTextAsync(destFile1);
        var destContent2 = await File.ReadAllTextAsync(destFile2);

        destContent1.Should().Be("Copy Content 1");
        destContent2.Should().Be("Copy Content 2");
    }

    [Fact]
    public async Task DeleteFilesParallelAsync_WithDirectoryInfo_ShouldDeleteFiles()
    {
        // Arrange
        var directory = new DirectoryInfo(_testDirectory);

        var file1 = Path.Combine(_testDirectory, "delete1.tmp");
        var file2 = Path.Combine(_testDirectory, "delete2.tmp");
        var keepFile = Path.Combine(_testDirectory, "keep.txt");

        await File.WriteAllTextAsync(file1, "Delete me");
        await File.WriteAllTextAsync(file2, "Delete me too");
        await File.WriteAllTextAsync(keepFile, "Keep me");

        // Act
        var results = await directory.DeleteFilesParallelAsync("*.tmp");

        // Assert
        results.Should().HaveCount(2);
        File.Exists(file1).Should().BeFalse();
        File.Exists(file2).Should().BeFalse();
        File.Exists(keepFile).Should().BeTrue();
    }

    [Fact]
    public async Task ProcessMultipleDirectoriesParallelAsync_ShouldProcessAllDirectories()
    {
        // Arrange
        var dir1 = new DirectoryInfo(Path.Combine(_testDirectory, "dir1"));
        var dir2 = new DirectoryInfo(Path.Combine(_testDirectory, "dir2"));

        dir1.Create();
        dir2.Create();

        await File.WriteAllTextAsync(Path.Combine(dir1.FullName, "file1.txt"), "Dir1Content");
        await File.WriteAllTextAsync(Path.Combine(dir2.FullName, "file2.txt"), "Dir2Content");

        var directories = new[] { dir1, dir2 };

        // Act
        var results = await directories.ProcessMultipleDirectoriesParallelAsync(
            "*.txt",
            async (filePath, ct) =>
            {
                var content = await File.ReadAllTextAsync(filePath, ct);
                return content.Length;
            });

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(r => r.Should().BeGreaterThan(0));
    }

    [Fact]
    public async Task ProcessFilesParallelAsync_WithNonExistentDirectory_ShouldThrow()
    {
        // Arrange
        var directory = new DirectoryInfo(Path.Combine(_testDirectory, "nonexistent"));

        // Act
        var act = async () => await directory.ProcessFilesParallelAsync(
            async (filePath, _) => await ValueTask.FromResult(filePath),
            "*.txt");

        // Assert
        await act.Should().ThrowAsync<DirectoryNotFoundException>();
    }

    [Fact]
    public async Task ReadAllFilesParallelAsync_WithSearchOption_ShouldSearchRecursively()
    {
        // Arrange
        var directory = new DirectoryInfo(_testDirectory);
        var subDirectory = Path.Combine(_testDirectory, "sub");
        Directory.CreateDirectory(subDirectory);

        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "root.txt"), "Root");
        await File.WriteAllTextAsync(Path.Combine(subDirectory, "sub.txt"), "Sub");

        // Act
        var results = await directory.ReadAllFilesParallelAsync(
            "*.txt",
            SearchOption.AllDirectories);

        // Assert
        results.Should().HaveCount(2);
        results.Values.Should().Contain("Root");
        results.Values.Should().Contain("Sub");
    }

    [Fact]
    public async Task TransformFilesParallelAsync_WithCustomOptions_ShouldRespectOptions()
    {
        // Arrange
        var sourceDirectory = new DirectoryInfo(_testDirectory);
        var destDirectory = Path.Combine(_testDirectory, "customtransform");

        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "custom.txt"), "test");

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
        await sourceDirectory.TransformFilesParallelAsync(
            destDirectory,
            async (_, content) => await ValueTask.FromResult(content),
            "*.txt",
            SearchOption.TopDirectoryOnly,
            options);

        // Assert
        startCalled.Should().BeTrue();
        completeCalled.Should().BeTrue();
    }

    [Fact]
    public async Task CopyFilesToParallelAsync_WithOverwriteExisting_ShouldOverwrite()
    {
        // Arrange
        var sourceDirectory = new DirectoryInfo(_testDirectory);
        var destDirectory = Path.Combine(_testDirectory, "overwrite");
        Directory.CreateDirectory(destDirectory);

        var sourceFile = Path.Combine(_testDirectory, "overwrite.txt");
        var destFile = Path.Combine(destDirectory, "overwrite.txt");

        await File.WriteAllTextAsync(sourceFile, "New content");
        await File.WriteAllTextAsync(destFile, "Old content");

        var options = new FileOperationOptions { OverwriteExisting = true };

        // Act
        await sourceDirectory.CopyFilesToParallelAsync(destDirectory, "*.txt", SearchOption.TopDirectoryOnly, options);

        // Assert
        var content = await File.ReadAllTextAsync(destFile);
        content.Should().Be("New content");
    }
}
