using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Rivulet.Core;

namespace Rivulet.Diagnostics.Tests;

public class RivuletHealthCheckTests
{
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

        await Task.Delay(1500);

        var context = new HealthCheckContext();
        var result = await healthCheck.CheckHealthAsync(context);

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data.Should().ContainKey("items_started");
        result.Data.Should().ContainKey("items_completed");
    }

    [Fact]
    public async Task HealthCheck_ShouldReturnDegraded_WhenErrorRateExceedsThreshold()
    {
        using var exporter = new PrometheusExporter();
        var healthCheck = new RivuletHealthCheck(exporter, new RivuletHealthCheckOptions
        {
            ErrorRateThreshold = 0.01,
            FailureCountThreshold = 1000
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

        await Task.Delay(1500);

        var context = new HealthCheckContext();
        var result = await healthCheck.CheckHealthAsync(context);

        result.Status.Should().BeOneOf(HealthStatus.Degraded, HealthStatus.Unhealthy);
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

        await Task.Delay(1500);

        var context = new HealthCheckContext();
        var result = await healthCheck.CheckHealthAsync(context);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("Failure count");
    }
}
