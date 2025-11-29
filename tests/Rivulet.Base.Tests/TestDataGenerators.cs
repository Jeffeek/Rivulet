namespace Rivulet.Base.Tests;

/// <summary>
/// Provides test data generation utilities.
/// </summary>
public static class TestDataGenerators
{
    /// <summary>
    /// Generates a sequence of integers asynchronously with optional delay between items.
    /// </summary>
    /// <param name="count">The number of items to generate.</param>
    /// <param name="delayMs">Optional delay in milliseconds between items.</param>
    /// <returns>An async enumerable sequence of integers from 1 to count.</returns>
    public static async IAsyncEnumerable<int> GenerateItemsAsync(int count, int delayMs = 0)
    {
        for (var i = 1; i <= count; i++)
        {
            if (delayMs > 0)
                await Task.Delay(delayMs);
            yield return i;
        }
    }
}
