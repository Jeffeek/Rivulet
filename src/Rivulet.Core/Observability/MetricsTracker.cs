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

        Task.Run(SampleMetricsPeriodically, _samplerCts.Token);
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
            await SampleMetrics().ConfigureAwait(false);
        }
    }

    private async Task SampleMetrics()
    {
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
    }

    public override void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Cancel the background sampler task
        // The SampleMetricsPeriodically loop checks _samplerCts.Token.IsCancellationRequested
        // and will exit naturally. The _disposed flag protects against any race conditions.
        _samplerCts.Cancel();

        // Fire-and-forget final metrics sample (don't wait to avoid blocking)
        _ = Task.Run(async () =>
        {
            try
            {
                await SampleMetrics().ConfigureAwait(false);
            }
            catch
            {
                // Swallow exceptions to prevent unobserved task exceptions
            }
        }, CancellationToken.None);

        // Dispose resources (don't wait for task completion to avoid thread pool exhaustion)
        _samplerCts.Dispose();
        _stopwatch.Stop();
    }
}
