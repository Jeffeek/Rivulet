using System.Diagnostics;
using Rivulet.Core.Internal;

namespace Rivulet.Core.Tests.Internal;

public sealed class DisposalHelperTests
{
    [Fact]
    public async Task DisposePeriodicTaskAsync_WithSuccessfulCompletion_ShouldComplete()
    {
        var cts = new CancellationTokenSource();
        var taskStarted = new TaskCompletionSource<bool>();
        var taskCanceled = new TaskCompletionSource<bool>();

        var backgroundTask = Task.Run(async () =>
            {
                taskStarted.SetResult(true);
                try
                {
                    await Task.Delay(Timeout.Infinite, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    taskCanceled.SetResult(true);
                }
            },
            CancellationToken.None);

        // Wait for task to start
        await taskStarted.Task;

        // Dispose
        await DisposalHelper.DisposePeriodicTaskAsync(cts, backgroundTask, TimeSpan.FromSeconds(5));

        // Verify task was canceled
        await taskCanceled.Task.WaitAsync(TimeSpan.FromSeconds(1), CancellationToken.None);
        taskCanceled.Task.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public async Task DisposePeriodicTaskAsync_WithStopwatch_ShouldStopStopwatch()
    {
        var cts = new CancellationTokenSource();
        var stopwatch = Stopwatch.StartNew();
        var backgroundTask = Task.CompletedTask;

        stopwatch.IsRunning.ShouldBeTrue();

        await DisposalHelper.DisposePeriodicTaskAsync(cts, backgroundTask, TimeSpan.FromSeconds(1), stopwatch);

        stopwatch.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public async Task DisposePeriodicTaskAsync_WithoutStopwatch_ShouldComplete()
    {
        var cts = new CancellationTokenSource();
        var backgroundTask = Task.CompletedTask;

        // Should not throw without stopwatch
        await DisposalHelper.DisposePeriodicTaskAsync(cts, backgroundTask, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task DisposePeriodicTaskAsync_WithFinalWork_ShouldExecuteFinalWork()
    {
        var cts = new CancellationTokenSource();
        var backgroundTask = Task.CompletedTask;
        var finalWorkExecuted = false;

        await DisposalHelper.DisposePeriodicTaskAsync(
            cts,
            backgroundTask,
            TimeSpan.FromSeconds(1),
            finalWork: () =>
            {
                finalWorkExecuted = true;
                return ValueTask.CompletedTask;
            });

        finalWorkExecuted.ShouldBeTrue();
    }

    [Fact]
    public async Task DisposePeriodicTaskAsync_WithoutFinalWork_ShouldComplete()
    {
        var cts = new CancellationTokenSource();
        var backgroundTask = Task.CompletedTask;

        // Should not throw without final work
        await DisposalHelper.DisposePeriodicTaskAsync(
            cts,
            backgroundTask,
            TimeSpan.FromSeconds(1),
            finalWork: null);
    }

    [Fact]
    public async Task DisposePeriodicTaskAsync_WithTimeoutException_ShouldHandleGracefully()
    {
        var cts = new CancellationTokenSource();
        var taskStarted = new TaskCompletionSource<bool>();

        var backgroundTask = Task.Run(async () =>
            {
                taskStarted.SetResult(true);
                try
                {
                    // Keep running even after cancellation is requested
                    await Task.Delay(Timeout.Infinite, CancellationToken.None);
                }
                catch (OperationCanceledException)
                {
                    // Ignored
                }
            },
            CancellationToken.None);

        // Wait for task to start
        await taskStarted.Task;

        // Dispose with very short timeout - should handle timeout gracefully
        await DisposalHelper.DisposePeriodicTaskAsync(cts, backgroundTask, TimeSpan.FromMilliseconds(1));

        // Should complete without throwing despite timeout
    }

    [Fact]
    public async Task DisposePeriodicTaskAsync_WithOperationCanceledException_ShouldHandleGracefully()
    {
        var cts = new CancellationTokenSource();
        var taskStarted = new TaskCompletionSource<bool>();

        var backgroundTask = Task.Run(async () =>
            {
                taskStarted.SetResult(true);
                await Task.Delay(Timeout.Infinite, cts.Token);
            },
            CancellationToken.None);

        // Wait for task to start
        await taskStarted.Task;

        // Dispose - task will throw OperationCanceledException
        await DisposalHelper.DisposePeriodicTaskAsync(cts, backgroundTask, TimeSpan.FromSeconds(5));

        // Should complete without rethrowing the exception
    }

    [Fact]
    public async Task DisposePeriodicTaskAsync_WithAggregateException_ShouldHandleGracefully()
    {
        var cts = new CancellationTokenSource();

        var backgroundTask = Task.Run(() =>
            {
                var tasks = new[]
                {
                    Task.Run(async () =>
                        {
                            await Task.Delay(Timeout.Infinite, cts.Token);
                        },
                        CancellationToken.None),
                    Task.Run(async () =>
                        {
                            await Task.Delay(Timeout.Infinite, cts.Token);
                        },
                        CancellationToken.None)
                };

                return Task.WhenAll(tasks);
            },
            CancellationToken.None);

        // Small delay to ensure tasks are started
        await Task.Delay(50, CancellationToken.None);

        // Dispose - may result in AggregateException
        await DisposalHelper.DisposePeriodicTaskAsync(cts, backgroundTask, TimeSpan.FromSeconds(5));

        // Should complete without rethrowing the exception
    }

    [Fact]
    public async Task DisposePeriodicTaskAsync_WithAllParameters_ShouldExecuteAllSteps()
    {
        var cts = new CancellationTokenSource();
        var stopwatch = Stopwatch.StartNew();
        var finalWorkExecuted = false;
        var taskStarted = new TaskCompletionSource<bool>();

        var backgroundTask = Task.Run(async () =>
            {
                taskStarted.SetResult(true);
                await Task.Delay(Timeout.Infinite, cts.Token);
            },
            CancellationToken.None);

        // Wait for task to start
        await taskStarted.Task;

        stopwatch.IsRunning.ShouldBeTrue();

        await DisposalHelper.DisposePeriodicTaskAsync(
            cts,
            backgroundTask,
            TimeSpan.FromSeconds(5),
            stopwatch,
            () =>
            {
                finalWorkExecuted = true;
                return ValueTask.CompletedTask;
            });

        stopwatch.IsRunning.ShouldBeFalse();
        finalWorkExecuted.ShouldBeTrue();
    }

    [Fact]
    public async Task DisposePeriodicTaskAsync_ShouldDisposeCancellationTokenSource()
    {
        var cts = new CancellationTokenSource();
        var backgroundTask = Task.CompletedTask;

        await DisposalHelper.DisposePeriodicTaskAsync(cts, backgroundTask, TimeSpan.FromSeconds(1));

        // Verify CTS is disposed by trying to use it
        Should.Throw<ObjectDisposedException>(() => cts.Token);
    }

    [Fact]
    public async Task DisposePeriodicTaskAsync_WithAsyncFinalWork_ShouldAwaitCompletion()
    {
        var cts = new CancellationTokenSource();
        var backgroundTask = Task.CompletedTask;
        var finalWorkCompleted = false;

        await DisposalHelper.DisposePeriodicTaskAsync(
            cts,
            backgroundTask,
            TimeSpan.FromSeconds(1),
            finalWork: async () =>
            {
                await Task.Delay(100, CancellationToken.None);
                finalWorkCompleted = true;
            });

        finalWorkCompleted.ShouldBeTrue();
    }

    [Fact]
    public async Task DisposePeriodicTaskAsync_WithFastCompletingTask_ShouldNotTimeout()
    {
        var cts = new CancellationTokenSource();
        var taskStarted = new TaskCompletionSource<bool>();

        var backgroundTask = Task.Run(async () =>
            {
                taskStarted.SetResult(true);
                // Don't use cts.Token here - this simulates a task that completes quickly on its own
                await Task.Delay(50, CancellationToken.None);
            },
            CancellationToken.None);

        // Wait for task to start and complete
        await taskStarted.Task;
        await Task.Delay(100, CancellationToken.None); // Give it time to complete

        await DisposalHelper.DisposePeriodicTaskAsync(cts, backgroundTask, TimeSpan.FromSeconds(5));

        // Task should have completed without issues
        backgroundTask.IsCompleted.ShouldBeTrue();
    }
}
