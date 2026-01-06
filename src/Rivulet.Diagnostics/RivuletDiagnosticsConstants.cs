namespace Rivulet.Diagnostics;

/// <summary>
///     Constants used throughout the Rivulet.Diagnostics package for consistency and maintainability.
/// </summary>
internal static class RivuletDiagnosticsConstants
{
    /// <summary>
    ///     EventCounter configuration and metadata keys.
    /// </summary>
    public static class EventCounterKeys
    {
        public const string IntervalSec = "EventCounterIntervalSec";
        public const string Name = "Name";
        public const string Mean = "Mean";
        public const string Increment = "Increment";
        public const string DisplayName = "DisplayName";
        public const string DisplayUnits = "DisplayUnits";
    }

    /// <summary>
    ///     DateTime format strings for consistent timestamp formatting.
    /// </summary>
    public static class DateTimeFormats
    {
        /// <summary>
        ///     Format for console output: yyyy-MM-dd HH:mm:ss
        /// </summary>
        public const string Console = "yyyy-MM-dd HH:mm:ss";

        /// <summary>
        ///     Format for file output with milliseconds: yyyy-MM-dd HH:mm:ss.fff
        /// </summary>
        public const string File = "yyyy-MM-dd HH:mm:ss.fff";

        // ReSharper disable CommentTypo
        /// <summary>
        ///     Format for file rotation timestamps: yyyyMMdd-HHmmss
        /// </summary>
        // ReSharper restore CommentTypo
        public const string FileRotation = "yyyyMMdd-HHmmss";

        /// <summary>
        ///     Format for Prometheus comments: yyyy-MM-dd HH:mm:ss
        /// </summary>
        public const string Prometheus = "yyyy-MM-dd HH:mm:ss";
    }

    /// <summary>
    ///     Prometheus export format strings.
    /// </summary>
    public static class PrometheusFormats
    {
        public const string HeaderComment = "# Rivulet.Core Metrics";
        public const string GeneratedAtCommentFormat = "# Generated at {0} UTC";
        public const string HelpFormat = "# HELP rivulet_{0} {1}";
        public const string TypeFormat = "# TYPE rivulet_{0} gauge";
        public const string MetricFormat = "rivulet_{0} {1:F2}";
    }

    /// <summary>
    ///     Health check data dictionary keys.
    /// </summary>
    public static class HealthCheckKeys
    {
        public const string ItemsStarted = "items_started";
        public const string ItemsCompleted = "items_completed";
        public const string TotalFailures = "total_failures";
        public const string TotalRetries = "total_retries";
        public const string ErrorRate = "error_rate";
    }

    /// <summary>
    ///     Health check status messages.
    /// </summary>
    public static class HealthCheckMessages
    {
        public const string NothingRunning = "No Rivulet operations currently running";
    }
}
