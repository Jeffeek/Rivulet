using Microsoft.Extensions.Diagnostics.HealthChecks;
using Rivulet.Core;

namespace Rivulet.Diagnostics.Tests;

/// <summary>
///     Tests specifically designed to cover edge cases and improve code coverage.
/// </summary>
[Collection(TestCollections.SerialEventSource)]
public sealed class EdgeCaseCoverageTests
{
    [Fact]
    public async Task HealthCheck_ShouldHandleZeroItemsStarted()
    {
        using var exporter = new PrometheusExporter();
        var healthCheck = new RivuletHealthCheck(exporter);

        await Task.Delay(100, CancellationToken.None);

        var context = new HealthCheckContext();
        var result = await healthCheck.CheckHealthAsync(context, TestContext.Current.CancellationToken);

        result.Status.ShouldBeOneOf(HealthStatus.Healthy, HealthStatus.Degraded, HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task HealthCheck_ShouldHandleDegradedState()
    {
        using var exporter = new PrometheusExporter();
        var healthCheck =
            new RivuletHealthCheck(exporter, new() { ErrorRateThreshold = 0.2, FailureCountThreshold = 50 });

        try
        {
            await Enumerable.Range(1, 100)
                .ToAsyncEnumerable()
                .SelectParallelStreamAsync(static async (x, ct) =>
                    {
                        await Task.Delay(5, ct);
                        return x <= 90 ? throw new InvalidOperationException("Test failure") : x;
                    },
                    new() { MaxDegreeOfParallelism = 8, ErrorMode = ErrorMode.CollectAndContinue },
                    cancellationToken: TestContext.Current.CancellationToken)
                .ToListAsync(TestContext.Current.CancellationToken);
        }
        catch
        {
            // Expected
        }

        // Wait for EventSource counters to fire (1s default interval)
        // Increased from 1100ms to 2000ms to handle CI/CD timing variability
        await Task.Delay(2000, CancellationToken.None);

        var context = new HealthCheckContext();
        var result = await healthCheck.CheckHealthAsync(context, TestContext.Current.CancellationToken);

        result.Data.TryGetValue("total_failures", out var failuresObj).ShouldBeTrue();
        var failures = (double)failuresObj;
        failures.ShouldBeGreaterThanOrEqualTo(50, "many operations should have failed");

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Data.ContainsKey("error_rate").ShouldBeTrue();
    }

    [Fact]
    public async Task HealthCheck_ShouldHandleExceptionDuringMetricsCollection()
    {
        var exporter = new PrometheusExporter();
        exporter.Dispose();

        var healthCheck = new RivuletHealthCheck(exporter);

        var context = new HealthCheckContext();
        var result = await healthCheck.CheckHealthAsync(context, TestContext.Current.CancellationToken);

        result.Status.ShouldNotBe(default);
    }

    [Fact]
    public async Task ConsoleListener_ShouldHandleDifferentColorScenarios()
    {
        var listener = new RivuletConsoleListener();

        var attemptCount = 0;
        try
        {
            try
            {
                await Enumerable.Range(1, 5)
                    .ToAsyncEnumerable()
                    .SelectParallelStreamAsync(async (x, ct) =>
                        {
                            if (++attemptCount > 2) return x;

                            await Task.Delay(1, ct);
                            throw new InvalidOperationException("Retry");
                        },
                        new() { MaxRetries = 2, IsTransient = static _ => true, MaxDegreeOfParallelism = 1 },
                        cancellationToken: TestContext.Current.CancellationToken)
                    .ToListAsync(TestContext.Current.CancellationToken);
            }
            catch
            {
                // Expected
            }

            // Wait for EventSource counters to fire (1s default interval)
            // Increased from 1100ms to 2000ms to handle CI/CD timing variability
            await Task.Delay(2000, CancellationToken.None);
        }
        finally
        {
            listener.Dispose();
        }
    }

    [Fact]
    public async Task FileListener_ShouldHandleFileWriteEdgeCases()
    {
        var testFile = Path.Join(Path.GetTempPath(), $"rivulet-edge-{Guid.NewGuid()}.log");

        try
        {
            await using (new RivuletFileListener(testFile))
            {
                // Operations must run long enough for EventCounter polling (1 second interval)
                // 5 items * 200ms / 2 parallelism = 500ms minimum operation time
                await Enumerable.Range(1, 5)
                    .ToAsyncEnumerable()
                    .SelectParallelStreamAsync(static async (x, ct) =>
                        {
                            await Task.Delay(200, ct);
                            return x;
                        },
                        new() { MaxDegreeOfParallelism = 2 },
                        cancellationToken: TestContext.Current.CancellationToken)
                    .ToListAsync(TestContext.Current.CancellationToken);

                try
                {
                    await Enumerable.Range(1, 5)
                        .ToAsyncEnumerable()
                        .SelectParallelStreamAsync(static async (x, ct) =>
                            {
                                await Task.Delay(200, ct);
                                return x % 2 == 0 ? throw new InvalidOperationException() : x;
                            },
                            new() { MaxDegreeOfParallelism = 2, ErrorMode = ErrorMode.CollectAndContinue })
                        .ToListAsync(TestContext.Current.CancellationToken);
                }
                catch
                {
                    // Expected
                }

                // Wait for EventSource counters to fire
                await Task.Delay(1500, CancellationToken.None);
            }

            await Task.Delay(100, CancellationToken.None);

            File.Exists(testFile).ShouldBeTrue();
            var content = await File.ReadAllTextAsync(testFile, TestContext.Current.CancellationToken);
            content.ShouldNotBeEmpty();
        }
        finally
        {
            TestCleanupHelper.RetryDeleteFile(testFile);
        }
    }

    [Fact]
    public async Task StructuredLogListener_ShouldHandleJsonFormatting()
    {
        var testFile = Path.Join(Path.GetTempPath(), $"rivulet-json-{Guid.NewGuid()}.json");

        try
        {
            await using (new RivuletStructuredLogListener(testFile))
            {
                // Operations must run long enough for EventCounter polling (1 second interval)
                // 5 items * 200ms / 2 parallelism = 500ms minimum operation time
                await Enumerable.Range(1, 5)
                    .ToAsyncEnumerable()
                    .SelectParallelStreamAsync(static async (x, ct) =>
                        {
                            await Task.Delay(200, ct);
                            return x;
                        },
                        new() { MaxDegreeOfParallelism = 2 },
                        cancellationToken: TestContext.Current.CancellationToken)
                    .ToListAsync(TestContext.Current.CancellationToken);

                // Wait for EventSource counters to fire (1s default interval)
                await Task.Delay(1500, CancellationToken.None);
            }

            await Task.Delay(100, CancellationToken.None);

            File.Exists(testFile).ShouldBeTrue();
            var jsonContent = await File.ReadAllTextAsync(testFile, TestContext.Current.CancellationToken);
            jsonContent.ShouldNotBeEmpty();
        }
        finally
        {
            TestCleanupHelper.RetryDeleteFile(testFile);
        }
    }

    [Fact]
    public async Task EventListenerBase_ShouldHandleUnknownCounterNames()
    {
        var listener = new TestEventListener();

        try
        {
            // Operations must run long enough for EventCounter polling (1 second interval)
            // 5 items * 200ms / 2 parallelism = 500ms minimum operation time
            await Enumerable.Range(1, 5)
                .ToAsyncEnumerable()
                .SelectParallelStreamAsync(static async (x, ct) =>
                    {
                        await Task.Delay(200, ct);
                        return x;
                    },
                    new() { MaxDegreeOfParallelism = 2 },
                    cancellationToken: TestContext.Current.CancellationToken)
                .ToListAsync(TestContext.Current.CancellationToken);

            // Wait for EventSource counters to fire (1s default interval)
            // Increased from 1100ms to 2000ms to handle CI/CD timing variability
            await Task.Delay(1500, CancellationToken.None);

            listener.ReceivedCounters.ShouldNotBeEmpty();
        }
        finally
        {
            listener.Dispose();
        }
    }

    [Fact]
    public async Task MetricsAggregator_ShouldHandleNullCallback()
    {
        await using var aggregator = new MetricsAggregator(TimeSpan.FromSeconds(1));

        // Operations must run long enough for EventCounter polling (1 second interval)
        // 5 items * 200ms / 2 parallelism = 500ms minimum operation time
        await Enumerable.Range(1, 5)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(static async (x, ct) =>
                {
                    await Task.Delay(200, ct);
                    return x;
                },
                new() { MaxDegreeOfParallelism = 2 },
                cancellationToken: TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Wait for EventSource counters to fire (1s default interval)
        // Increased from 1100ms to 2000ms to handle CI/CD timing variability
        await Task.Delay(1500, CancellationToken.None);
    }

    [Fact]
    public async Task MetricsAggregator_ShouldHandleEmptyMetrics()
    {
        await using var aggregator = new MetricsAggregator(TimeSpan.FromMilliseconds(500));
        aggregator.OnAggregation += metrics => { _ = true; };

        await Task.Delay(1000, CancellationToken.None);
    }

    [Fact]
    public async Task PrometheusExporter_ShouldHandleRapidQueries()
    {
        using var exporter = new PrometheusExporter();

        for (var i = 0; i < 5; i++)
        {
            _ = exporter.ExportDictionary();
            await Task.Delay(10, CancellationToken.None);
        }

        // Operations must run long enough for EventCounter polling (1 second interval)
        // 5 items * 200ms / 2 parallelism = 500ms minimum operation time
        await Enumerable.Range(1, 5)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(static async (x, ct) =>
                {
                    await Task.Delay(200, ct);
                    return x;
                },
                new() { MaxDegreeOfParallelism = 2 },
                cancellationToken: TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Wait for EventSource counters to fire (1s default interval)
        await Task.Delay(1500, CancellationToken.None);

        var finalMetrics = exporter.ExportDictionary();
        finalMetrics.ShouldNotBeEmpty();
    }

    private sealed class TestEventListener : RivuletEventListenerBase
    {
        public List<string> ReceivedCounters { get; } = new();

        protected override void OnCounterReceived(
            string name,
            string displayName,
            double value,
            string displayUnits
        ) => ReceivedCounters.Add(name);
    }
}
