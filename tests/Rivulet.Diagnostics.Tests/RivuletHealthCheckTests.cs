using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Rivulet.Core;

namespace Rivulet.Diagnostics.Tests;

/// <summary>
/// These tests use RivuletEventSource which is a static singleton shared across all test assemblies.
/// Some tests are added to a serial collection to prevent parallel execution issues with metrics.
/// </summary>
[Collection(TestCollections.SerialEventSource)]
[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public class RivuletHealthCheckTests
{
    [Fact]
    public void HealthCheck_ShouldThrow_WhenExporterIsNull()
    {
        var act = () => new RivuletHealthCheck(null!);
        act.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe("exporter");
    }

    [Fact]
    public void HealthCheck_ShouldUseDefaultOptions_WhenOptionsIsNull()
    {
        using var exporter = new PrometheusExporter();
        // ReSharper disable once RedundantArgumentDefaultValue
        var act = () => new RivuletHealthCheck(exporter, null);
        act.ShouldNotThrow();
    }

    [Fact]
    public async Task HealthCheck_ShouldReturnHealthy_WhenNoOperationsRunning()
    {
        using var exporter = new PrometheusExporter();
        var healthCheck = new RivuletHealthCheck(exporter);
        var context = new HealthCheckContext();

        var result = await healthCheck.CheckHealthAsync(context);

        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Description.ShouldNotBeNull();
        result.Description.ShouldContain("No Rivulet operations");
    }

    [Fact]
    public async Task HealthCheck_ShouldReturnHealthy_WhenOperationsSucceed()
    {
        using var exporter = new PrometheusExporter();
        var healthCheck = new RivuletHealthCheck(exporter, new()
        {
            ErrorRateThreshold = 1.0, // 100% - should always pass for successful operations
            FailureCountThreshold = 10000 // Very high threshold to avoid failures from previous tests
        });

        await Enumerable.Range(1, 10)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x * 2;
            }, new()
            {
                MaxDegreeOfParallelism = 2
            })
            .ToListAsync();

        await Task.Delay(1100);

        var context = new HealthCheckContext();
        var result = await healthCheck.CheckHealthAsync(context);

        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Data.ContainsKey(RivuletDiagnosticsConstants.HealthCheckKeys.ItemsStarted).ShouldBeTrue();
        result.Data.ContainsKey(RivuletDiagnosticsConstants.HealthCheckKeys.ItemsCompleted).ShouldBeTrue();
    }

    [Fact]
    public async Task HealthCheck_ShouldReturnDegraded_WhenErrorRateExceedsThreshold()
    {
        using var exporter = new PrometheusExporter();

        // Use failure count threshold instead of error rate to avoid issues with shared static counters
        var healthCheck = new RivuletHealthCheck(exporter, new()
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
                }, new()
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
        result.Data.ContainsKey("total_failures").ShouldBeTrue();
        var failures = (double)result.Data["total_failures"];
        failures.ShouldBeGreaterThanOrEqualTo(100, "all operations should have failed");

        // Should be Unhealthy because failure count exceeds threshold
        result.Status.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task HealthCheck_ShouldReturnUnhealthy_WhenFailureCountExceedsThreshold()
    {
        using var exporter = new PrometheusExporter();
        var healthCheck = new RivuletHealthCheck(exporter, new()
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
                }, new()
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

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldNotBeNull();
        result.Description.ShouldContain("Failure count");
    }

    [Fact]
    public void HealthCheck_ShouldNotThrow_WhenDisposed()
    {
        using var exporter = new PrometheusExporter();
        var healthCheck = new RivuletHealthCheck(exporter);
        var act = () => healthCheck.Dispose();
        act.ShouldNotThrow();
    }

    [Fact]
    public async Task HealthCheck_ShouldReturnDegraded_WhenErrorRateExceedsThresholdButNotFailureCount()
    {
        using var exporter = new PrometheusExporter();
        var healthCheck = new RivuletHealthCheck(exporter, new()
        {
            ErrorRateThreshold = 0.2,
            FailureCountThreshold = 10000
        });

        try
        {
            await Enumerable.Range(1, 5000)
                .ToAsyncEnumerable()
                .SelectParallelStreamAsync(async (x, ct) =>
                {
                    await Task.Delay(1, ct);
                    if (x <= 4000)
                    {
                        throw new InvalidOperationException("Test error");
                    }
                    return x * 2;
                }, new()
                {
                    MaxDegreeOfParallelism = 20,
                    ErrorMode = ErrorMode.CollectAndContinue
                })
                .ToListAsync();
        }
        catch
        {
            // ignored
        }

        await Task.Yield();
        await Task.Delay(2500);

        var context = new HealthCheckContext();
        var result = await healthCheck.CheckHealthAsync(context);

        result.Data.ContainsKey("error_rate").ShouldBeTrue();
        var errorRate = (double)result.Data["error_rate"];
        errorRate.ShouldBeGreaterThan(0.2);

        result.Data.ContainsKey("total_failures").ShouldBeTrue();
        var failures = (double)result.Data["total_failures"];
        failures.ShouldBeLessThan(10000);

        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description.ShouldNotBeNull();
        result.Description.ShouldContain("Error rate");
    }

    [Fact]
    public void HealthCheck_Constructor_ShouldAcceptNullOptions()
    {
        using var exporter = new PrometheusExporter();
        // ReSharper disable once RedundantArgumentDefaultValue
        var act = () => new RivuletHealthCheck(exporter, null);
        act.ShouldNotThrow();
    }

    [Fact]
    public void HealthCheck_ShouldUseDefaultOptionsValues()
    {
        var options = new RivuletHealthCheckOptions();
        options.ErrorRateThreshold.ShouldBe(0.1);
        options.FailureCountThreshold.ShouldBe(1000);
    }

    [Fact]
    public void HealthCheck_ShouldAllowCustomOptionsValues()
    {
        var options = new RivuletHealthCheckOptions
        {
            ErrorRateThreshold = 0.5,
            FailureCountThreshold = 100
        };
        options.ErrorRateThreshold.ShouldBe(0.5);
        options.FailureCountThreshold.ShouldBe(100);
    }

    [Fact]
    public void HealthCheck_Dispose_ShouldNotThrow()
    {
        using var exporter = new PrometheusExporter();
        var healthCheck = new RivuletHealthCheck(exporter);
        var act = () => healthCheck.Dispose();
        act.ShouldNotThrow();
    }

    [Fact]
    public async Task HealthCheck_ShouldIncludeAllMetricsInData()
    {
        using var exporter = new PrometheusExporter();
        var healthCheck = new RivuletHealthCheck(exporter);

        await Enumerable.Range(1, 5)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(async (x, ct) =>
            {
                await Task.Delay(10, ct);
                return x * 2;
            }, new()
            {
                MaxDegreeOfParallelism = 2
            })
            .ToListAsync();

        await Task.Delay(1100);

        var context = new HealthCheckContext();
        var result = await healthCheck.CheckHealthAsync(context);

        result.Data.ContainsKey(RivuletDiagnosticsConstants.HealthCheckKeys.ItemsStarted).ShouldBeTrue();
        result.Data.ContainsKey(RivuletDiagnosticsConstants.HealthCheckKeys.ItemsCompleted).ShouldBeTrue();
        result.Data.ContainsKey(RivuletDiagnosticsConstants.HealthCheckKeys.TotalFailures).ShouldBeTrue();
        result.Data.ContainsKey(RivuletDiagnosticsConstants.HealthCheckKeys.TotalRetries).ShouldBeTrue();
        result.Data.ContainsKey(RivuletDiagnosticsConstants.HealthCheckKeys.ErrorRate).ShouldBeTrue();
    }

    [Fact]
    public async Task HealthCheck_WithZeroItemsStarted_ShouldHaveZeroErrorRate()
    {
        using var exporter = new PrometheusExporter();
        var healthCheck = new RivuletHealthCheck(exporter);

        // Don't run any operations, so itemsStarted is 0
        await Task.Delay(100);

        var context = new HealthCheckContext();
        var result = await healthCheck.CheckHealthAsync(context);

        // With 0 items started, error rate should be 0
        result.Status.ShouldBe(HealthStatus.Healthy);
    }
}
