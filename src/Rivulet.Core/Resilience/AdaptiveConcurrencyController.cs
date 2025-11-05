using System.Diagnostics.CodeAnalysis;
using Rivulet.Core.Internal;

namespace Rivulet.Core.Resilience;

/// <summary>
/// Thread-safe controller for adaptive concurrency management.
/// Monitors performance and dynamically adjusts parallelism.
/// </summary>
[SuppressMessage("ReSharper", "InconsistentlySynchronizedField")]
internal sealed class AdaptiveConcurrencyController : IDisposable
{
    private readonly AdaptiveConcurrencyOptions _options;
    private readonly SemaphoreSlim _semaphore;
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif
    private readonly Timer _samplingTimer;
    private readonly List<double> _latencySamples = new();

    private int _currentConcurrency;
    private int _successCount;
    private int _failureCount;
    private bool _disposed;

    /// <summary>
    /// Gets the current concurrency level.
    /// </summary>
    public int CurrentConcurrency => LockHelper.Execute(_lock, () => _currentConcurrency);

    /// <summary>
    /// Initializes a new instance of the AdaptiveConcurrencyController class.
    /// </summary>
    /// <param name="options">Adaptive concurrency configuration options.</param>
    public AdaptiveConcurrencyController(AdaptiveConcurrencyOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();

        _currentConcurrency = options.InitialConcurrency ?? options.MinConcurrency;
        _semaphore = new SemaphoreSlim(_currentConcurrency, options.MaxConcurrency);

        _samplingTimer = new Timer(
            _ => SampleAndAdjust(),
            null,
            _options.SampleInterval,
            _options.SampleInterval);
    }

    /// <summary>
    /// Acquires a slot to execute an operation.
    /// Blocks until a slot is available based on current concurrency limit.
    /// </summary>
    public async ValueTask AcquireAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Releases a slot after operation completion.
    /// </summary>
    /// <param name="latency">Operation latency.</param>
    /// <param name="success">Whether the operation succeeded.</param>
    public void Release(TimeSpan latency, bool success)
    {
        LockHelper.Execute(_lock, () =>
        {
            if (_options.TargetLatency.HasValue)
            {
                _latencySamples.Add(latency.TotalMilliseconds);
            }

            if (success)
                _successCount++;
            else
                _failureCount++;
        });

        _semaphore.Release();
    }

    /// <summary>
    /// Samples performance metrics and adjusts concurrency.
    /// </summary>
    private void SampleAndAdjust()
    {
        if (_disposed)
            return;

        LockHelper.Execute(_lock, () =>
        {
            var totalOps = _successCount + _failureCount;
            if (totalOps == 0)
            {
                return;
            }

            var successRate = (double)_successCount / totalOps;
            var shouldDecrease = successRate < _options.MinSuccessRate;

            if (_options.TargetLatency.HasValue && _latencySamples.Count > 0)
            {
                var avgLatency = _latencySamples.Average();
                if (avgLatency > _options.TargetLatency.Value.TotalMilliseconds)
                {
                    shouldDecrease = true;
                }
            }

            var oldConcurrency = _currentConcurrency;
            var newConcurrency = oldConcurrency;

            if (shouldDecrease)
            {
                newConcurrency = _options.DecreaseStrategy switch
                {
                    AdaptiveConcurrencyStrategy.AIMD => Math.Max(_options.MinConcurrency, oldConcurrency / 2),
                    AdaptiveConcurrencyStrategy.Aggressive => Math.Max(_options.MinConcurrency, oldConcurrency / 2),
                    AdaptiveConcurrencyStrategy.Gradual => Math.Max(_options.MinConcurrency, oldConcurrency * 3 / 4),
                    _ => Math.Max(_options.MinConcurrency, oldConcurrency / 2)
                };
            }
            else if (successRate >= _options.MinSuccessRate)
            {
                newConcurrency = _options.IncreaseStrategy switch
                {
                    AdaptiveConcurrencyStrategy.AIMD => Math.Min(_options.MaxConcurrency, oldConcurrency + 1),
                    AdaptiveConcurrencyStrategy.Aggressive => Math.Min(_options.MaxConcurrency, oldConcurrency + Math.Max(1, oldConcurrency / 10)),
                    AdaptiveConcurrencyStrategy.Gradual => Math.Min(_options.MaxConcurrency, oldConcurrency + 1),
                    _ => Math.Min(_options.MaxConcurrency, oldConcurrency + 1)
                };
            }

            if (newConcurrency != oldConcurrency)
            {
                AdjustConcurrency(oldConcurrency, newConcurrency);
            }

            _successCount = 0;
            _failureCount = 0;
            _latencySamples.Clear();
        });
    }

    /// <summary>
    /// Adjusts the semaphore capacity to the new concurrency level.
    /// </summary>
    private void AdjustConcurrency(int oldConcurrency, int newConcurrency)
    {
        var delta = newConcurrency - oldConcurrency;

        switch (delta)
        {
            case > 0:
                _semaphore.Release(delta);
                break;
            case < 0:
                _ = Task.Run(async () =>
                {
                    try
                    {
                        for (var i = 0; i < Math.Abs(delta); i++)
                        {
                            await _semaphore.WaitAsync().ConfigureAwait(false);
                        }
                    }
                    catch
                    {
                        // ignored
                    }
                }, CancellationToken.None);
                break;
        }

        _currentConcurrency = newConcurrency;

        if (_options.OnConcurrencyChange is not null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _options.OnConcurrencyChange(oldConcurrency, newConcurrency).ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            }, CancellationToken.None);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Use ManualResetEvent to ensure timer callback completes before disposal
        using var waitHandle = new ManualResetEvent(false);
        _samplingTimer.Dispose(waitHandle);
        waitHandle.WaitOne(TimeSpan.FromSeconds(1));

        _semaphore.Dispose();
    }
}
