namespace Rivulet.Core.Tests;

/// <summary>
///     Defines shared test collection names as constants to avoid string duplication.
/// </summary>
internal static class TestCollections
{
    /// <summary>
    ///     Collection for tests that use EventSource singleton and must run sequentially.
    /// </summary>
    public const string EventSourceSequential = "EventSource Sequential Tests";
}

/// <summary>
///     Collection definition for EventSource tests that must run sequentially.
///     EventSource is a singleton, so tests that verify its counters cannot run in parallel.
/// </summary>
[CollectionDefinition(TestCollections.EventSourceSequential, DisableParallelization = true)]
public class EventSourceSequentialTestCollection;