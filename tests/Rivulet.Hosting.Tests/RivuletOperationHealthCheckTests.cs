using Microsoft.Extensions.Diagnostics.HealthChecks;
using Rivulet.Hosting.HealthChecks;

namespace Rivulet.Hosting.Tests;

public class RivuletOperationHealthCheckTests
{
    [Fact]
    public async Task Constructor_WithNullOptions_ShouldUseDefaults()
    {
        var healthCheck = new RivuletOperationHealthCheck();

        var result = await healthCheck.CheckHealthAsync(new());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Constructor_WithCustomOptions_ShouldUseProvidedOptions()
    {
        var options = new RivuletOperationHealthCheckOptions
        {
            StalledThreshold = TimeSpan.FromSeconds(1),
            UnhealthyFailureThreshold = 2
        };

        var healthCheck = new RivuletOperationHealthCheck(options);

        // Wait for stalled threshold to pass
        await Task.Delay(1100);

        var result = await healthCheck.CheckHealthAsync(new());

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("No successful operations");
    }

    [Fact]
    public async Task RecordSuccess_ShouldResetConsecutiveFailures()
    {
        var healthCheck = new RivuletOperationHealthCheck();

        // Record some failures
        healthCheck.RecordFailure();
        healthCheck.RecordFailure();
        healthCheck.RecordFailure();

        // Record success
        healthCheck.RecordSuccess();

        var result = await healthCheck.CheckHealthAsync(new());

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data["consecutive_failures"].Should().Be(0);
    }

    [Fact]
    public async Task RecordFailure_ShouldIncrementConsecutiveFailures()
    {
        var healthCheck = new RivuletOperationHealthCheck();

        healthCheck.RecordFailure();
        healthCheck.RecordFailure();
        healthCheck.RecordFailure();

        var result = await healthCheck.CheckHealthAsync(new());

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data["consecutive_failures"].Should().Be(3);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenHealthy_ShouldReturnHealthyStatus()
    {
        var healthCheck = new RivuletOperationHealthCheck();

        healthCheck.RecordSuccess();

        var result = await healthCheck.CheckHealthAsync(new());

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("Operation healthy");
        result.Data.Should().ContainKey("consecutive_failures");
        result.Data.Should().ContainKey("time_since_last_success");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenStalled_ShouldReturnDegradedStatus()
    {
        var options = new RivuletOperationHealthCheckOptions
        {
            StalledThreshold = TimeSpan.FromMilliseconds(100)
        };

        var healthCheck = new RivuletOperationHealthCheck(options);

        // Wait for stalled threshold
        await Task.Delay(150);

        var result = await healthCheck.CheckHealthAsync(new());

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("No successful operations");
        result.Data["time_since_last_success"].Should().BeOfType<TimeSpan>();
        result.Data["consecutive_failures"].Should().Be(0);
    }

    [Fact]
    public async Task CheckHealthAsync_WithTooManyFailures_ShouldReturnUnhealthyStatus()
    {
        var options = new RivuletOperationHealthCheckOptions
        {
            UnhealthyFailureThreshold = 5
        };

        var healthCheck = new RivuletOperationHealthCheck(options);

        // Record enough failures to exceed threshold
        for (var i = 0; i < 5; i++)
        {
            healthCheck.RecordFailure();
        }

        var result = await healthCheck.CheckHealthAsync(new());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("Operation has failed 5 consecutive times");
        result.Data["consecutive_failures"].Should().Be(5);
        result.Data.Should().ContainKey("time_since_last_success");
    }

    [Fact]
    public async Task CheckHealthAsync_WithJustBelowThreshold_ShouldStayHealthy()
    {
        var options = new RivuletOperationHealthCheckOptions
        {
            UnhealthyFailureThreshold = 10
        };

        var healthCheck = new RivuletOperationHealthCheck(options);

        // Record just below threshold
        for (var i = 0; i < 9; i++)
        {
            healthCheck.RecordFailure();
        }

        var result = await healthCheck.CheckHealthAsync(new());

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data["consecutive_failures"].Should().Be(9);
    }

    [Fact]
    public async Task CheckHealthAsync_WithExactThreshold_ShouldBeUnhealthy()
    {
        var options = new RivuletOperationHealthCheckOptions
        {
            UnhealthyFailureThreshold = 3
        };

        var healthCheck = new RivuletOperationHealthCheck(options);

        for (var i = 0; i < 3; i++)
        {
            healthCheck.RecordFailure();
        }

        var result = await healthCheck.CheckHealthAsync(new());

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WithBothStalledAndFailures_ShouldPrioritizeUnhealthy()
    {
        var options = new RivuletOperationHealthCheckOptions
        {
            StalledThreshold = TimeSpan.FromMilliseconds(10),
            UnhealthyFailureThreshold = 3
        };

        var healthCheck = new RivuletOperationHealthCheck(options);

        // Record failures
        for (var i = 0; i < 3; i++)
        {
            healthCheck.RecordFailure();
        }

        // Wait for stalled threshold
        await Task.Delay(50);

        var result = await healthCheck.CheckHealthAsync(new());

        // Unhealthy due to failures takes precedence
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("failed 3 consecutive times");
    }

    [Fact]
    public async Task RecordSuccess_ShouldUpdateLastSuccessTime()
    {
        var options = new RivuletOperationHealthCheckOptions
        {
            StalledThreshold = TimeSpan.FromMilliseconds(100)
        };

        var healthCheck = new RivuletOperationHealthCheck(options);

        // Wait then record success
        await Task.Delay(50);
        healthCheck.RecordSuccess();

        var result = await healthCheck.CheckHealthAsync(new());

        result.Status.Should().Be(HealthStatus.Healthy);
        var timeSinceSuccess = (TimeSpan)result.Data["time_since_last_success"];
        timeSinceSuccess.Should().BeLessThan(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task RecordFailure_MultipleTimes_ShouldAccumulateCorrectly()
    {
        var healthCheck = new RivuletOperationHealthCheck();

        healthCheck.RecordFailure();
        healthCheck.RecordFailure();
        var result1 = await healthCheck.CheckHealthAsync(new());
        result1.Data["consecutive_failures"].Should().Be(2);

        healthCheck.RecordFailure();
        var result2 = await healthCheck.CheckHealthAsync(new());
        result2.Data["consecutive_failures"].Should().Be(3);

        healthCheck.RecordSuccess();
        var result3 = await healthCheck.CheckHealthAsync(new());
        result3.Data["consecutive_failures"].Should().Be(0);
    }

    [Fact]
    public async Task CheckHealthAsync_WithCancellationToken_ShouldComplete()
    {
        var healthCheck = new RivuletOperationHealthCheck();

        using var cts = new CancellationTokenSource();
        var result = await healthCheck.CheckHealthAsync(new(), cts.Token);

        result.Should().NotBeNull();
        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public void RivuletOperationHealthCheckOptions_ShouldHaveCorrectDefaults()
    {
        var options = new RivuletOperationHealthCheckOptions();

        options.StalledThreshold.Should().Be(TimeSpan.FromMinutes(5));
        options.UnhealthyFailureThreshold.Should().Be(10);
    }

    [Fact]
    public void RivuletOperationHealthCheckOptions_ShouldAllowCustomValues()
    {
        var options = new RivuletOperationHealthCheckOptions
        {
            StalledThreshold = TimeSpan.FromSeconds(30),
            UnhealthyFailureThreshold = 5
        };

        options.StalledThreshold.Should().Be(TimeSpan.FromSeconds(30));
        options.UnhealthyFailureThreshold.Should().Be(5);
    }

    [Fact]
    public async Task RecordSuccess_ConcurrentCalls_ShouldBeThreadSafe()
    {
        var healthCheck = new RivuletOperationHealthCheck();

        // Record failures
        for (var i = 0; i < 5; i++)
        {
            healthCheck.RecordFailure();
        }

        // Concurrently record success
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => healthCheck.RecordSuccess()))
            .ToArray();

        await Task.WhenAll(tasks);

        var result = await healthCheck.CheckHealthAsync(new());

        result.Data["consecutive_failures"].Should().Be(0);
    }

    [Fact]
    public async Task RecordFailure_ConcurrentCalls_ShouldBeThreadSafe()
    {
        var healthCheck = new RivuletOperationHealthCheck();

        // Concurrently record failures
        var tasks = Enumerable.Range(0, 100)
            .Select(_ => Task.Run(() => healthCheck.RecordFailure()))
            .ToArray();

        await Task.WhenAll(tasks);

        var result = await healthCheck.CheckHealthAsync(new());

        result.Data["consecutive_failures"].Should().Be(100);
    }

    [Fact]
    public async Task CheckHealthAsync_DataDictionary_ShouldContainAllExpectedKeys()
    {
        var healthCheck = new RivuletOperationHealthCheck();

        healthCheck.RecordFailure();
        healthCheck.RecordFailure();

        var result = await healthCheck.CheckHealthAsync(new());

        result.Data.Should().ContainKey("consecutive_failures");
        result.Data.Should().ContainKey("time_since_last_success");
        result.Data.Count.Should().Be(2);
    }
}
