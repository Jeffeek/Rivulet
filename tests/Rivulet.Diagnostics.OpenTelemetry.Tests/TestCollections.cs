namespace Rivulet.Diagnostics.OpenTelemetry.Tests;

/// <summary>
/// Defines shared test collection names as constants to avoid string duplication.
/// </summary>
public static class TestCollections
{
    /// <summary>
    /// Collection for metrics tests that use EventSource singleton and must run sequentially.
    /// </summary>
    public const string Metrics = "Metrics Tests";

    /// <summary>
    /// Collection for ActivitySource tests that can run in parallel.
    /// </summary>
    public const string ActivitySource = "ActivitySource Tests";
}

/// <summary>
/// Collection for metrics tests that must run serially due to shared static state in RivuletEventSource.
/// </summary>
[CollectionDefinition(TestCollections.Metrics, DisableParallelization = true)]
public class MetricsTestsCollection;

/// <summary>
/// Collection for ActivitySource tests that can run in parallel.
/// </summary>
[CollectionDefinition(TestCollections.ActivitySource)]
public class ActivitySourceTestsCollection;
