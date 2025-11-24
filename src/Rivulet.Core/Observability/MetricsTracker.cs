using System.Diagnostics;

namespace Rivulet.Core.Observability;

/// <summary>
/// Active implementation of metrics tracker with full sampling and reporting.
/// Used when user provides MetricsOptions with custom metrics collection.
/// </summary>
internal sealed class MetricsTracker : MetricsTrackerBase
{
    private readonly MetricsOptions _options;
    private readonly Stopwatch _stopwatch;
    private readonly CancellationTokenSource _samplerCts;
    private readonly Task _samplerTask;

    private long _itemsStarted;
    private long _itemsCompleted;
    private long _totalRetries;
    private long _totalFailures;
    private long _throttleEvents;
    private long _drainEvents;
    private int _activeWorkers;
    private int _queueDepth;
    private bool _disposed;

    internal MetricsTracker(MetricsOptions options, CancellationToken cancellationToken)
    {
        _options = options;
        _stopwatch = Stopwatch.StartNew();
        _samplerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _samplerTask = Task.Run(SampleMetricsPeriodically, _samplerCts.Token);
    }

    public override void IncrementItemsStarted()
    {
        Interlocked.Increment(ref _itemsStarted);
        RivuletEventSource.Log.IncrementItemsStarted();
    }

    public override void IncrementItemsCompleted()
    {
        Interlocked.Increment(ref _itemsCompleted);
        RivuletEventSource.Log.IncrementItemsCompleted();
    }

    public override void IncrementRetries()
    {
        Interlocked.Increment(ref _totalRetries);
        RivuletEventSource.Log.IncrementRetries();
    }

    public override void IncrementFailures()
    {
        Interlocked.Increment(ref _totalFailures);
        RivuletEventSource.Log.IncrementFailures();
    }

    public override void IncrementThrottleEvents()
    {
        Interlocked.Increment(ref _throttleEvents);
        RivuletEventSource.Log.IncrementThrottleEvents();
    }

    public override void IncrementDrainEvents()
    {
        Interlocked.Increment(ref _drainEvents);
        RivuletEventSource.Log.IncrementDrainEvents();
    }

    public override void SetActiveWorkers(int count)
    {
        Interlocked.Exchange(ref _activeWorkers, count);
    }

    public override void SetQueueDepth(int depth)
    {
        Interlocked.Exchange(ref _queueDepth, depth);
    }

    private async Task SampleMetricsPeriodically()
    {
        try
        {
            while (!_samplerCts.Token.IsCancellationRequested)
            {
                await Task.Delay(_options.SampleInterval, _samplerCts.Token).ConfigureAwait(false);
                await SampleMetrics().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Wait to ensure all in-flight metric increments complete
            // before taking the final sample. This prevents race conditions where
            // the last items are still calling Increment*() methods.
            // Increased from 100ms → 200ms → 500ms → 1000ms for Windows CI/CD reliability
            // The 1000ms delay accounts for:
            // - CPU cache coherency delays on multi-core Windows runners (~500ms worst case)
            // - Memory barrier propagation across NUMA nodes
            // - Async state machine cleanup and thread pool scheduling delays
            await Task.Delay(1000).ConfigureAwait(false);
            await SampleMetrics().ConfigureAwait(false);
        }
    }

    private async Task SampleMetrics()
    {
        // Force a memory barrier to ensure all writes from worker threads are visible
        // This is critical for final samples where workers just finished processing
        Thread.MemoryBarrier();

        var elapsed = _stopwatch.Elapsed;
        var completed = Interlocked.Read(ref _itemsCompleted);
        var started = Interlocked.Read(ref _itemsStarted);
        var retries = Interlocked.Read(ref _totalRetries);
        var failures = Interlocked.Read(ref _totalFailures);
        var throttles = Interlocked.Read(ref _throttleEvents);
        var drains = Interlocked.Read(ref _drainEvents);
        var activeWorkers = Interlocked.CompareExchange(ref _activeWorkers, 0, 0);
        var queueDepth = Interlocked.CompareExchange(ref _queueDepth, 0, 0);

        var itemsPerSecond = elapsed.TotalSeconds > 0 ? completed / elapsed.TotalSeconds : 0;
        var errorRate = started > 0 ? (double)failures / started : 0.0;

        var snapshot = new MetricsSnapshot
        {
            ActiveWorkers = activeWorkers,
            QueueDepth = queueDepth,
            ItemsStarted = started,
            ItemsCompleted = completed,
            TotalRetries = retries,
            TotalFailures = failures,
            ThrottleEvents = throttles,
            DrainEvents = drains,
            Elapsed = elapsed,
            ItemsPerSecond = itemsPerSecond,
            ErrorRate = errorRate
        };

        try
        {
            if (_options.OnMetricsSample != null)
                await _options.OnMetricsSample(snapshot).ConfigureAwait(false);
        }
        catch
        {
            // ignored
        }

        // Memory barrier after callback ensures callback writes are globally visible
        Thread.MemoryBarrier();
    }

    private static TimeSpan DisposeWait => TimeSpan.FromSeconds(5);

    public override async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Cancel the background sampler task
        // The SampleMetricsPeriodically loop will catch OperationCanceledException
        // and execute one final metrics sample before exiting
        await _samplerCts.CancelAsync().ConfigureAwait(false);

        // Wait for the sampler task to complete its final sample
        // Use a longer timeout to ensure final metrics are captured even under high CPU contention
        // This is important for accurate metrics in high-concurrency scenarios
        try
        {
            await _samplerTask.WaitAsync(DisposeWait);
        }
        catch (Exception ex) when (ex is AggregateException or OperationCanceledException)
        {
            // Task was cancelled or faulted, which is expected
        }

        _samplerCts.Dispose();
        _stopwatch.Stop();
    }
}
