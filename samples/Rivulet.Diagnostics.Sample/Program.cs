using Rivulet.Core;
using Rivulet.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;

Console.WriteLine("=== Rivulet.Diagnostics Sample ===\n");

// Sample 1: Console Listener - Real-time metrics output
Console.WriteLine("1. RivuletConsoleListener - Real-time console metrics");
using (new RivuletConsoleListener())
{
    var numbers = Enumerable.Range(1, 50);
    await numbers.SelectParallelAsync(
        async (n, ct) =>
        {
            await Task.Delay(10, ct);
            return n * n;
        },
        new ParallelOptionsRivulet { MaxDegreeOfParallelism = 10 });
}
Console.WriteLine("✓ Console listener demonstrated\n");

// Sample 2: File Listener - Write metrics to a file
Console.WriteLine("2. RivuletFileListener - File-based metrics logging");
var metricsFile = Path.Join(Path.GetTempPath(), "rivulet-metrics.log");
await using (new RivuletFileListener(metricsFile))
{
    var items = Enumerable.Range(1, 30);
    await items.ToAsyncEnumerable().ForEachParallelAsync(
        async (_, ct) =>
        {
            await Task.Delay(20, ct);
        },
        new ParallelOptionsRivulet { MaxDegreeOfParallelism = 5 });
}
Console.WriteLine($"✓ Metrics written to: {metricsFile}\n");

// Sample 3: Structured Log Listener - JSON format for log aggregation
Console.WriteLine("3. RivuletStructuredLogListener - JSON structured logging");
var jsonFile = Path.Join(Path.GetTempPath(), "rivulet-metrics.json");
await using (new RivuletStructuredLogListener(jsonFile))
{
    var data = Enumerable.Range(1, 25);
    await data.SelectParallelAsync(
        async (d, ct) =>
        {
            await Task.Delay(15, ct);
            return d.ToString();
        },
        new ParallelOptionsRivulet { MaxDegreeOfParallelism = 8 });
}
Console.WriteLine($"✓ JSON metrics written to: {jsonFile}\n");

// Sample 4: Prometheus Exporter - Metrics in Prometheus format
Console.WriteLine("4. PrometheusExporter - Prometheus text format");
using (var prometheusExporter = new PrometheusExporter())
{
    var workItems = Enumerable.Range(1, 40);
    await workItems.SelectParallelAsync(
        async (item, ct) =>
        {
            await Task.Delay(25, ct);
            if (item % 10 == 0) throw new InvalidOperationException("Simulated error");
            return item;
        },
        new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 6,
            MaxRetries = 2,
            IsTransient = ex => ex is InvalidOperationException,
            ErrorMode = ErrorMode.CollectAndContinue
        });

    // Export metrics in Prometheus format
    var prometheusText = prometheusExporter.Export();
    Console.WriteLine("Prometheus metrics:");
    Console.WriteLine(prometheusText);

    // Export as dictionary
    var metricsDict = prometheusExporter.ExportDictionary();
    Console.WriteLine("\nMetrics dictionary:");
    foreach (var kvp in metricsDict)
    {
        Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
    }
}
Console.WriteLine("\n✓ Prometheus export demonstrated\n");

// Sample 5: Health Check - Monitor Rivulet operations
Console.WriteLine("5. RivuletHealthCheck - Health monitoring");
using (var exporter = new PrometheusExporter())
{
    var healthCheck = new RivuletHealthCheck(exporter, new RivuletHealthCheckOptions
    {
        ErrorRateThreshold = 0.15,  // 15% error rate threshold
        FailureCountThreshold = 100
    });

    // Run some operations with errors
    var testData = Enumerable.Range(1, 100);
    await testData.SelectParallelAsync(
        async (item, ct) =>
        {
            await Task.Delay(5, ct);
            if (item % 8 == 0) throw new Exception("Simulated error");
            return item;
        },
        new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 10,
            ErrorMode = ErrorMode.CollectAndContinue
        });

    // Check health
    var context = new HealthCheckContext();
    var result = await healthCheck.CheckHealthAsync(context);

    Console.WriteLine($"Health Status: {result.Status}");
    Console.WriteLine($"Description: {result.Description}");
    Console.WriteLine("Health Data:");
    foreach (var kvp in result.Data)
    {
        Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
    }
}
Console.WriteLine("\n✓ Health check demonstrated\n");

// Sample 6: MetricsAggregator - Aggregated statistics
Console.WriteLine("6. MetricsAggregator - Statistical analysis");
var aggregatedMetricsList = new List<AggregatedMetrics>();

await using (var aggregator = new MetricsAggregator(TimeSpan.FromSeconds(2)))
{
    // Subscribe to aggregation events
    aggregator.OnAggregation += metrics =>
    {
        aggregatedMetricsList.AddRange(metrics);
    };

    await Task.Delay(100); // Let aggregator initialize

    var workload = Enumerable.Range(1, 60);
    await workload.SelectParallelAsync(
        async (item, ct) =>
        {
            await Task.Delay(Random.Shared.Next(5, 30), ct);
            if (item % 12 == 0) throw new InvalidOperationException("Error");
            return item;
        },
        new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 12,
            MaxRetries = 1,
            IsTransient = ex => ex is InvalidOperationException,
            ErrorMode = ErrorMode.CollectAndContinue
        });

    await Task.Delay(2500); // Let metrics aggregate
}

// Display aggregated statistics
Console.WriteLine("Aggregated Metrics:");
foreach (var metric in aggregatedMetricsList.GroupBy(m => m.Name).Select(g => g.Last()))
{
    Console.WriteLine($"  {metric.DisplayName}:");
    Console.WriteLine($"    Min: {metric.Min:F2} {metric.DisplayUnits}");
    Console.WriteLine($"    Max: {metric.Max:F2} {metric.DisplayUnits}");
    Console.WriteLine($"    Avg: {metric.Average:F2} {metric.DisplayUnits}");
    Console.WriteLine($"    Current: {metric.Current:F2} {metric.DisplayUnits}");
    Console.WriteLine($"    Samples: {metric.SampleCount}");
}
Console.WriteLine("\n✓ Metrics aggregation demonstrated\n");

Console.WriteLine("=== All diagnostics samples completed successfully ===");
Console.WriteLine("\nGenerated files:");
Console.WriteLine($"  - {metricsFile}");
Console.WriteLine($"  - {jsonFile}");
