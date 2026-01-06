using System.Diagnostics.CodeAnalysis;

namespace Rivulet.Core.Observability;

/// <summary>
///     Constants for Rivulet.Core metrics to avoid magic strings and ensure consistency.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
public static class RivuletMetricsConstants
{
    /// <summary>
    ///     EventCounter counter names used in RivuletEventSource.
    ///     These names are used by dotnet-counters and other EventListener implementations.
    /// </summary>
    public static class CounterNames
    {
        /// <summary>
        ///     Counter name for items started: "items-started"
        /// </summary>
        public const string ItemsStarted = "items-started";

        /// <summary>
        ///     Counter name for items completed: "items-completed"
        /// </summary>
        public const string ItemsCompleted = "items-completed";

        /// <summary>
        ///     Counter name for total retries: "total-retries"
        /// </summary>
        public const string TotalRetries = "total-retries";

        /// <summary>
        ///     Counter name for total failures: "total-failures"
        /// </summary>
        public const string TotalFailures = "total-failures";

        /// <summary>
        ///     Counter name for throttle events: "throttle-events"
        /// </summary>
        public const string ThrottleEvents = "throttle-events";

        /// <summary>
        ///     Counter name for drain events: "drain-events"
        /// </summary>
        public const string DrainEvents = "drain-events";
    }

    /// <summary>
    ///     Display names for EventCounters shown in monitoring tools.
    /// </summary>
    public static class DisplayNames
    {
        /// <summary>
        ///     Display name for items started: "Items Started"
        /// </summary>
        public const string ItemsStarted = "Items Started";

        /// <summary>
        ///     Display name for items completed: "Items Completed"
        /// </summary>
        public const string ItemsCompleted = "Items Completed";

        /// <summary>
        ///     Display name for total retries: "Total Retries"
        /// </summary>
        public const string TotalRetries = "Total Retries";

        /// <summary>
        ///     Display name for total failures: "Total Failures"
        /// </summary>
        public const string TotalFailures = "Total Failures";

        /// <summary>
        ///     Display name for throttle events: "Throttle Events"
        /// </summary>
        public const string ThrottleEvents = "Throttle Events";

        /// <summary>
        ///     Display name for drain events: "Drain Events"
        /// </summary>
        public const string DrainEvents = "Drain Events";
    }

    /// <summary>
    ///     Display units for EventCounters.
    /// </summary>
    public static class DisplayUnits
    {
        /// <summary>
        ///     Unit for item counts: "items"
        /// </summary>
        public const string Items = "items";

        /// <summary>
        ///     Unit for retry counts: "retries"
        /// </summary>
        public const string Retries = "retries";

        /// <summary>
        ///     Unit for failure counts: "failures"
        /// </summary>
        public const string Failures = "failures";

        /// <summary>
        ///     Unit for event counts: "events"
        /// </summary>
        public const string Events = "events";
    }
}
