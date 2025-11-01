namespace Rivulet.Diagnostics.Tests;

/// <summary>
/// Collection for tests that must run serially due to shared static state in RivuletEventSource.
/// Tests that create EventListeners without performing operations may receive events from
/// other parallel tests, causing false failures.
/// </summary>
[CollectionDefinition("Serial EventSource Tests", DisableParallelization = true)]
public class SerialEventSourceTestsCollection;
