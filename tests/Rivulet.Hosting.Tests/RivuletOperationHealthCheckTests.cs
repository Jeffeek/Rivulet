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

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Constructor_WithCustomOptions_ShouldUseProvidedOptions()
    {
        var options = new RivuletOperationHealthCheckOptions { StalledThreshold = TimeSpan.FromSeconds(1), UnhealthyFailureThreshold = 2 };

        var healthCheck = new RivuletOperationHealthCheck(options);

        // Wait for stalled threshold to pass
        await Task.Delay(1100);

        var result = await healthCheck.CheckHealthAsync(new());

        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description.ShouldNotBeNull();
        result.Description.ShouldContain("No successful operations");
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

        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Data["consecutive_failures"].ShouldBe(0);
    }

    [Fact]
    public async Task RecordFailure_ShouldIncrementConsecutiveFailures()
    {
        var healthCheck = new RivuletOperationHealthCheck();

        healthCheck.RecordFailure();
        healthCheck.RecordFailure();
        healthCheck.RecordFailure();

        var result = await healthCheck.CheckHealthAsync(new());

        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Data["consecutive_failures"].ShouldBe(3);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenHealthy_ShouldReturnHealthyStatus()
    {
        var healthCheck = new RivuletOperationHealthCheck();

        healthCheck.RecordSuccess();

        var result = await healthCheck.CheckHealthAsync(new());

        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Description.ShouldNotBeNull();
        result.Description.ShouldContain("Operation healthy");
        result.Data.ContainsKey("consecutive_failures").ShouldBeTrue();
        result.Data.ContainsKey("time_since_last_success").ShouldBeTrue();
    }

    [Fact]
    public async Task CheckHealthAsync_WhenStalled_ShouldReturnDegradedStatus()
    {
        var options = new RivuletOperationHealthCheckOptions { StalledThreshold = TimeSpan.FromMilliseconds(100) };

        var healthCheck = new RivuletOperationHealthCheck(options);

        // Wait for stalled threshold
        await Task.Delay(150);

        var result = await healthCheck.CheckHealthAsync(new());

        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description.ShouldNotBeNull();
        result.Description.ShouldContain("No successful operations");
        result.Data["time_since_last_success"].ShouldBeOfType<TimeSpan>();
        result.Data["consecutive_failures"].ShouldBe(0);
    }

    [Fact]
    public async Task CheckHealthAsync_WithTooManyFailures_ShouldReturnUnhealthyStatus()
    {
        var options = new RivuletOperationHealthCheckOptions { UnhealthyFailureThreshold = 5 };

        var healthCheck = new RivuletOperationHealthCheck(options);

        // Record enough failures to exceed threshold
        for (var i = 0; i < 5; i++) healthCheck.RecordFailure();

        var result = await healthCheck.CheckHealthAsync(new());

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldNotBeNull();
        result.Description.ShouldContain("Operation has failed 5 consecutive times");
        result.Data["consecutive_failures"].ShouldBe(5);
        result.Data.ContainsKey("time_since_last_success").ShouldBeTrue();
    }

    [Fact]
    public async Task CheckHealthAsync_WithJustBelowThreshold_ShouldStayHealthy()
    {
        var options = new RivuletOperationHealthCheckOptions { UnhealthyFailureThreshold = 10 };

        var healthCheck = new RivuletOperationHealthCheck(options);

        // Record just below threshold
        for (var i = 0; i < 9; i++) healthCheck.RecordFailure();

        var result = await healthCheck.CheckHealthAsync(new());

        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Data["consecutive_failures"].ShouldBe(9);
    }

    [Fact]
    public async Task CheckHealthAsync_WithExactThreshold_ShouldBeUnhealthy()
    {
        var options = new RivuletOperationHealthCheckOptions { UnhealthyFailureThreshold = 3 };

        var healthCheck = new RivuletOperationHealthCheck(options);

        for (var i = 0; i < 3; i++) healthCheck.RecordFailure();

        var result = await healthCheck.CheckHealthAsync(new());

        result.Status.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WithBothStalledAndFailures_ShouldPrioritizeUnhealthy()
    {
        var options = new RivuletOperationHealthCheckOptions
        {
            StalledThreshold = TimeSpan.FromMilliseconds(10), UnhealthyFailureThreshold = 3
        };

        var healthCheck = new RivuletOperationHealthCheck(options);

        // Record failures
        for (var i = 0; i < 3; i++) healthCheck.RecordFailure();

        // Wait for stalled threshold
        await Task.Delay(50);

        var result = await healthCheck.CheckHealthAsync(new());

        // Unhealthy due to failures takes precedence
        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldNotBeNull();
        result.Description.ShouldContain("failed 3 consecutive times");
    }

    [Fact]
    public async Task RecordSuccess_ShouldUpdateLastSuccessTime()
    {
        var options = new RivuletOperationHealthCheckOptions { StalledThreshold = TimeSpan.FromMilliseconds(100) };

        var healthCheck = new RivuletOperationHealthCheck(options);

        // Wait then record success
        await Task.Delay(50);
        healthCheck.RecordSuccess();

        var result = await healthCheck.CheckHealthAsync(new());

        result.Status.ShouldBe(HealthStatus.Healthy);
        var timeSinceSuccess = (TimeSpan)result.Data["time_since_last_success"];
        timeSinceSuccess.ShouldBeLessThan(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task RecordFailure_MultipleTimes_ShouldAccumulateCorrectly()
    {
        var healthCheck = new RivuletOperationHealthCheck();

        healthCheck.RecordFailure();
        healthCheck.RecordFailure();
        var result1 = await healthCheck.CheckHealthAsync(new());
        result1.Data["consecutive_failures"].ShouldBe(2);

        healthCheck.RecordFailure();
        var result2 = await healthCheck.CheckHealthAsync(new());
        result2.Data["consecutive_failures"].ShouldBe(3);

        healthCheck.RecordSuccess();
        var result3 = await healthCheck.CheckHealthAsync(new());
        result3.Data["consecutive_failures"].ShouldBe(0);
    }

    [Fact]
    public async Task CheckHealthAsync_WithCancellationToken_ShouldComplete()
    {
        var healthCheck = new RivuletOperationHealthCheck();

        using var cts = new CancellationTokenSource();
        var result = await healthCheck.CheckHealthAsync(new(), cts.Token);

        result.Status.ShouldNotBe(default);
        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public void RivuletOperationHealthCheckOptions_ShouldHaveCorrectDefaults()
    {
        var options = new RivuletOperationHealthCheckOptions();

        options.StalledThreshold.ShouldBe(TimeSpan.FromMinutes(5));
        options.UnhealthyFailureThreshold.ShouldBe(10);
    }

    [Fact]
    public void RivuletOperationHealthCheckOptions_ShouldAllowCustomValues()
    {
        var options = new RivuletOperationHealthCheckOptions { StalledThreshold = TimeSpan.FromSeconds(30), UnhealthyFailureThreshold = 5 };

        options.StalledThreshold.ShouldBe(TimeSpan.FromSeconds(30));
        options.UnhealthyFailureThreshold.ShouldBe(5);
    }

    [Fact]
    public async Task RecordSuccess_ConcurrentCalls_ShouldBeThreadSafe()
    {
        var healthCheck = new RivuletOperationHealthCheck();

        // Record failures
        for (var i = 0; i < 5; i++) healthCheck.RecordFailure();

        // Concurrently record success
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => healthCheck.RecordSuccess()))
            .ToArray();

        await Task.WhenAll(tasks);

        var result = await healthCheck.CheckHealthAsync(new());

        result.Data["consecutive_failures"].ShouldBe(0);
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

        result.Data["consecutive_failures"].ShouldBe(100);
    }

    [Fact]
    public async Task CheckHealthAsync_DataDictionary_ShouldContainAllExpectedKeys()
    {
        var healthCheck = new RivuletOperationHealthCheck();

        healthCheck.RecordFailure();
        healthCheck.RecordFailure();

        var result = await healthCheck.CheckHealthAsync(new());

        result.Data.ContainsKey("consecutive_failures").ShouldBeTrue();
        result.Data.ContainsKey("time_since_last_success").ShouldBeTrue();
        result.Data.Count.ShouldBe(2);
    }
}