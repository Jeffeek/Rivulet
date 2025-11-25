namespace Rivulet.Diagnostics.Tests;

/// <summary>
/// Collection for tests that must run serially due to shared static state in RivuletEventSource.
/// Tests that create EventListeners without performing operations may receive events from
/// other parallel tests, causing false failures.
/// </summary>
[CollectionDefinition("Serial EventSource Tests", DisableParallelization = true)]
public class SerialEventSourceTestsCollection;

/// <summary>
/// Collection for tests that manipulate Console.Out and must run serially on Windows.
/// Parallel execution causes ObjectDisposedException in FluentAssertions when one test
/// disposes the console TextWriter while another test (or FluentAssertions) is using it.
/// This is particularly problematic on Windows due to different console resource management.
/// </summary>
[CollectionDefinition("Serial Console Tests", DisableParallelization = true)]
public class SerialConsoleTestsCollection;
