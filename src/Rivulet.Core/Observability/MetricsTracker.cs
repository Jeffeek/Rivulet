using System.Diagnostics;
using Rivulet.Core.Internal;

namespace Rivulet.Core.Observability;

/// <summary>
///     Active implementation of metrics tracker with full sampling and reporting.
///     Used when user provides MetricsOptions with custom metrics collection.
/// </summary>
internal sealed class MetricsTracker : MetricsTrackerBase
{
    private readonly MetricsOptions _options;
    private readonly CancellationTokenSource _samplerCts;
    private readonly Task _samplerTask;
    private readonly Stopwatch _stopwatch;
    private int _activeWorkers;
    private bool _disposed;
    private long _drainEvents;
    private long _itemsCompleted;

    private long _itemsStarted;
    private int _queueDepth;
    private long _throttleEvents;
    private long _totalFailures;
    private long _totalRetries;

    internal MetricsTracker(MetricsOptions options, CancellationToken cancellationToken)
    {
        _options = options;
        _stopwatch = Stopwatch.StartNew();
        _samplerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _samplerTask = PeriodicTaskRunner.RunPeriodicAsync(SampleMetrics, _options.SampleInterval, _samplerCts.Token);
    }

    private static TimeSpan DisposeWait => TimeSpan.FromSeconds(2);

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

    public override void SetActiveWorkers(int count) =>
        Interlocked.Exchange(ref _activeWorkers, count);

    public override void SetQueueDepth(int depth) =>
        Interlocked.Exchange(ref _queueDepth, depth);

    private async ValueTask SampleMetrics()
    {
        Thread.MemoryBarrier();

        var elapsed = _stopwatch.Elapsed;
        var completed = Interlocked.Read(ref _itemsCompleted);
        var started = Interlocked.Read(ref _itemsStarted);
        var retries = Interlocked.Read(ref _totalRetries);
        var failures = Interlocked.Read(ref _totalFailures);
        var throttles = Interlocked.Read(ref _throttleEvents);
        var drains = Interlocked.Read(ref _drainEvents);
        var activeWorkers = Volatile.Read(ref _activeWorkers);
        var queueDepth = Volatile.Read(ref _queueDepth);

        var itemsPerSecond = elapsed.TotalSeconds > 0 ? completed / elapsed.TotalSeconds : 0;
        var errorRate = started > 0 ? (double)failures / started : 0.0;

        await CallbackHelper.InvokeSafelyAsync(_options.OnMetricsSample,
                new MetricsSnapshot
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
                },
                nameof(MetricsOptions.OnMetricsSample))
            .ConfigureAwait(false);

        Thread.MemoryBarrier();
    }

    public override ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;

        _disposed = true;

        return DisposalHelper.DisposePeriodicTaskAsync(
            _samplerCts,
            _samplerTask,
            DisposeWait,
            _stopwatch,
            async () =>
            {
                await Task.Delay(100).ConfigureAwait(false);
                await SampleMetrics().ConfigureAwait(false);
            });
    }
}
