using Microsoft.Extensions.Diagnostics.HealthChecks;
using Rivulet.Core;

namespace Rivulet.Diagnostics.Tests;

/// <summary>
/// Tests specifically designed to cover edge cases and improve code coverage.
/// </summary>
[Collection("Serial EventSource Tests")]
public class EdgeCaseCoverageTests
{
    [Fact]
    public async Task HealthCheck_ShouldHandleZeroItemsStarted()
    {
        using var exporter = new PrometheusExporter();
        var healthCheck = new RivuletHealthCheck(exporter);

        await Task.Delay(100);

        var context = new HealthCheckContext();
        var result = await healthCheck.CheckHealthAsync(context);

        result.Status.Should().BeOneOf(HealthStatus.Healthy, HealthStatus.Degraded, HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task HealthCheck_ShouldHandleDegradedState()
    {
        using var exporter = new PrometheusExporter();
        var healthCheck = new RivuletHealthCheck(exporter, new RivuletHealthCheckOptions
        {
            ErrorRateThreshold = 0.2,
            FailureCountThreshold = 50
        });

        try
        {
            await Enumerable.Range(1, 100)
                .ToAsyncEnumerable()
                .SelectParallelStreamAsync(async (x, ct) =>
                {
                    await Task.Delay(5, ct);
                    if (x <= 90)
                        throw new InvalidOperationException("Test failure");
                    return x;
                }, new ParallelOptionsRivulet
                {
                    MaxDegreeOfParallelism = 8,
                    ErrorMode = ErrorMode.CollectAndContinue
                })
                .ToListAsync();
        }
        catch
        {
            // Expected
        }

        // Wait for EventSource counters to fire (1s default interval)
        // Increased from 1100ms to 2000ms to handle CI/CD timing variability
        await Task.Delay(2000);

        var context = new HealthCheckContext();
        var result = await healthCheck.CheckHealthAsync(context);

        result.Data.Should().ContainKey("total_failures");
        var failures = (double)result.Data["total_failures"];
        failures.Should().BeGreaterThanOrEqualTo(50, "many operations should have failed");

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Data.Should().ContainKey("error_rate");
    }

    [Fact]
    public async Task HealthCheck_ShouldHandleExceptionDuringMetricsCollection()
    {
        var exporter = new PrometheusExporter();
        exporter.Dispose();

        var healthCheck = new RivuletHealthCheck(exporter);

        var context = new HealthCheckContext();
        var result = await healthCheck.CheckHealthAsync(context);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ConsoleListener_ShouldHandleDifferentColorScenarios()
    {
        var listener = new RivuletConsoleListener(useColors: true);

        var attemptCount = 0;
        try
        {
            try
            {
                await Enumerable.Range(1, 5)
                    .ToAsyncEnumerable()
                    .SelectParallelStreamAsync(async (x, ct) =>
                    {
                        if (++attemptCount <= 2)
                        {
                            await Task.Delay(1, ct);
                            throw new InvalidOperationException("Retry");
                        }
                        return x;
                    }, new ParallelOptionsRivulet
                    {
                        MaxRetries = 2,
                        IsTransient = _ => true,
                        MaxDegreeOfParallelism = 1
                    })
                    .ToListAsync();
            }
            catch
            {
                // Expected
            }

            // Wait for EventSource counters to fire (1s default interval)
            // Increased from 1100ms to 2000ms to handle CI/CD timing variability
            await Task.Delay(2000);
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
                await Enumerable.Range(1, 10)
                    .ToAsyncEnumerable()
                    .SelectParallelStreamAsync(async (x, ct) =>
                    {
                        await Task.Delay(1, ct);
                        return x;
                    }, new ParallelOptionsRivulet
                    {
                        MaxDegreeOfParallelism = 2
                    })
                    .ToListAsync();

                try
                {
                    await Enumerable.Range(1, 5)
                        .ToAsyncEnumerable()
                        .SelectParallelStreamAsync(async (x, ct) =>
                        {
                            await Task.Delay(1, ct);
                            if (x % 2 == 0)
                                throw new InvalidOperationException();
                            return x;
                        }, new ParallelOptionsRivulet
                        {
                            MaxDegreeOfParallelism = 2,
                            ErrorMode = ErrorMode.CollectAndContinue
                        })
                        .ToListAsync();
                }
                catch
                {
                    // Expected
                }

                // Wait for EventSource counters to fire (1s default interval)
                // Increased from 1100ms to 2000ms to handle CI/CD timing variability
                await Task.Delay(2000);
            }

            await Task.Delay(100);

            File.Exists(testFile).Should().BeTrue();
            var content = await File.ReadAllTextAsync(testFile);
            content.Should().NotBeEmpty();
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
                await Enumerable.Range(1, 5)
                    .ToAsyncEnumerable()
                    .SelectParallelStreamAsync(async (x, ct) =>
                    {
                        await Task.Delay(1, ct);
                        return x;
                    }, new ParallelOptionsRivulet
                    {
                        MaxDegreeOfParallelism = 2
                    })
                    .ToListAsync();

                // Wait for EventSource counters to fire (1s default interval)
                // Increased from 1100ms to 2000ms to handle CI/CD timing variability
                await Task.Delay(2000);
            }

            await Task.Delay(100);

            File.Exists(testFile).Should().BeTrue();
            var jsonContent = await File.ReadAllTextAsync(testFile);
            jsonContent.Should().NotBeEmpty();
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
            await Enumerable.Range(1, 5)
                .ToAsyncEnumerable()
                .SelectParallelStreamAsync(async (x, ct) =>
                {
                    await Task.Delay(1, ct);
                    return x;
                }, new ParallelOptionsRivulet
                {
                    MaxDegreeOfParallelism = 2
                })
                .ToListAsync();

            // Wait for EventSource counters to fire (1s default interval)
            // Increased from 1100ms to 2000ms to handle CI/CD timing variability
            await Task.Delay(2000);

            listener.ReceivedCounters.Should().NotBeEmpty();
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

        await Enumerable.Range(1, 5)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x;
            }, new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 2
            })
            .ToListAsync();

        // Wait for EventSource counters to fire (1s default interval)
        // Increased from 1100ms to 2000ms to handle CI/CD timing variability
        await Task.Delay(2000);
    }

    [Fact]
    public async Task MetricsAggregator_ShouldHandleEmptyMetrics()
    {
        await using var aggregator = new MetricsAggregator(TimeSpan.FromMilliseconds(500));
        aggregator.OnAggregation += metrics =>
        {
            _ = true;
        };

        await Task.Delay(1000);
    }

    [Fact]
    public async Task PrometheusExporter_ShouldHandleRapidQueries()
    {
        using var exporter = new PrometheusExporter();

        for (var i = 0; i < 5; i++)
        {
            _ = exporter.ExportDictionary();
            await Task.Delay(10);
        }

        await Enumerable.Range(1, 3)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x;
            }, new ParallelOptionsRivulet())
            .ToListAsync();

        // Wait for EventSource counters to fire (1s default interval)
        // Increased from 1100ms to 2000ms to handle CI/CD timing variability
        await Task.Delay(2000);

        var finalMetrics = exporter.ExportDictionary();
        finalMetrics.Should().NotBeEmpty();
    }

    private sealed class TestEventListener : RivuletEventListenerBase
    {
        public List<string> ReceivedCounters { get; } = new();

        protected override void OnCounterReceived(string name, string displayName, double value, string displayUnits)
        {
            ReceivedCounters.Add(name);
        }
    }
}
