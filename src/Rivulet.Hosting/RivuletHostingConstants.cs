namespace Rivulet.Hosting;

/// <summary>
///     Constants used throughout the Rivulet.Hosting package for consistency and maintainability.
/// </summary>
internal static class RivuletHostingConstants
{
    /// <summary>
    ///     Configuration section name for Rivulet settings in appsettings.json.
    /// </summary>
    public const string ConfigurationSectionName = "Rivulet";

    /// <summary>
    ///     Health check data dictionary keys for operation health checks.
    /// </summary>
    public static class HealthCheckKeys
    {
        public const string ConsecutiveFailures = "consecutive_failures";
        public const string TimeSinceLastSuccess = "time_since_last_success";
    }

    /// <summary>
    ///     Health check status messages.
    /// </summary>
    public static class HealthCheckMessages
    {
        public const string OperationHealthy = "Operation healthy";
        public const string ConsecutiveFailuresFormat = "Operation has failed {0} consecutive times";
    }
}