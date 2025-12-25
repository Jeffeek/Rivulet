using Rivulet.Base.Tests;

namespace Rivulet.IO.Tests;

public sealed class DirectoryInfoExtensionsTests : TempDirectoryFixture
{
    [Fact]
    public async Task ProcessFilesParallelAsync_WithDirectoryInfo_ShouldProcessAllFiles()
    {
        // Arrange
        var directory = new DirectoryInfo(TestDirectory);

        await File.WriteAllTextAsync(Path.Join(TestDirectory, "file1.txt"), "Content1");
        await File.WriteAllTextAsync(Path.Join(TestDirectory, "file2.txt"), "Content2");
        await File.WriteAllTextAsync(Path.Join(TestDirectory, "file3.txt"), "Content3");

        // Act
        var results = await directory.ProcessFilesParallelAsync(static async (filePath, ct) =>
            {
                var content = await File.ReadAllTextAsync(filePath, ct);
                return content.Length;
            },
            "*.txt");

        // Assert
        results.Count.ShouldBe(3);
        results.ShouldAllBe(static r => r > 0);
    }

    [Fact]
    public async Task ReadAllFilesParallelAsync_WithDirectoryInfo_ShouldReturnDictionary()
    {
        // Arrange
        var directory = new DirectoryInfo(TestDirectory);

        var file1 = Path.Join(TestDirectory, "read1.txt");
        var file2 = Path.Join(TestDirectory, "read2.txt");

        await File.WriteAllTextAsync(file1, "Content A");
        await File.WriteAllTextAsync(file2, "Content B");

        // Act
        var results = await directory.ReadAllFilesParallelAsync("*.txt");

        // Assert
        results.Count.ShouldBe(2);
        results.TryGetValue(file1, out var value1).ShouldBeTrue();
        value1.ShouldBe("Content A");
        results.TryGetValue(file2, out var value2).ShouldBeTrue();
        value2.ShouldBe("Content B");
    }

    [Fact]
    public void GetFilesEnumerable_WithDirectoryInfo_ShouldReturnFileInfos()
    {
        // Arrange
        var directory = new DirectoryInfo(TestDirectory);

        File.WriteAllText(Path.Join(TestDirectory, "enum1.txt"), "Test1");
        File.WriteAllText(Path.Join(TestDirectory, "enum2.txt"), "Test2");
        File.WriteAllText(Path.Join(TestDirectory, "skip.dat"), "Skip");

        // Act
        var files = directory.GetFilesEnumerable("*.txt").ToList();

        // Assert
        files.Count.ShouldBe(2);
        files.ShouldAllBe(static f => f.Extension == ".txt");
    }

    [Fact]
    public async Task TransformFilesParallelAsync_WithDirectoryInfo_ShouldTransformFiles()
    {
        // Arrange
        var sourceDirectory = new DirectoryInfo(TestDirectory);
        var destDirectory = Path.Join(TestDirectory, "transformed");

        await File.WriteAllTextAsync(Path.Join(TestDirectory, "trans1.txt"), "lower");
        await File.WriteAllTextAsync(Path.Join(TestDirectory, "trans2.txt"), "case");

        // Act
        var results = await sourceDirectory.TransformFilesParallelAsync(
            destDirectory,
            static (_, content) => ValueTask.FromResult(content.ToUpperInvariant()),
            "*.txt");

        // Assert
        results.Count.ShouldBe(2);
        var destFile1 = Path.Join(destDirectory, "trans1.txt");
        var destFile2 = Path.Join(destDirectory, "trans2.txt");

        File.Exists(destFile1).ShouldBeTrue();
        File.Exists(destFile2).ShouldBeTrue();

        var content1 = await File.ReadAllTextAsync(destFile1);
        var content2 = await File.ReadAllTextAsync(destFile2);

        content1.ShouldBe("LOWER");
        content2.ShouldBe("CASE");
    }

    [Fact]
    public async Task CopyFilesToParallelAsync_WithDirectoryInfo_ShouldCopyFiles()
    {
        // Arrange
        var sourceDirectory = new DirectoryInfo(TestDirectory);
        var destDirectory = Path.Join(TestDirectory, "copied");

        var file1 = Path.Join(TestDirectory, "copy1.txt");
        var file2 = Path.Join(TestDirectory, "copy2.txt");

        await File.WriteAllTextAsync(file1, "Copy Content 1");
        await File.WriteAllTextAsync(file2, "Copy Content 2");

        // Act
        var results = await sourceDirectory.CopyFilesToParallelAsync(destDirectory, "*.txt");

        // Assert
        results.Count.ShouldBe(2);

        var destFile1 = Path.Join(destDirectory, "copy1.txt");
        var destFile2 = Path.Join(destDirectory, "copy2.txt");

        File.Exists(destFile1).ShouldBeTrue();
        File.Exists(destFile2).ShouldBeTrue();

        var destContent1 = await File.ReadAllTextAsync(destFile1);
        var destContent2 = await File.ReadAllTextAsync(destFile2);

        destContent1.ShouldBe("Copy Content 1");
        destContent2.ShouldBe("Copy Content 2");
    }

