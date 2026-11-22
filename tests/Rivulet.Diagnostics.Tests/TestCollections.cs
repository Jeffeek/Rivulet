namespace Rivulet.Diagnostics.Tests;

/// <summary>
///     Defines shared test collection names as constants to avoid string duplication.
/// </summary>
internal static class TestCollections
{
    /// <summary>
    ///     Collection for tests that use EventSource singleton and must run sequentially.
    /// </summary>
    public const string SerialEventSource = "Serial EventSource Tests";
}

/// <summary>
///     Collection for tests that must run serially due to shared static state in RivuletEventSource.
///     Tests that create EventListeners without performing operations may receive events from
///     other parallel tests, causing false failures.
/// </summary>
[CollectionDefinition(TestCollections.SerialEventSource, DisableParallelization = true)]
public class SerialEventSourceTestsCollection;
