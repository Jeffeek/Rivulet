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
    private readonly List<ScheduledTask> _scheduledTasks = new();
    private TimeSpan _currentTime = TimeSpan.Zero;
    private bool _disposed;

    /// <summary>
    /// Gets the current virtual time.
    /// </summary>
    public TimeSpan CurrentTime => LockHelper.Execute(_lock, () => _currentTime);

    /// <summary>
    /// Advances virtual time by the specified duration and executes all scheduled tasks.
    /// </summary>
    public void AdvanceTime(TimeSpan duration)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(VirtualTimeProvider));

        var tasksToExecute = LockHelper.Execute(_lock, () =>
        {
            var targetTime = _currentTime + duration;
            var tasks = new List<ScheduledTask>();

            while (_scheduledTasks.Count > 0)
            {
                var nextTask = _scheduledTasks.MinBy(t => t.ExecutionTime);
                if (nextTask is null || nextTask.ExecutionTime > targetTime)
                    break;

                _scheduledTasks.Remove(nextTask);
                _currentTime = nextTask.ExecutionTime;
                tasks.Add(nextTask);
            }

            _currentTime = targetTime;
            return tasks;
        });

        // Complete tasks outside the lock to avoid blocking continuations
        foreach (var task in tasksToExecute)
        {
            task.TaskCompletionSource.SetResult();
        }
    }

    /// <summary>
    /// Schedules a virtual delay that completes when time is advanced.
    /// </summary>
    /// <returns>A task that completes when the virtual time reaches the scheduled time.</returns>
    public Task CreateDelay(TimeSpan delay)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(VirtualTimeProvider));

        var tcs = new TaskCompletionSource();

        LockHelper.Execute(_lock, () =>
        {
            var executionTime = _currentTime + delay;
            _scheduledTasks.Add(new ScheduledTask(executionTime, tcs));
        });

        return tcs.Task;
    }

    /// <summary>
    /// Resets virtual time to zero and clears all scheduled tasks.
    /// </summary>
    public void Reset()
    {
        LockHelper.Execute(_lock, () =>
        {
            _currentTime = TimeSpan.Zero;
            _scheduledTasks.Clear();
        });
    }

    /// <summary>
    /// Disposes the virtual time provider and releases all resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }

    private sealed record ScheduledTask(TimeSpan ExecutionTime, TaskCompletionSource TaskCompletionSource);
}
