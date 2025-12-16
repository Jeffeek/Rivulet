namespace Rivulet.Diagnostics.OpenTelemetry;

/// <summary>
///     Constants for Rivulet OpenTelemetry integration to avoid magic strings and ensure consistency.
/// </summary>
public static class RivuletOpenTelemetryConstants
{
    /// <summary>
    ///     Instrumentation version for OpenTelemetry ActivitySource and Meter.
    ///     Update this when releasing new versions.
    /// </summary>
    public const string InstrumentationVersion = "1.3.0";

    private const string RivuletPrefix = "rivulet.";

    /// <summary>
    ///     OpenTelemetry metric names exposed by RivuletMetricsExporter.
    /// </summary>
    public static class MetricNames
    {
        /// <summary>
        ///     Metric name for items started: "rivulet.items.started"
        /// </summary>
        public const string ItemsStarted = $"{RivuletPrefix}items.started";

        /// <summary>
        ///     Metric name for items completed: "rivulet.items.completed"
        /// </summary>
        public const string ItemsCompleted = $"{RivuletPrefix}items.completed";

        /// <summary>
        ///     Metric name for total retries: "rivulet.retries.total"
        /// </summary>
        public const string RetriesTotal = $"{RivuletPrefix}retries.total";

        /// <summary>
        ///     Metric name for total failures: "rivulet.failures.total"
        /// </summary>
        public const string FailuresTotal = $"{RivuletPrefix}failures.total";

        /// <summary>
        ///     Metric name for throttle events: "rivulet.throttle.events"
        /// </summary>
        public const string ThrottleEvents = $"{RivuletPrefix}throttle.events";

        /// <summary>
        ///     Metric name for drain events: "rivulet.drain.events"
        /// </summary>
        public const string DrainEvents = $"{RivuletPrefix}drain.events";

        /// <summary>
        ///     Metric name for error rate: "rivulet.error.rate"
        /// </summary>
        public const string ErrorRate = $"{RivuletPrefix}error.rate";
    }

    /// <summary>
    ///     OpenTelemetry metric units.
    /// </summary>
    public static class MetricUnits
    {
        /// <summary>
        ///     Unit for item counts: "{items}"
        /// </summary>
        public const string Items = "{items}";

        /// <summary>
        ///     Unit for retry counts: "{retries}"
        /// </summary>
        public const string Retries = "{retries}";

        /// <summary>
        ///     Unit for failure counts: "{failures}"
        /// </summary>
        public const string Failures = "{failures}";

        /// <summary>
        ///     Unit for event counts: "{events}"
        /// </summary>
        public const string Events = "{events}";

        /// <summary>
        ///     Unit for ratios: "{ratio}"
        /// </summary>
        public const string Ratio = "{ratio}";
    }

    /// <summary>
    ///     OpenTelemetry metric descriptions.
    /// </summary>
    public static class MetricDescriptions
    {
        /// <summary>
        ///     Description for items started metric.
        /// </summary>
        public const string ItemsStarted = "Total number of items that have started processing";

        /// <summary>
        ///     Description for items completed metric.
        /// </summary>
        public const string ItemsCompleted = "Total number of items that have completed processing";

        /// <summary>
        ///     Description for total retries metric.
        /// </summary>
        public const string RetriesTotal = "Total number of retry attempts across all operations";

        /// <summary>
        ///     Description for total failures metric.
        /// </summary>
        public const string FailuresTotal = "Total number of failed items after all retries";

        /// <summary>
        ///     Description for throttle events metric.
        /// </summary>
        public const string ThrottleEvents = "Total number of backpressure throttle events";

        /// <summary>
        ///     Description for drain events metric.
        /// </summary>
        public const string DrainEvents = "Total number of channel drain events";

        /// <summary>
        ///     Description for error rate metric.
        /// </summary>
        public const string ErrorRate = "Error rate (failures / items started)";
    }

    /// <summary>
    ///     Activity (span) tag names for distributed tracing.
    /// </summary>
    public static class TagNames
    {
        /// <summary>
        ///     Tag for total items count: "rivulet.total_items"
        /// </summary>
        public const string TotalItems = $"{RivuletPrefix}total_items";

        /// <summary>
        ///     Tag for item index: "rivulet.item_index"
        /// </summary>
        public const string ItemIndex = $"{RivuletPrefix}item_index";

        /// <summary>
        ///     Tag for retry attempt number: "rivulet.retry_attempt"
        /// </summary>
        public const string RetryAttempt = $"{RivuletPrefix}retry_attempt";

        /// <summary>
        ///     Tag for total retries on an item: "rivulet.retries"
        /// </summary>
        public const string Retries = $"{RivuletPrefix}retries";

        /// <summary>
        ///     Tag for error transient status: "rivulet.error.transient"
        /// </summary>
        public const string ErrorTransient = $"{RivuletPrefix}error.transient";

        /// <summary>
        ///     Tag for items processed count: "rivulet.items_processed"
        /// </summary>
        public const string ItemsProcessed = $"{RivuletPrefix}items_processed";

        /// <summary>
        ///     Tag for circuit breaker state: "rivulet.circuit_breaker.state"
        /// </summary>
        public const string CircuitBreakerState = $"{RivuletPrefix}circuit_breaker.state";

        /// <summary>
        ///     Tag for old concurrency level: "rivulet.concurrency.old"
        /// </summary>
        public const string ConcurrencyOld = $"{RivuletPrefix}concurrency.old";

        /// <summary>
        ///     Tag for new concurrency level: "rivulet.concurrency.new"
        /// </summary>
        public const string ConcurrencyNew = $"{RivuletPrefix}concurrency.new";

        /// <summary>
        ///     Tag for current concurrency level: "rivulet.concurrency.current"
        /// </summary>
        public const string ConcurrencyCurrent = $"{RivuletPrefix}concurrency.current";

        /// <summary>
        ///     Tag for exception type: "exception.type"
        /// </summary>
        public const string ExceptionType = "exception.type";

        /// <summary>
        ///     Tag for exception message: "exception.message"
        /// </summary>
        public const string ExceptionMessage = "exception.message";
    }

    /// <summary>
    ///     Activity event names for distributed tracing.
    /// </summary>
    public static class EventNames
    {
        /// <summary>
        ///     Event name for retry attempts: "retry"
        /// </summary>
        public const string Retry = "retry";

        /// <summary>
        ///     Event name for circuit breaker state changes: "circuit_breaker_state_change"
        /// </summary>
        public const string CircuitBreakerStateChange = "circuit_breaker_state_change";

        /// <summary>
        ///     Event name for adaptive concurrency changes: "adaptive_concurrency_change"
        /// </summary>
        public const string AdaptiveConcurrencyChange = "adaptive_concurrency_change";
    }
}