using System.Diagnostics;
using Rivulet.Core.Internal;

namespace Rivulet.Core.Observability;

internal sealed class ProgressTracker : IAsyncDisposable
{
    private readonly ProgressOptions _options;
    private readonly CancellationTokenSource _reporterCts;
    private readonly Task _reporterTask;
    private readonly Stopwatch _stopwatch;
    private readonly int? _totalItems;
    private bool _disposed;
    private int _errorCount;
    private int _itemsCompleted;

    private int _itemsStarted;

    public ProgressTracker(int? totalItems, ProgressOptions options, CancellationToken cancellationToken)
    {
        _totalItems = totalItems;
        _options = options;
        _stopwatch = Stopwatch.StartNew();
        _reporterCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _reporterTask = PeriodicTaskRunner.RunPeriodicAsync(
            ReportProgress,
            _options.ReportInterval,
            _reporterCts.Token,
            async () =>
            {
                await Task.Delay(100).ConfigureAwait(false);
                await ReportProgress().ConfigureAwait(false);
            });
    }

    private static TimeSpan DisposeWait => TimeSpan.FromSeconds(5);

    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;

        _disposed = true;

        return DisposalHelper.DisposePeriodicTaskAsync(_reporterCts, _reporterTask, DisposeWait, _stopwatch);
    }

    public void IncrementStarted() => Interlocked.Increment(ref _itemsStarted);

    public void IncrementCompleted() => Interlocked.Increment(ref _itemsCompleted);

    public void IncrementErrors() => Interlocked.Increment(ref _errorCount);

    private async ValueTask ReportProgress()
    {
        if (_options.OnProgress is null) return;

        Thread.MemoryBarrier();

        var elapsed = _stopwatch.Elapsed;
        var completed = Volatile.Read(ref _itemsCompleted);
        var started = Volatile.Read(ref _itemsStarted);
        var errors = Volatile.Read(ref _errorCount);

        var itemsPerSecond = elapsed.TotalSeconds > 0 ? completed / elapsed.TotalSeconds : 0;

        TimeSpan? estimatedTimeRemaining = null;
        double? percentComplete = null;

        if (_totalItems is > 0)
        {
            var remaining = _totalItems.Value - completed;
            if (itemsPerSecond > 0 && remaining > 0)
                estimatedTimeRemaining = TimeSpan.FromSeconds(remaining / itemsPerSecond);

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

        await CallbackHelper.InvokeSafelyAsync(_options.OnProgress, snapshot, nameof(ProgressOptions.OnProgress))
            .ConfigureAwait(false);

        Thread.MemoryBarrier();
    }
}