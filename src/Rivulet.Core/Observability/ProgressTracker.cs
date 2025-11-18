using System.Diagnostics;

namespace Rivulet.Core.Observability;

internal sealed class ProgressTracker : IAsyncDisposable
{
    private readonly int? _totalItems;
    private readonly ProgressOptions _options;
    private readonly Stopwatch _stopwatch;
    private readonly CancellationTokenSource _reporterCts;
    private readonly Task _reporterTask;

    private int _itemsStarted;
    private int _itemsCompleted;
    private int _errorCount;
    private bool _disposed;

    public ProgressTracker(int? totalItems, ProgressOptions options, CancellationToken cancellationToken)
    {
        _totalItems = totalItems;
        _options = options;
        _stopwatch = Stopwatch.StartNew();
        _reporterCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _reporterTask = Task.Run(ReportProgressPeriodically, _reporterCts.Token);
    }

    public void IncrementStarted() => Interlocked.Increment(ref _itemsStarted);

    public void IncrementCompleted() => Interlocked.Increment(ref _itemsCompleted);

    public void IncrementErrors() => Interlocked.Increment(ref _errorCount);

    private async Task ReportProgressPeriodically()
    {
        try
        {
            while (!_reporterCts.Token.IsCancellationRequested)
            {
                await Task.Delay(_options.ReportInterval, _reporterCts.Token).ConfigureAwait(false);
                await ReportProgress().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Wait to ensure all in-flight counter increments complete
            // before taking the final progress report. This prevents race conditions where
            // the last items are still calling Increment*() methods.
            // Increased from 100ms → 200ms for Windows CI/CD reliability (0.56% "off by 1" failures)
            await Task.Delay(200).ConfigureAwait(false);
            await ReportProgress().ConfigureAwait(false);
        }
    }

    private async Task ReportProgress()
    {
        if (_options.OnProgress is null)
            return;

        // Force a memory barrier to ensure all writes from worker threads are visible
        // This is critical for final reports where workers just finished processing
        Thread.MemoryBarrier();

        var elapsed = _stopwatch.Elapsed;
        var completed = _itemsCompleted;
        var started = _itemsStarted;
        var errors = _errorCount;

        var itemsPerSecond = elapsed.TotalSeconds > 0 ? completed / elapsed.TotalSeconds : 0;

        TimeSpan? estimatedTimeRemaining = null;
        double? percentComplete = null;

        if (_totalItems is > 0)
        {
            var remaining = _totalItems.Value - completed;
            if (itemsPerSecond > 0 && remaining > 0)
            {
                estimatedTimeRemaining = TimeSpan.FromSeconds(remaining / itemsPerSecond);
            }

            percentComplete = (double)completed / _totalItems.Value * 100.0;
        }

        var snapshot = new ProgressSnapshot
        {
            ItemsStarted = started,
            ItemsCompleted = completed,
            TotalItems = _totalItems,
            ErrorCount = errors,
            Elapsed = elapsed,
            ItemsPerSecond = itemsPerSecond,
            EstimatedTimeRemaining = estimatedTimeRemaining,
            PercentComplete = percentComplete
        };

        try
        {
            await _options.OnProgress(snapshot).ConfigureAwait(false);
        }
        catch
        {
            // Swallow exceptions from user callback to prevent breaking the operation
        }

        // Memory barrier after callback ensures callback writes are globally visible
        Thread.MemoryBarrier();
    }

    private static TimeSpan DisposeWait => TimeSpan.FromSeconds(5);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Cancel the background reporter task
        // The ReportProgressPeriodically loop will catch OperationCanceledException
        // and execute one final progress report before exiting
        await _reporterCts.CancelAsync().ConfigureAwait(false);

        // Wait briefly for the reporter task to complete its final report
        // Use a timeout to allow final callback execution without indefinite blocking
        try
        {
            await _reporterTask.WaitAsync(DisposeWait).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is AggregateException or OperationCanceledException)
        {
            // Task was cancelled or faulted, which is expected
        }

        _reporterCts.Dispose();
        _stopwatch.Stop();
    }
}
