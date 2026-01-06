namespace Rivulet.Hosting.HealthChecks;

/// <summary>
///     Helper methods for creating health check data dictionaries.
/// </summary>
internal static class HealthCheckDataBuilder
{
    /// <summary>
    ///     Creates a standardized health check data dictionary with failure count and time since last success.
    /// </summary>
    /// <param name="consecutiveFailures">Number of consecutive failures.</param>
    /// <param name="timeSinceLastSuccess">Time elapsed since last successful operation.</param>
    /// <returns>A dictionary containing the health check metrics.</returns>
    internal static Dictionary<string, object> CreateOperationData(
        int consecutiveFailures,
        TimeSpan timeSinceLastSuccess
    ) => new()
    {
        [RivuletHostingConstants.HealthCheckKeys.ConsecutiveFailures] = consecutiveFailures,
        [RivuletHostingConstants.HealthCheckKeys.TimeSinceLastSuccess] = timeSinceLastSuccess
    };
}
