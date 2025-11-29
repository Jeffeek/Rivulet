namespace Rivulet.Base.Tests;

/// <summary>
/// Base class for tests that need a temporary directory.
/// Creates a unique temporary directory on construction and cleans it up on disposal.
/// </summary>
public abstract class TempDirectoryFixture : IDisposable
{
    /// <summary>
    /// Gets the path to the temporary test directory.
    /// </summary>
    protected string TestDirectory { get; }

    protected TempDirectoryFixture()
    {
        TestDirectory = Path.Join(Path.GetTempPath(), $"RivuletTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(TestDirectory);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!disposing || !Directory.Exists(TestDirectory)) return;

        try
        {
            Directory.Delete(TestDirectory, recursive: true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
