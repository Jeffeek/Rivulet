using System.Diagnostics;

namespace Rivulet.Core.Observability;

internal sealed class ProgressTracker : IDisposable
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
                await Task.Delay(_options.ReportInterval, _reporterCts.Token);
                await ReportProgress();
            }
        }
        catch (OperationCanceledException)
        {
            await ReportProgress();
        }
    }

    private async Task ReportProgress()
    {
        if (_options.OnProgress is null)
            return;

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
            await _options.OnProgress(snapshot);
        }
        catch
        {
            // Swallow exceptions from user callback to prevent breaking the operation
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Fire-and-forget final progress report to avoid blocking disposal
        // This prevents potential deadlocks in synchronization contexts (ASP.NET, UI apps)
        _ = Task.Run(async () =>
        {
            try
            {
                await ReportProgress().ConfigureAwait(false);
            }
            catch
            {
                // Swallow exceptions to prevent unobserved task exceptions
            }
        });

        _reporterCts.Cancel();

        try
        {
            _reporterTask.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // ignored
        }

        _reporterCts.Dispose();
        _stopwatch.Stop();
    }
}
