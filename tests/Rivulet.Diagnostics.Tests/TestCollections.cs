namespace Rivulet.Diagnostics.Tests;

/// <summary>
/// Defines shared test collection names as constants to avoid string duplication.
/// </summary>
public static class TestCollections
{
    /// <summary>
    /// Collection for tests that use EventSource singleton and must run sequentially.
    /// </summary>
    public const string SerialEventSource = "Serial EventSource Tests";

    /// <summary>
    /// Collection for tests that manipulate Console.Out and must run sequentially.
    /// </summary>
    public const string SerialConsole = "Serial Console Tests";

    /// <summary>
    /// Collection for EventSource tests that must run sequentially.
    /// </summary>
    public const string EventSource = "EventSource Tests";
}

/// <summary>
/// Collection for tests that must run serially due to shared static state in RivuletEventSource.
/// Tests that create EventListeners without performing operations may receive events from
/// other parallel tests, causing false failures.
/// </summary>
[CollectionDefinition(TestCollections.SerialEventSource, DisableParallelization = true)]
public class SerialEventSourceTestsCollection;

/// <summary>
/// Collection for tests that manipulate Console.Out and must run serially on Windows.
/// Parallel execution causes ObjectDisposedException in FluentAssertions when one test
/// disposes the console TextWriter while another test (or FluentAssertions) is using it.
/// This is particularly problematic on Windows due to different console resource management.
/// </summary>
[CollectionDefinition(TestCollections.SerialConsole, DisableParallelization = true)]
public class SerialConsoleTestsCollection;
