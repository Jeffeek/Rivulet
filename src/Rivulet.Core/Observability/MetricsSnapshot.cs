using System.Diagnostics.CodeAnalysis;

namespace Rivulet.Core.Observability;

/// <summary>
///     Represents a snapshot of runtime metrics for parallel operations.
///     Provides visibility into active workers, queue depth, throughput, and error rates.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
public sealed class MetricsSnapshot
{
    /// <summary>
    ///     Gets the number of worker tasks currently executing operations.
    /// </summary>
    public int ActiveWorkers { get; init; }

    /// <summary>
    ///     Gets the current number of items waiting in the input channel queue.
    ///     Indicates backpressure when near <see cref="ParallelOptionsRivulet.ChannelCapacity" />.
    /// </summary>
    public int QueueDepth { get; init; }

    /// <summary>
    ///     Gets the total number of items that have started processing.
    /// </summary>
    public long ItemsStarted { get; init; }

    /// <summary>
    ///     Gets the total number of items that have completed processing successfully.
    /// </summary>
    public long ItemsCompleted { get; init; }

    /// <summary>
    ///     Gets the total number of retry attempts across all items.
    ///     Indicates transient error frequency.
    /// </summary>
    public long TotalRetries { get; init; }

    /// <summary>
    ///     Gets the total number of failed items across all retries.
    ///     Includes both transient and permanent failures.
    /// </summary>
    public long TotalFailures { get; init; }

    /// <summary>
    ///     Gets the number of throttle events triggered by backpressure.
    ///     Indicates when the input queue is full and producers are waiting.
    /// </summary>
    public long ThrottleEvents { get; init; }

    /// <summary>
    ///     Gets the number of drain events when the pipeline is emptying.
    /// </summary>
    public long DrainEvents { get; init; }

    /// <summary>
    ///     Gets the time elapsed since the operation started.
    /// </summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>
    ///     Gets the current throughput in items completed per second.
    ///     Calculated based on <see cref="ItemsCompleted" /> and <see cref="Elapsed" />.
    /// </summary>
    public double ItemsPerSecond { get; init; }

    /// <summary>
    ///     Gets the current error rate as a percentage (0.0 to 1.0).
    ///     Calculated as TotalFailures / ItemsStarted.
    ///     Returns 0.0 if no items have started.
    /// </summary>
    public double ErrorRate { get; init; }
}
