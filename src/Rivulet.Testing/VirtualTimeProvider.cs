namespace Rivulet.Testing;

/// <summary>
/// Provides virtual time for testing, allowing fast-forward of time without actual delays.
/// </summary>
public sealed class VirtualTimeProvider : IDisposable
{
    private readonly SemaphoreSlim _timeLock = new(1, 1);
    private readonly List<ScheduledTask> _scheduledTasks = new();
    private TimeSpan _currentTime = TimeSpan.Zero;
    private bool _disposed;

    /// <summary>
    /// Gets the current virtual time.
    /// </summary>
    public TimeSpan CurrentTime
    {
        get
        {
            _timeLock.Wait();
            try
            {
                return _currentTime;
            }
            finally
            {
                _timeLock.Release();
            }
        }
    }

    /// <summary>
    /// Advances virtual time by the specified duration and executes all scheduled tasks.
    /// </summary>
    public async Task AdvanceTimeAsync(TimeSpan duration)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(VirtualTimeProvider));

        await _timeLock.WaitAsync();
        try
        {
            var targetTime = _currentTime + duration;
            var tasksToExecute = new List<ScheduledTask>();

            while (_scheduledTasks.Count > 0)
            {
                var nextTask = _scheduledTasks.MinBy(t => t.ExecutionTime);
                if (nextTask is null || nextTask.ExecutionTime > targetTime)
                    break;

                _scheduledTasks.Remove(nextTask);
                _currentTime = nextTask.ExecutionTime;
                tasksToExecute.Add(nextTask);
            }

            _currentTime = targetTime;

            // Execute tasks outside the lock
            _timeLock.Release();
            foreach (var task in tasksToExecute)
            {
                task.TaskCompletionSource.SetResult();
            }
            await _timeLock.WaitAsync();
        }
        finally
        {
            _timeLock.Release();
        }
    }

    /// <summary>
    /// Schedules a virtual delay that completes when time is advanced.
    /// </summary>
    public Task DelayAsync(TimeSpan delay)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(VirtualTimeProvider));

        var tcs = new TaskCompletionSource();
        _timeLock.Wait();
        try
        {
            var executionTime = _currentTime + delay;
            _scheduledTasks.Add(new ScheduledTask(executionTime, tcs));
        }
        finally
        {
            _timeLock.Release();
        }

        return tcs.Task;
    }

    /// <summary>
    /// Resets virtual time to zero and clears all scheduled tasks.
    /// </summary>
    public void Reset()
    {
        _timeLock.Wait();
        try
        {
            _currentTime = TimeSpan.Zero;
            _scheduledTasks.Clear();
        }
        finally
        {
            _timeLock.Release();
        }
    }

    /// <summary>
    /// Disposes the virtual time provider and releases all resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timeLock.Dispose();
    }

    private sealed record ScheduledTask(TimeSpan ExecutionTime, TaskCompletionSource TaskCompletionSource);
}
