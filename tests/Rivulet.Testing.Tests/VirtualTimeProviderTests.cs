using System.Diagnostics.CodeAnalysis;

namespace Rivulet.Testing.Tests;

[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public sealed class VirtualTimeProviderTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithZeroTime()
    {
        using var timeProvider = new VirtualTimeProvider();

        timeProvider.CurrentTime.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void AdvanceTime_ShouldUpdateCurrentTime()
    {
        using var timeProvider = new VirtualTimeProvider();

        timeProvider.AdvanceTime(TimeSpan.FromSeconds(5));

        timeProvider.CurrentTime.ShouldBe(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void AdvanceTime_ShouldBeAdditive()
    {
        using var timeProvider = new VirtualTimeProvider();

        timeProvider.AdvanceTime(TimeSpan.FromSeconds(3));
        timeProvider.AdvanceTime(TimeSpan.FromSeconds(2));

        timeProvider.CurrentTime.ShouldBe(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DelayAsync_WithZeroDuration_ShouldCompleteImmediately()
    {
        using var timeProvider = new VirtualTimeProvider();

        var delayTask = timeProvider.CreateDelay(TimeSpan.Zero);

        timeProvider.AdvanceTime(TimeSpan.Zero);

        delayTask.IsCompleted.ShouldBeTrue();
        await delayTask;
    }

    [Fact]
    public async Task DelayAsync_WithNegativeDuration_ShouldCompleteImmediately()
    {
        using var timeProvider = new VirtualTimeProvider();

        var delayTask = timeProvider.CreateDelay(TimeSpan.FromSeconds(-5));

        timeProvider.AdvanceTime(TimeSpan.Zero);

        delayTask.IsCompleted.ShouldBeTrue();
        await delayTask;
    }

    [Fact]
    public async Task AdvanceTimeAsync_WithMultipleScheduledTasks_ShouldExecuteAllInOrder()
    {
        using var timeProvider = new VirtualTimeProvider();

        // Create all delays upfront
        var delay1 = timeProvider.CreateDelay(TimeSpan.FromSeconds(5));
        var delay2 = timeProvider.CreateDelay(TimeSpan.FromSeconds(3));
        var delay3 = timeProvider.CreateDelay(TimeSpan.FromSeconds(7));

        // All delays should be incomplete
        delay1.IsCompleted.ShouldBeFalse();
        delay2.IsCompleted.ShouldBeFalse();
        delay3.IsCompleted.ShouldBeFalse();

        // Advance time past all delays
        timeProvider.AdvanceTime(TimeSpan.FromSeconds(10));

        // All delays should now be completed
        delay1.IsCompleted.ShouldBeTrue();
        delay2.IsCompleted.ShouldBeTrue();
        delay3.IsCompleted.ShouldBeTrue();

        // Await the delays - they should complete immediately since time was advanced
        await Task.WhenAll(delay1, delay2, delay3);

        // Verify the virtual time was advanced correctly
        timeProvider.CurrentTime.ShouldBe(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Dispose_ShouldCancelAllPendingDelays()
    {
        var timeProvider = new VirtualTimeProvider();

        timeProvider.Dispose();

        Assert.Throws<ObjectDisposedException>(() => { _ = timeProvider.CreateDelay(TimeSpan.FromSeconds(10)); });
        Assert.Throws<ObjectDisposedException>(() => { _ = timeProvider.CreateDelay(TimeSpan.FromSeconds(20)); });
    }

    [Fact]
    public void Dispose_ShouldBeIdempotent()
    {
        var timeProvider = new VirtualTimeProvider();

        timeProvider.Dispose();
        var act = () => timeProvider.Dispose();

        act.ShouldNotThrow();
    }

    [Fact]
    public void DelayAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        var timeProvider = new VirtualTimeProvider();
        timeProvider.Dispose();

        Assert.Throws<ObjectDisposedException>(() => { _ = timeProvider.CreateDelay(TimeSpan.FromSeconds(1)); });
    }

    [Fact]
    public void AdvanceTimeAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        var timeProvider = new VirtualTimeProvider();
        timeProvider.Dispose();

        var act = () => timeProvider.AdvanceTime(TimeSpan.FromSeconds(1));

        act.ShouldThrow<ObjectDisposedException>();
    }

    [Fact]
    public async Task AdvanceTimeAsync_WithLargeTimeSpan_ShouldWork()
    {
        using var timeProvider = new VirtualTimeProvider();

        var delay = timeProvider.CreateDelay(TimeSpan.FromDays(365));

        timeProvider.AdvanceTime(TimeSpan.FromDays(365));

        delay.IsCompleted.ShouldBeTrue();
        await delay;
    }

    [Fact]
    public async Task ConcurrentDelays_ShouldAllCompleteCorrectly()
    {
        using var timeProvider = new VirtualTimeProvider();

        // Register all delays synchronously first to avoid race condition
        var delayTasks = Enumerable.Range(1, 100)
            .Select(i => (Index: i, Task: timeProvider.CreateDelay(TimeSpan.FromMilliseconds(i * 10))))
            .ToList();

        // Now advance time to complete all delays
        timeProvider.AdvanceTime(TimeSpan.FromSeconds(10));

        // Wait for all delays to complete concurrently
        await Task.WhenAll(delayTasks.Select(static x => x.Task));

        // Verify all delays completed
        var results = delayTasks.Select(static x => x.Index).ToList();

        results.Count.ShouldBe(100);
        results.ShouldAllBe(static i => i >= 1 && i <= 100);
    }

    [Fact]
    public async Task ConcurrentDelaysWithTaskRun_ShouldAllCompleteCorrectly()
    {
        using var timeProvider = new VirtualTimeProvider();
        using var countdown = new CountdownEvent(100);

        // Use Task.Run to test concurrent registration
        var tasks = Enumerable.Range(1, 100)
            .Select(i => Task.Run(async () =>
            {
                var delayTask = timeProvider.CreateDelay(TimeSpan.FromMilliseconds(i * 10));
                countdown.Signal(); // Signal that this task has registered
                await delayTask;
                return i;
            }))
            .ToList();

        // Wait for all tasks to register their delays
        countdown.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue("all tasks should register within 5 seconds");

        // Now advance time to complete all delays
        timeProvider.AdvanceTime(TimeSpan.FromSeconds(10));

        // Wait for all tasks to complete
        var results = await Task.WhenAll(tasks);

        results.Length.ShouldBe(100);
        results.ShouldAllBe(static i => i >= 1 && i <= 100);
    }

    [Fact]
    public async Task Reset_ShouldClearTimeAndScheduledTasks()
    {
        using var timeProvider = new VirtualTimeProvider();

        // Advance time
        timeProvider.AdvanceTime(TimeSpan.FromSeconds(5));
        timeProvider.CurrentTime.ShouldBe(TimeSpan.FromSeconds(5));

        // Schedule some tasks that won't complete before reset
        var task1 = timeProvider.CreateDelay(TimeSpan.FromSeconds(10));
        var task2 = timeProvider.CreateDelay(TimeSpan.FromSeconds(20));

        // Tasks should not be completed yet
        task1.IsCompleted.ShouldBeFalse();
        task2.IsCompleted.ShouldBeFalse();

        // Reset
        timeProvider.Reset();

        // Time should be back to zero
        timeProvider.CurrentTime.ShouldBe(TimeSpan.Zero);

        // Old tasks should be canceled after reset
        task1.IsCompleted.ShouldBeTrue();
        task2.IsCompleted.ShouldBeTrue();
        task1.IsCanceled.ShouldBeTrue();
        task2.IsCanceled.ShouldBeTrue();

        // Advancing time should not affect canceled tasks
        timeProvider.AdvanceTime(TimeSpan.FromSeconds(30));

        // New tasks after reset should work normally
        var task3 = timeProvider.CreateDelay(TimeSpan.FromSeconds(5));
        task3.IsCompleted.ShouldBeFalse();

        timeProvider.AdvanceTime(TimeSpan.FromSeconds(5));
        task3.IsCompleted.ShouldBeTrue();
        task3.IsCanceled.ShouldBeFalse();
        await task3;
    }

    [Fact]
    public void Reset_ShouldAllowMultipleCalls()
    {
        using var timeProvider = new VirtualTimeProvider();

        timeProvider.AdvanceTime(TimeSpan.FromSeconds(10));
        timeProvider.Reset();
        timeProvider.CurrentTime.ShouldBe(TimeSpan.Zero);

        timeProvider.AdvanceTime(TimeSpan.FromSeconds(5));
        timeProvider.Reset();
        timeProvider.CurrentTime.ShouldBe(TimeSpan.Zero);

        var act = () => timeProvider.Reset();
        act.ShouldNotThrow();
    }

    [Fact]
    public void AdvanceTime_WithNegativeDuration_ShouldThrow()
    {
        using var timeProvider = new VirtualTimeProvider();

        var act = () => timeProvider.AdvanceTime(TimeSpan.FromSeconds(-1));
        var ex = act.ShouldThrow<ArgumentOutOfRangeException>();
        ex.ParamName.ShouldBe("duration");
        ex.Message.ShouldContain("Duration cannot be negative");
    }
}