    [Fact]
    public async Task DeleteFilesParallelAsync_WithDirectoryInfo_ShouldDeleteFiles()
    {
        // Arrange
        var directory = new DirectoryInfo(TestDirectory);

        var file1 = Path.Join(TestDirectory, "delete1.tmp");
        var file2 = Path.Join(TestDirectory, "delete2.tmp");
        var keepFile = Path.Join(TestDirectory, "keep.txt");

        await File.WriteAllTextAsync(file1, "Delete me");
        await File.WriteAllTextAsync(file2, "Delete me too");
        await File.WriteAllTextAsync(keepFile, "Keep me");

        // Act
        var results = await directory.DeleteFilesParallelAsync("*.tmp");

        // Assert
        results.Count.ShouldBe(2);
        File.Exists(file1).ShouldBeFalse();
        File.Exists(file2).ShouldBeFalse();
        File.Exists(keepFile).ShouldBeTrue();
    }

    [Fact]
    public async Task ProcessMultipleDirectoriesParallelAsync_ShouldProcessAllDirectories()
    {
        // Arrange
        var dir1 = new DirectoryInfo(Path.Join(TestDirectory, "dir1"));
        var dir2 = new DirectoryInfo(Path.Join(TestDirectory, "dir2"));

        dir1.Create();
        dir2.Create();

        await File.WriteAllTextAsync(Path.Join(dir1.FullName, "file1.txt"), "Dir1Content");
        await File.WriteAllTextAsync(Path.Join(dir2.FullName, "file2.txt"), "Dir2Content");

        var directories = new[] { dir1, dir2 };

        // Act
        var results = await directories.ProcessMultipleDirectoriesParallelAsync(
            "*.txt",
            static async (filePath, ct) =>
            {
                var content = await File.ReadAllTextAsync(filePath, ct);
                return content.Length;
            });

        // Assert
        results.Count.ShouldBe(2);
        results.ShouldAllBe(static r => r > 0);
    }

    [Fact]
    public async Task ProcessFilesParallelAsync_WithNonExistentDirectory_ShouldThrow()
    {
        // Arrange
        var directory = new DirectoryInfo(Path.Join(TestDirectory, "nonexistent"));

        // Act
        var act = () => directory.ProcessFilesParallelAsync(static (filePath, _) => ValueTask.FromResult(filePath),
            "*.txt");

        // Assert
        await act.ShouldThrowAsync<DirectoryNotFoundException>();
    }

    [Fact]
    public async Task ReadAllFilesParallelAsync_WithSearchOption_ShouldSearchRecursively()
    {
        // Arrange
        var directory = new DirectoryInfo(TestDirectory);
        var subDirectory = Path.Join(TestDirectory, "sub");
        Directory.CreateDirectory(subDirectory);

        await File.WriteAllTextAsync(Path.Join(TestDirectory, "root.txt"), "Root");
        await File.WriteAllTextAsync(Path.Join(subDirectory, "sub.txt"), "Sub");

        // Act
        var results = await directory.ReadAllFilesParallelAsync(
            "*.txt",
            SearchOption.AllDirectories);

        // Assert
        results.Count.ShouldBe(2);
        results.Values.ShouldContain("Root");
        results.Values.ShouldContain("Sub");
    }

    [Fact]
    public async Task TransformFilesParallelAsync_WithCustomOptions_ShouldRespectOptions()
    {
        // Arrange
        var sourceDirectory = new DirectoryInfo(TestDirectory);
        var destDirectory = Path.Join(TestDirectory, "customtransform");

        await File.WriteAllTextAsync(Path.Join(TestDirectory, "custom.txt"), "test");

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
            static (_, content) => ValueTask.FromResult(content),
            "*.txt",
            SearchOption.TopDirectoryOnly,
            options);

        // Assert
        startCalled.ShouldBeTrue();
        completeCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task CopyFilesToParallelAsync_WithOverwriteExisting_ShouldOverwrite()
    {
        // Arrange
        var sourceDirectory = new DirectoryInfo(TestDirectory);
        var destDirectory = Path.Join(TestDirectory, "overwrite");
        Directory.CreateDirectory(destDirectory);

        var sourceFile = Path.Join(TestDirectory, "overwrite.txt");
        var destFile = Path.Join(destDirectory, "overwrite.txt");

        await File.WriteAllTextAsync(sourceFile, "New content");
        await File.WriteAllTextAsync(destFile, "Old content");

        var options = new FileOperationOptions { OverwriteExisting = true };

        // Act
        await sourceDirectory.CopyFilesToParallelAsync(destDirectory, "*.txt", SearchOption.TopDirectoryOnly, options);

        // Assert
        var content = await File.ReadAllTextAsync(destFile);
        content.ShouldBe("New content");
    }
}
