using System.Diagnostics;

namespace Rivulet.Core.Observability;

internal sealed class MetricsTracker : IDisposable
{
    private readonly MetricsOptions? _options;
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

    public MetricsTracker(MetricsOptions? options, CancellationToken cancellationToken)
    {
        _options = options;
        _stopwatch = Stopwatch.StartNew();
        _samplerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _samplerTask = _options?.OnMetricsSample is not null ? Task.Run(SampleMetricsPeriodically, _samplerCts.Token) : Task.CompletedTask;
    }

    public void IncrementItemsStarted()
    {
        Interlocked.Increment(ref _itemsStarted);
        RivuletEventSource.Log.IncrementItemsStarted();
    }

    public void IncrementItemsCompleted()
    {
        Interlocked.Increment(ref _itemsCompleted);
        RivuletEventSource.Log.IncrementItemsCompleted();
    }

    public void IncrementRetries()
    {
        Interlocked.Increment(ref _totalRetries);
        RivuletEventSource.Log.IncrementRetries();
    }

    public void IncrementFailures()
    {
        Interlocked.Increment(ref _totalFailures);
        RivuletEventSource.Log.IncrementFailures();
    }

    public void IncrementThrottleEvents()
    {
        Interlocked.Increment(ref _throttleEvents);
        RivuletEventSource.Log.IncrementThrottleEvents();
    }

    public void IncrementDrainEvents()
    {
        Interlocked.Increment(ref _drainEvents);
        RivuletEventSource.Log.IncrementDrainEvents();
    }

    public void SetActiveWorkers(int count)
    {
        Interlocked.Exchange(ref _activeWorkers, count);
    }

    public void SetQueueDepth(int depth)
    {
        Interlocked.Exchange(ref _queueDepth, depth);
    }

    private async Task SampleMetricsPeriodically()
    {
        try
        {
            while (!_samplerCts.Token.IsCancellationRequested)
            {
                await Task.Delay(_options!.SampleInterval, _samplerCts.Token).ConfigureAwait(false);
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
        if (_options?.OnMetricsSample is null)
            return;

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
            await _options.OnMetricsSample(snapshot).ConfigureAwait(false);
        }
        catch
        {
            // Swallow
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_options?.OnMetricsSample is not null)
        {
            SampleMetrics().GetAwaiter().GetResult();
        }

        _samplerCts.Cancel();

        try
        {
            _samplerTask.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // Swallow
        }

        _samplerCts.Dispose();
        _stopwatch.Stop();
    }
}
