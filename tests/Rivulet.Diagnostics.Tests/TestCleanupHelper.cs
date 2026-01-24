namespace Rivulet.Diagnostics.Tests;

internal static class TestCleanupHelper
{
    public static void RetryDeleteFile(string filePath)
    {
        for (var i = 0; i < 5; i++)
        {
            try
            {
                if (File.Exists(filePath)) File.Delete(filePath);

                return; // Success
            }
            catch (IOException)
            {
                Thread.Sleep(50);
            }
#pragma warning disable CA1031 // Do not catch general exception types - intentional for test cleanup
            catch
            {
                /* Ignore other errors */
            }
#pragma warning restore CA1031
        }
    }

    public static void RetryDeleteDirectory(string directoryPath)
    {
        for (var i = 0; i < 5; i++)
        {
            try
            {
                if (Directory.Exists(directoryPath)) Directory.Delete(directoryPath, true);

                return; // Success
            }
            catch (IOException)
            {
                Thread.Sleep(50);
            }
#pragma warning disable CA1031 // Do not catch general exception types - intentional for test cleanup
            catch
            {
                /* Ignore other errors */
            }
#pragma warning restore CA1031
        }
    }
}
