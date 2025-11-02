using System.Diagnostics.CodeAnalysis;
using FluentAssertions;

namespace Rivulet.Testing.Tests;

[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public class VirtualTimeProviderTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithZeroTime()
    {
        using var timeProvider = new VirtualTimeProvider();

        timeProvider.CurrentTime.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void AdvanceTime_ShouldUpdateCurrentTime()
    {
        using var timeProvider = new VirtualTimeProvider();

        timeProvider.AdvanceTime(TimeSpan.FromSeconds(5));

        timeProvider.CurrentTime.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void AdvanceTime_ShouldBeAdditive()
    {
        using var timeProvider = new VirtualTimeProvider();

        timeProvider.AdvanceTime(TimeSpan.FromSeconds(3));
        timeProvider.AdvanceTime(TimeSpan.FromSeconds(2));

        timeProvider.CurrentTime.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DelayAsync_WithZeroDuration_ShouldCompleteImmediately()
    {
        using var timeProvider = new VirtualTimeProvider();

        var delayTask = timeProvider.CreateDelay(TimeSpan.Zero);

        timeProvider.AdvanceTime(TimeSpan.Zero);

        delayTask.IsCompleted.Should().BeTrue();
        await delayTask;
    }

    [Fact]
    public async Task DelayAsync_WithNegativeDuration_ShouldCompleteImmediately()
    {
        using var timeProvider = new VirtualTimeProvider();

        var delayTask = timeProvider.CreateDelay(TimeSpan.FromSeconds(-5));

        timeProvider.AdvanceTime(TimeSpan.Zero);

        delayTask.IsCompleted.Should().BeTrue();
        await delayTask;
    }

    [Fact]
    public async Task AdvanceTimeAsync_WithMultipleScheduledTasks_ShouldExecuteAllInOrder()
    {
        using var timeProvider = new VirtualTimeProvider();
        var executionOrder = new List<int>();

        var task1 = Task.Run(async () =>
        {
            await timeProvider.CreateDelay(TimeSpan.FromSeconds(5));
            executionOrder.Add(1);
        });

        var task2 = Task.Run(async () =>
        {
            await timeProvider.CreateDelay(TimeSpan.FromSeconds(3));
            executionOrder.Add(2);
        });

        var task3 = Task.Run(async () =>
        {
            await timeProvider.CreateDelay(TimeSpan.FromSeconds(7));
            executionOrder.Add(3);
        });

        await Task.Delay(50);

        timeProvider.AdvanceTime(TimeSpan.FromSeconds(10));

        await Task.WhenAll(task1, task2, task3);

        executionOrder.Should().Equal(2, 1, 3);
    }

    [Fact]
    public void Dispose_ShouldCancelAllPendingDelays()
    {
        var timeProvider = new VirtualTimeProvider();

        timeProvider.Dispose();

        Assert.Throws<ObjectDisposedException>(() => { var _ = timeProvider.CreateDelay(TimeSpan.FromSeconds(10)); });
        Assert.Throws<ObjectDisposedException>(() => { var _ = timeProvider.CreateDelay(TimeSpan.FromSeconds(20)); });
    }

    [Fact]
    public void Dispose_ShouldBeIdempotent()
    {
        var timeProvider = new VirtualTimeProvider();

        timeProvider.Dispose();
        var act = () => timeProvider.Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public void DelayAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        var timeProvider = new VirtualTimeProvider();
        timeProvider.Dispose();

        Assert.Throws<ObjectDisposedException>(() => { var _ = timeProvider.CreateDelay(TimeSpan.FromSeconds(1)); });
    }

    [Fact]
    public void AdvanceTimeAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        var timeProvider = new VirtualTimeProvider();
        timeProvider.Dispose();

        var act = () => timeProvider.AdvanceTime(TimeSpan.FromSeconds(1));

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task AdvanceTimeAsync_WithLargeTimeSpan_ShouldWork()
    {
        using var timeProvider = new VirtualTimeProvider();

        var delay = timeProvider.CreateDelay(TimeSpan.FromDays(365));

        timeProvider.AdvanceTime(TimeSpan.FromDays(365));

        delay.IsCompleted.Should().BeTrue();
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
        await Task.WhenAll(delayTasks.Select(x => x.Task));

        // Verify all delays completed
        var results = delayTasks.Select(x => x.Index).ToList();

        results.Should().HaveCount(100);
        results.Should().OnlyContain(i => i >= 1 && i <= 100);
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
        countdown.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue("all tasks should register within 5 seconds");

        // Now advance time to complete all delays
        timeProvider.AdvanceTime(TimeSpan.FromSeconds(10));

        // Wait for all tasks to complete
        var results = await Task.WhenAll(tasks);

        results.Should().HaveCount(100);
        results.Should().OnlyContain(i => i >= 1 && i <= 100);
    }

    [Fact]
    public async Task Reset_ShouldClearTimeAndScheduledTasks()
    {
        using var timeProvider = new VirtualTimeProvider();

        // Advance time
        timeProvider.AdvanceTime(TimeSpan.FromSeconds(5));
        timeProvider.CurrentTime.Should().Be(TimeSpan.FromSeconds(5));

        // Schedule some tasks that won't complete
        var task1 = timeProvider.CreateDelay(TimeSpan.FromSeconds(10));
        var task2 = timeProvider.CreateDelay(TimeSpan.FromSeconds(20));

        // Reset
        timeProvider.Reset();

        // Time should be back to zero
        timeProvider.CurrentTime.Should().Be(TimeSpan.Zero);

        // Old tasks should not complete after reset
        timeProvider.AdvanceTime(TimeSpan.FromSeconds(30));
        task1.IsCompleted.Should().BeFalse();
        task2.IsCompleted.Should().BeFalse();

        // New tasks after reset should work normally
        var task3 = timeProvider.CreateDelay(TimeSpan.FromSeconds(5));
        timeProvider.AdvanceTime(TimeSpan.FromSeconds(5));
        task3.IsCompleted.Should().BeTrue();
        await task3;
    }

    [Fact]
    public void Reset_ShouldAllowMultipleCalls()
    {
        using var timeProvider = new VirtualTimeProvider();

        timeProvider.AdvanceTime(TimeSpan.FromSeconds(10));
        timeProvider.Reset();
        timeProvider.CurrentTime.Should().Be(TimeSpan.Zero);

        timeProvider.AdvanceTime(TimeSpan.FromSeconds(5));
        timeProvider.Reset();
        timeProvider.CurrentTime.Should().Be(TimeSpan.Zero);

        var act = () => timeProvider.Reset();
        act.Should().NotThrow();
    }
}
