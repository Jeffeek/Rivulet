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
            catch
            {
                /* Ignore other errors */
            }
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
            catch
            {
                /* Ignore other errors */
            }
        }
    }
}