using System.Diagnostics;
using Rivulet.Core;

namespace Rivulet.Diagnostics.OpenTelemetry;

/// <summary>
/// ActivitySource for Rivulet parallel operations, enabling distributed tracing via OpenTelemetry.
/// </summary>
/// <remarks>
/// This ActivitySource creates spans for parallel operations, allowing correlation across
/// distributed systems. Use with OpenTelemetry.Instrumentation.* packages to export traces
/// to Jaeger, Zipkin, Azure Monitor, etc.
/// </remarks>
/// <example>
/// <code>
/// // Configure OpenTelemetry at startup
/// using var tracerProvider = Sdk.CreateTracerProviderBuilder()
///     .AddSource(RivuletActivitySource.SourceName)
///     .AddJaegerExporter()
///     .Build();
///
/// // Use Rivulet with automatic tracing
/// var results = await urls.SelectParallelAsync(
///     async (url, ct) => await httpClient.GetAsync(url, ct),
///     RivuletActivitySource.CreateOptions(new ParallelOptionsRivulet
///     {
///         MaxDegreeOfParallelism = 32
///     }));
/// </code>
/// </example>
public static class RivuletActivitySource
{
    /// <summary>
    /// The ActivitySource instance for creating activities.
    /// </summary>
    public static readonly ActivitySource Source = new(RivuletSharedConstants.RivuletCore, RivuletOpenTelemetryConstants.InstrumentationVersion);

    /// <summary>
    /// Creates an Activity for a parallel operation.
    /// </summary>
    /// <param name="operationName">The name of the operation (e.g., "SelectParallelAsync").</param>
    /// <param name="totalItems">The total number of items to process, if known.</param>
    /// <returns>An Activity if tracing is enabled, otherwise null.</returns>
    public static Activity? StartOperation(string operationName, int? totalItems = null)
    {
        // ReSharper disable once RedundantArgumentDefaultValue
        var activity = Source.StartActivity($"Rivulet.{operationName}", ActivityKind.Internal);

        if (activity is not null && totalItems.HasValue)
        {
            activity.SetTag(RivuletOpenTelemetryConstants.TagNames.TotalItems, totalItems.Value);
        }

        return activity;
    }

    /// <summary>
    /// Creates an Activity for processing a single item.
    /// </summary>
    /// <param name="operationName">The name of the operation (e.g., "ProcessItem").</param>
    /// <param name="itemIndex">The index of the item being processed.</param>
    /// <returns>An Activity if tracing is enabled, otherwise null.</returns>
    public static Activity? StartItemActivity(string operationName, int itemIndex)
    {
        // ReSharper disable once RedundantArgumentDefaultValue
        var activity = Source.StartActivity($"Rivulet.{operationName}.Item", ActivityKind.Internal);

        activity?.SetTag(RivuletOpenTelemetryConstants.TagNames.ItemIndex, itemIndex);

        return activity;
    }

    /// <summary>
    /// Records a retry attempt on the current activity.
    /// </summary>
    /// <param name="activity">The activity to annotate.</param>
    /// <param name="attemptNumber">The retry attempt number (1-based).</param>
    /// <param name="exception">The exception that triggered the retry.</param>
    public static void RecordRetry(Activity? activity, int attemptNumber, Exception? exception = null)
    {
        if (activity is null) return;

        activity.AddEvent(new ActivityEvent(RivuletOpenTelemetryConstants.EventNames.Retry,
            tags: new ActivityTagsCollection
            {
                { RivuletOpenTelemetryConstants.TagNames.RetryAttempt, attemptNumber },
                { RivuletOpenTelemetryConstants.TagNames.ExceptionType, exception?.GetType().FullName },
                { RivuletOpenTelemetryConstants.TagNames.ExceptionMessage, exception?.Message }
            }));

        activity.SetTag(RivuletOpenTelemetryConstants.TagNames.Retries, attemptNumber);
    }

    /// <summary>
    /// Records an error on the current activity.
    /// </summary>
    /// <param name="activity">The activity to annotate.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="isTransient">Whether the error is transient and eligible for retry.</param>
    public static void RecordError(Activity? activity, Exception exception, bool isTransient = false)
    {
        if (activity is null) return;

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.AddException(exception);
        activity.SetTag(RivuletOpenTelemetryConstants.TagNames.ErrorTransient, isTransient);
    }

    /// <summary>
    /// Records successful completion on the current activity.
    /// </summary>
    /// <param name="activity">The activity to annotate.</param>
    /// <param name="itemsProcessed">The number of items successfully processed.</param>
    public static void RecordSuccess(Activity? activity, int itemsProcessed)
    {
        if (activity is null) return;

        activity.SetStatus(ActivityStatusCode.Ok);
        activity.SetTag(RivuletOpenTelemetryConstants.TagNames.ItemsProcessed, itemsProcessed);
    }

    /// <summary>
    /// Records circuit breaker state change on the current activity.
    /// </summary>
    /// <param name="activity">The activity to annotate.</param>
    /// <param name="state">The new circuit breaker state.</param>
    public static void RecordCircuitBreakerStateChange(Activity? activity, string state)
    {
        activity?.AddEvent(new ActivityEvent(RivuletOpenTelemetryConstants.EventNames.CircuitBreakerStateChange,
            tags: new ActivityTagsCollection
            {
                { RivuletOpenTelemetryConstants.TagNames.CircuitBreakerState, state }
            }));
    }

    /// <summary>
    /// Records adaptive concurrency change on the current activity.
    /// </summary>
    /// <param name="activity">The activity to annotate.</param>
    /// <param name="oldConcurrency">The previous concurrency level.</param>
    /// <param name="newConcurrency">The new concurrency level.</param>
    public static void RecordConcurrencyChange(Activity? activity, int oldConcurrency, int newConcurrency)
    {
        if (activity is null) return;

        activity.AddEvent(new ActivityEvent(RivuletOpenTelemetryConstants.EventNames.AdaptiveConcurrencyChange,
            tags: new ActivityTagsCollection
            {
                { RivuletOpenTelemetryConstants.TagNames.ConcurrencyOld, oldConcurrency },
                { RivuletOpenTelemetryConstants.TagNames.ConcurrencyNew, newConcurrency }
            }));

        activity.SetTag(RivuletOpenTelemetryConstants.TagNames.ConcurrencyCurrent, newConcurrency);
    }
}
