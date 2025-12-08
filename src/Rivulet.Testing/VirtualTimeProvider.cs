using Rivulet.Core.Internal;

namespace Rivulet.Testing;

/// <summary>
/// Provides virtual time for testing, allowing fast-forward of time without actual delays.
/// </summary>
public sealed class VirtualTimeProvider : IDisposable
{
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif
    private readonly PriorityQueue<TaskCompletionSource, TimeSpan> _scheduledTasks = new();
    private TimeSpan _currentTime = TimeSpan.Zero;
    private bool _disposed;

    /// <summary>
    /// Gets the current virtual time.
    /// </summary>
    public TimeSpan CurrentTime => LockHelper.Execute(_lock, () => _currentTime);

    /// <summary>
    /// Advances virtual time by the specified duration and executes all scheduled tasks.
    /// </summary>
    /// <param name="duration">The amount of time to advance. Must be non-negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when duration is negative.</exception>
    public void AdvanceTime(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Duration cannot be negative.");

        ObjectDisposedException.ThrowIf(_disposed, nameof(VirtualTimeProvider));

        var tasksToExecute = LockHelper.Execute(_lock, () =>
        {
            var targetTime = _currentTime + duration;
            var tasks = new List<TaskCompletionSource>();

            while (_scheduledTasks.TryPeek(out var tcs, out var executionTime))
            {
                if (executionTime > targetTime)
                    break;

                _scheduledTasks.Dequeue();
                _currentTime = executionTime;
                tasks.Add(tcs);
            }

            _currentTime = targetTime;
            return tasks;
        });

        // Complete tasks outside the lock to avoid blocking continuations
        foreach (var tcs in tasksToExecute)
        {
            tcs.SetResult();
        }
    }

    /// <summary>
    /// Schedules a virtual delay that completes when time is advanced.
    /// </summary>
    /// <returns>A task that completes when the virtual time reaches the scheduled time.</returns>
    public Task CreateDelay(TimeSpan delay)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(VirtualTimeProvider));

        // Zero or negative delays complete immediately
        if (delay <= TimeSpan.Zero)
        {
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource();

        LockHelper.Execute(_lock, () =>
        {
            var executionTime = _currentTime + delay;
            _scheduledTasks.Enqueue(tcs, executionTime);
        });

        return tcs.Task;
    }

    /// <summary>
    /// Resets virtual time to zero and cancels all scheduled tasks.
    /// Any tasks awaiting delays will be canceled.
    /// </summary>
    public void Reset()
    {
        var tasksToCancel = LockHelper.Execute(_lock, () =>
        {
            var tasks = new List<TaskCompletionSource>();
            while (_scheduledTasks.TryDequeue(out var tcs, out _))
            {
                tasks.Add(tcs);
            }
            _currentTime = TimeSpan.Zero;
            return tasks;
        });

        // Cancel all pending tasks outside the lock
        foreach (var tcs in tasksToCancel)
        {
            tcs.TrySetCanceled();
        }
    }

    /// <summary>
    /// Disposes the virtual time provider and releases all resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
