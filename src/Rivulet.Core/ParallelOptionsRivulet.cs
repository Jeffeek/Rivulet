namespace Rivulet.Core;

/// <summary>
/// Configuration options for controlling parallel async operations, including concurrency limits,
/// error handling modes, retry policies, timeouts, and lifecycle hooks.
/// </summary>
public sealed class ParallelOptionsRivulet
{
    /// <summary>
    /// Gets the maximum number of concurrent tasks to execute in parallel.
    /// Defaults to the number of processor cores.
    /// </summary>
    public int MaxDegreeOfParallelism { get; init; } = Math.Max(1, Environment.ProcessorCount);
    /// <summary>
    /// Gets the timeout duration for processing each individual item.
    /// If null, no per-item timeout is enforced. Defaults to null.
    /// </summary>
    public TimeSpan? PerItemTimeout { get; init; }
    /// <summary>
    /// Gets the error handling mode that determines how failures are managed during parallel processing.
    /// Defaults to <see cref="Core.ErrorMode.FailFast"/>.
    /// </summary>
    public ErrorMode ErrorMode { get; init; } = ErrorMode.FailFast;

    /// <summary>
    /// Gets a callback invoked when an error occurs during processing.
    /// The callback receives the item index and the exception.
    /// Return true to continue processing, false to cancel remaining work.
    /// In <see cref="Core.ErrorMode.CollectAndContinue"/> and <see cref="Core.ErrorMode.BestEffort"/> modes,
    /// this only affects flow control and does not prevent error collection.
    /// </summary>
    public Func<int, Exception, ValueTask<bool>>? OnErrorAsync { get; init; }
    /// <summary>
    /// Gets a callback invoked when processing of an item starts.
    /// Receives the item index.
    /// </summary>
    public Func<int, ValueTask>? OnStartItemAsync { get; init; }
    /// <summary>
    /// Gets a callback invoked when processing of an item completes successfully.
    /// Receives the item index.
    /// </summary>
    public Func<int, ValueTask>? OnCompleteItemAsync { get; init; }
    /// <summary>
    /// Gets a callback invoked periodically when the processing pipeline is throttling due to backpressure.
    /// Receives the current item count.
    /// </summary>
    public Func<int, ValueTask>? OnThrottleAsync { get; init; }
    /// <summary>
    /// Gets a callback invoked when the processing pipeline is draining remaining items.
    /// Receives the current item count.
    /// </summary>
    public Func<int, ValueTask>? OnDrainAsync { get; init; }

    /// <summary>
    /// Gets a predicate to determine if an exception is transient and should be retried.
    /// Return true for transient errors, false for permanent failures.
    /// If null, no retries are performed. Defaults to null.
    /// </summary>
    public Func<Exception, bool>? IsTransient { get; init; }
    /// <summary>
    /// Gets the maximum number of retry attempts for transient failures.
    /// Defaults to 0 (no retries).
    /// </summary>
    public int MaxRetries { get; init; } = 0;
    /// <summary>
    /// Gets the base delay for exponential backoff between retry attempts.
    /// The actual delay is calculated as BaseDelay * 2^(attempt - 1).
    /// Defaults to 100 milliseconds.
    /// </summary>
    public TimeSpan BaseDelay { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets the channel capacity for buffering items in streaming operations.
    /// Controls backpressure by limiting how many items can be queued.
    /// Defaults to 1024.
    /// </summary>
    public int ChannelCapacity { get; init; } = 1024;
}
