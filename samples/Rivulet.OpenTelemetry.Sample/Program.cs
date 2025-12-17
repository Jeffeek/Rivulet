using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Rivulet.Core;
using Rivulet.Diagnostics.OpenTelemetry;

// ReSharper disable ArrangeObjectCreationWhenTypeNotEvident
// ReSharper disable ArgumentsStyleLiteral

Console.WriteLine("=== Rivulet.Diagnostics.OpenTelemetry Sample ===\n");

// Configure OpenTelemetry with both tracing and metrics
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("RivuletSample"))
    .AddSource(RivuletSharedConstants.RivuletCore)
    .AddConsoleExporter()
    .Build();

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("RivuletSample"))
    .AddMeter(RivuletSharedConstants.RivuletCore)
    .AddConsoleExporter()
    .Build();

// Create the metrics exporter
using var metricsExporter = new RivuletMetricsExporter();

Console.WriteLine("OpenTelemetry configured with Console exporters\n");

// Sample 1: Basic tracing with SelectParallelAsync
Console.WriteLine("1. Basic Distributed Tracing");
using (var activity = RivuletActivitySource.StartOperation("BasicProcessing", totalItems: 20))
{
    var numbers = Enumerable.Range(1, 20);

    var results = await numbers.SelectParallelAsync(static async (num, ct) =>
        {
            using var itemActivity = RivuletActivitySource.StartItemActivity("ProcessNumber", num);

            await Task.Delay(Random.Shared.Next(10, 50), ct);
            var result = num * num;

            itemActivity?.SetTag("input", num);
            itemActivity?.SetTag("output", result);

            return result;
        },
        new ParallelOptionsRivulet { MaxDegreeOfParallelism = 5 });

    RivuletActivitySource.RecordSuccess(activity, results.Count);
    Console.WriteLine($"✓ Processed {results.Count} items with full tracing\n");
}

// Sample 2: Tracing with retries
Console.WriteLine("2. Tracing with Retry Logic");
using (var activity = RivuletActivitySource.StartOperation("ProcessingWithRetries", totalItems: 15))
{
    var items = Enumerable.Range(1, 15);
    var attempt = 0;

    var results = await items.SelectParallelAsync(
        async (item, ct) =>
        {
            using var itemActivity = RivuletActivitySource.StartItemActivity("ProcessItem", item);

            await Task.Delay(20, ct);

            if (item % 5 != 0 || attempt++ >= 3) return item * 2;

            // Simulate transient errors
            var ex = new InvalidOperationException($"Transient error on item {item}");
            RivuletActivitySource.RecordRetry(itemActivity, attempt, ex);
            throw ex;
        },
        new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 4,
            MaxRetries = 3,
            IsTransient = static ex => ex is InvalidOperationException,
            ErrorMode = ErrorMode.CollectAndContinue
        });

    RivuletActivitySource.RecordSuccess(activity, results.Count);
    Console.WriteLine($"✓ Processed {results.Count} items with retry tracing\n");
}

// Sample 3: Tracing with error recording
Console.WriteLine("3. Error Tracking and Recording");
using (var activity = RivuletActivitySource.StartOperation("ProcessingWithErrors", totalItems: 10))
{
    var items = Enumerable.Range(1, 10);

    try
    {
        await items.SelectParallelAsync(static async (item, ct) =>
            {
                using var itemActivity = RivuletActivitySource.StartItemActivity("ProcessItem", item);

                await Task.Delay(15, ct);

                if (item != 5) return item;

                var ex = new InvalidOperationException("Permanent error");
                RivuletActivitySource.RecordError(itemActivity, ex, isTransient: false);
                throw ex;
            },
            new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 3,
                ErrorMode = ErrorMode.FailFast
            });
    }
    catch (InvalidOperationException ex)
    {
        RivuletActivitySource.RecordError(activity, ex);
        Console.WriteLine($"✓ Caught and traced error: {ex.Message}\n");
    }
}

// Sample 4: Custom span attributes and events
Console.WriteLine("4. Custom Span Attributes");
using (var activity = RivuletActivitySource.StartOperation("CustomTracking", totalItems: 12))
{
    activity?.SetTag("environment", "sample");
    activity?.SetTag("batch_id", Guid.NewGuid().ToString());

    var items = Enumerable.Range(1, 12);
    var results = await items.SelectParallelAsync(static async (item, ct) =>
        {
            using var itemActivity = RivuletActivitySource.StartItemActivity("ProcessItem", item);

            itemActivity?.SetTag("category", item % 2 == 0 ? "even" : "odd");
            itemActivity?.AddEvent(new ActivityEvent("ProcessingStarted"));

            await Task.Delay(25, ct);

            itemActivity?.AddEvent(new ActivityEvent("ProcessingCompleted"));

            return item * 3;
        },
        new ParallelOptionsRivulet { MaxDegreeOfParallelism = 4 });

    activity?.SetTag("results_count", results.Count);
    RivuletActivitySource.RecordSuccess(activity, results.Count);
    Console.WriteLine($"✓ Processed {results.Count} items with custom attributes\n");
}

// Sample 5: Simulated circuit breaker and concurrency events
Console.WriteLine("5. Circuit Breaker and Adaptive Concurrency Events");
using (var activity = RivuletActivitySource.StartOperation("AdaptiveProcessing", totalItems: 8))
{
    // Simulate circuit breaker state changes
    RivuletActivitySource.RecordCircuitBreakerStateChange(activity, "Closed");

    var items = Enumerable.Range(1, 8);
    await Task.Delay(100);

    // Simulate concurrency adjustment
    RivuletActivitySource.RecordConcurrencyChange(activity, oldConcurrency: 10, newConcurrency: 5);

    var results = await items.SelectParallelAsync(static async (item, ct) =>
        {
            await Task.Delay(30, ct);
            return item;
        },
        new ParallelOptionsRivulet { MaxDegreeOfParallelism = 5 });

    // Simulate another concurrency change
    RivuletActivitySource.RecordConcurrencyChange(activity, oldConcurrency: 5, newConcurrency: 8);
    RivuletActivitySource.RecordCircuitBreakerStateChange(activity, "Open");

    RivuletActivitySource.RecordSuccess(activity, results.Count);
    Console.WriteLine("✓ Recorded circuit breaker and concurrency events\n");
}

Console.WriteLine("=== All OpenTelemetry samples completed successfully ===");
Console.WriteLine("\nIn production, configure exporters for:");
Console.WriteLine("  - Jaeger: AddJaegerExporter()");
Console.WriteLine("  - Zipkin: AddZipkinExporter()");
Console.WriteLine("  - OTLP: AddOtlpExporter()");
Console.WriteLine("  - Azure Monitor: AddAzureMonitorTraceExporter()");
Console.WriteLine("  - Prometheus: AddPrometheusExporter() for metrics");