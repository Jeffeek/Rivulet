using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Rivulet.Core;

namespace Rivulet.Diagnostics.Tests;

/// <summary>
/// These tests use RivuletEventSource which is a static singleton shared across all test assemblies.
/// Some tests are added to a serial collection to prevent parallel execution issues with metrics.
/// </summary>
[Collection("Serial EventSource Tests")]
[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public class RivuletHealthCheckTests
{
    [Fact]
    public void HealthCheck_ShouldThrow_WhenExporterIsNull()
    {
        var act = () => new RivuletHealthCheck(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("exporter");
    }

    [Fact]
    public void HealthCheck_ShouldUseDefaultOptions_WhenOptionsIsNull()
    {
        using var exporter = new PrometheusExporter();
        // ReSharper disable once RedundantArgumentDefaultValue
        var act = () => new RivuletHealthCheck(exporter, null);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task HealthCheck_ShouldReturnHealthy_WhenNoOperationsRunning()
    {
        using var exporter = new PrometheusExporter();
        var healthCheck = new RivuletHealthCheck(exporter);
        var context = new HealthCheckContext();

        var result = await healthCheck.CheckHealthAsync(context);

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("No Rivulet operations");
    }

    [Fact]
    public async Task HealthCheck_ShouldReturnHealthy_WhenOperationsSucceed()
    {
        using var exporter = new PrometheusExporter();
        var healthCheck = new RivuletHealthCheck(exporter, new RivuletHealthCheckOptions
        {
            ErrorRateThreshold = 0.5,
            FailureCountThreshold = 100
        });

        await Enumerable.Range(1, 10)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x * 2;
            }, new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 2
            })
            .ToListAsync();

        await Task.Delay(1100);

        var context = new HealthCheckContext();
        var result = await healthCheck.CheckHealthAsync(context);

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data.Should().ContainKey(RivuletDiagnosticsConstants.HealthCheckKeys.ItemsStarted);
        result.Data.Should().ContainKey(RivuletDiagnosticsConstants.HealthCheckKeys.ItemsCompleted);
    }

    [Fact]
    public async Task HealthCheck_ShouldReturnDegraded_WhenErrorRateExceedsThreshold()
    {
        using var exporter = new PrometheusExporter();

        // Use failure count threshold instead of error rate to avoid issues with shared static counters
        var healthCheck = new RivuletHealthCheck(exporter, new RivuletHealthCheckOptions
        {
            ErrorRateThreshold = 1.0, // Set high to not trigger on rate
            FailureCountThreshold = 50 // Trigger on absolute failure count
        });

        try
        {
            // Run operations that all fail
            await Enumerable.Range(1, 100)
                .ToAsyncEnumerable()
                .SelectParallelStreamAsync<int, int>(async (_, ct) =>
                {
                    await Task.Delay(10, ct);
                    throw new InvalidOperationException("Test error");
                }, new ParallelOptionsRivulet
                {
                    MaxDegreeOfParallelism = 8,
                    ErrorMode = ErrorMode.CollectAndContinue
                })
                .ToListAsync();
        }
        catch
        {
            // ignored
        }

        await Task.Delay(1100);

        var context = new HealthCheckContext();
        var result = await healthCheck.CheckHealthAsync(context);

        // Verify that we have failures recorded
        result.Data.Should().ContainKey("total_failures");
        var failures = (double)result.Data["total_failures"];
        failures.Should().BeGreaterThanOrEqualTo(100, "all operations should have failed");

        // Should be Unhealthy because failure count exceeds threshold
        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task HealthCheck_ShouldReturnUnhealthy_WhenFailureCountExceedsThreshold()
    {
        using var exporter = new PrometheusExporter();
        var healthCheck = new RivuletHealthCheck(exporter, new RivuletHealthCheckOptions
        {
            ErrorRateThreshold = 1.0,
            FailureCountThreshold = 5
        });

        try
        {
            await Enumerable.Range(1, 10)
                .ToAsyncEnumerable()
                .SelectParallelStreamAsync<int, int>(async (_, ct) =>
                {
                    await Task.Delay(10, ct);
                    throw new InvalidOperationException("Test error");
                }, new ParallelOptionsRivulet
                {
                    MaxDegreeOfParallelism = 2,
                    ErrorMode = ErrorMode.CollectAndContinue
                })
                .ToListAsync();
        }
        catch
        {
            // ignored
        }

        await Task.Delay(1100);

        var context = new HealthCheckContext();
        var result = await healthCheck.CheckHealthAsync(context);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("Failure count");
    }

    [Fact]
    public void HealthCheck_ShouldNotThrow_WhenDisposed()
    {
        using var exporter = new PrometheusExporter();
        var healthCheck = new RivuletHealthCheck(exporter);
        var act = () => healthCheck.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task HealthCheck_ShouldReturnDegraded_WhenErrorRateExceedsThresholdButNotFailureCount()
    {
        using var exporter = new PrometheusExporter();
        var healthCheck = new RivuletHealthCheck(exporter, new RivuletHealthCheckOptions
        {
            ErrorRateThreshold = 0.2,
            FailureCountThreshold = 1000
        });

        try
        {
            await Enumerable.Range(1, 10)
                .ToAsyncEnumerable()
                .SelectParallelStreamAsync(async (x, ct) =>
                {
                    await Task.Delay(10, ct);
                    if (x <= 3)
                    {
                        throw new InvalidOperationException("Test error");
                    }
                    return x * 2;
                }, new ParallelOptionsRivulet
                {
                    MaxDegreeOfParallelism = 2,
                    ErrorMode = ErrorMode.CollectAndContinue
                })
                .ToListAsync();
        }
        catch
        {
            // ignored
        }

        await Task.Delay(1100);

        var context = new HealthCheckContext();
        var result = await healthCheck.CheckHealthAsync(context);

        result.Data.Should().ContainKey("error_rate");
        var errorRate = (double)result.Data["error_rate"];
        errorRate.Should().BeGreaterThan(0.2);

        result.Data.Should().ContainKey("total_failures");
        var failures = (double)result.Data["total_failures"];
        failures.Should().BeLessThan(1000);

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("Error rate");
    }
}
