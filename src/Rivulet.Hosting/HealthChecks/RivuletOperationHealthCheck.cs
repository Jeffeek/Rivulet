using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Rivulet.Hosting.HealthChecks;

/// <summary>
///     Health check for monitoring long-running Rivulet operations.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
public sealed class RivuletOperationHealthCheck : IHealthCheck
{
    private readonly RivuletOperationHealthCheckOptions _options;
    private int _consecutiveFailures;
    // Store DateTime as ticks (long) for thread-safe access with Interlocked
    private long _lastSuccessTimeTicks = DateTime.UtcNow.Ticks;

    /// <summary>
    ///     Initializes a new instance of the RivuletOperationHealthCheck class.
    /// </summary>
    /// <param name="options">Optional configuration options.</param>
    public RivuletOperationHealthCheck(RivuletOperationHealthCheckOptions? options = null) =>
        _options = options ?? new RivuletOperationHealthCheckOptions();

    /// <summary>
    ///     Checks the health status based on recent operation successes and failures.
    /// </summary>
    /// <param name="context">The health check context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The health check result.</returns>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var lastSuccessTime = new DateTime(Interlocked.Read(ref _lastSuccessTimeTicks), DateTimeKind.Utc);
        var timeSinceLastSuccess = DateTime.UtcNow - lastSuccessTime;
        var failures = _consecutiveFailures;

        if (failures >= _options.UnhealthyFailureThreshold)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                string.Format(RivuletHostingConstants.HealthCheckMessages.ConsecutiveFailuresFormat, failures),
                data: new Dictionary<string, object>
                {
                    [RivuletHostingConstants.HealthCheckKeys.ConsecutiveFailures] = failures,
                    [RivuletHostingConstants.HealthCheckKeys.TimeSinceLastSuccess] = timeSinceLastSuccess
                }));
        }

        if (timeSinceLastSuccess > _options.StalledThreshold)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"No successful operations in {timeSinceLastSuccess.TotalSeconds:F0} seconds",
                data: new Dictionary<string, object>
                {
                    [RivuletHostingConstants.HealthCheckKeys.ConsecutiveFailures] = failures,
                    [RivuletHostingConstants.HealthCheckKeys.TimeSinceLastSuccess] = timeSinceLastSuccess
                }));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            $"Operation healthy, {failures} recent failures",
            new Dictionary<string, object>
            {
                [RivuletHostingConstants.HealthCheckKeys.ConsecutiveFailures] = failures,
                [RivuletHostingConstants.HealthCheckKeys.TimeSinceLastSuccess] = timeSinceLastSuccess
            }));
    }

    /// <summary>
    ///     Records a successful operation.
    /// </summary>
    public void RecordSuccess()
    {
        Interlocked.Exchange(ref _lastSuccessTimeTicks, DateTime.UtcNow.Ticks);
        Interlocked.Exchange(ref _consecutiveFailures, 0);
    }

    /// <summary>
    ///     Records a failed operation.
    /// </summary>
    public void RecordFailure() =>
        Interlocked.Increment(ref _consecutiveFailures);
}

/// <summary>
///     Options for configuring RivuletOperationHealthCheck.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
public sealed class RivuletOperationHealthCheckOptions
{
    /// <summary>
    ///     Time without successful operations before health is degraded. Default: 5 minutes.
    /// </summary>
    public TimeSpan StalledThreshold { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     Number of consecutive failures before health is unhealthy. Default: 10.
    /// </summary>
    public int UnhealthyFailureThreshold { get; init; } = 10;
}