namespace Rivulet.Diagnostics.OpenTelemetry.Tests;

/// <summary>
/// Collection for metrics tests that must run serially due to shared static state in RivuletEventSource.
/// </summary>
[CollectionDefinition("Metrics Tests", DisableParallelization = true)]
public class MetricsTestsCollection;

/// <summary>
/// Collection for ActivitySource tests that can run in parallel.
/// </summary>
[CollectionDefinition("ActivitySource Tests")]
public class ActivitySourceTestsCollection;
