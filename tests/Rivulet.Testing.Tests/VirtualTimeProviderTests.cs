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
    public async Task AdvanceTimeAsync_ShouldUpdateCurrentTime()
    {
        using var timeProvider = new VirtualTimeProvider();

        await timeProvider.AdvanceTimeAsync(TimeSpan.FromSeconds(5));

        timeProvider.CurrentTime.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AdvanceTimeAsync_ShouldBeAdditive()
    {
        using var timeProvider = new VirtualTimeProvider();

        await timeProvider.AdvanceTimeAsync(TimeSpan.FromSeconds(3));
        await timeProvider.AdvanceTimeAsync(TimeSpan.FromSeconds(2));

        timeProvider.CurrentTime.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void DelayAsync_ShouldCompleteImmediatelyWithoutAdvanceTime()
    {
        using var timeProvider = new VirtualTimeProvider();

        var delayTask = timeProvider.DelayAsync(TimeSpan.FromSeconds(10));

        delayTask.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task DelayAsync_ShouldCompleteWhenTimeAdvances()
    {
        using var timeProvider = new VirtualTimeProvider();

        var delayTask = timeProvider.DelayAsync(TimeSpan.FromSeconds(10));

        await timeProvider.AdvanceTimeAsync(TimeSpan.FromSeconds(10));

        delayTask.IsCompleted.Should().BeTrue();
        await delayTask;
    }

    [Fact]
    public async Task DelayAsync_ShouldCompleteInOrder()
    {
        using var timeProvider = new VirtualTimeProvider();

        var delay5 = timeProvider.DelayAsync(TimeSpan.FromSeconds(5));
        var delay10 = timeProvider.DelayAsync(TimeSpan.FromSeconds(10));
        var delay3 = timeProvider.DelayAsync(TimeSpan.FromSeconds(3));

        await timeProvider.AdvanceTimeAsync(TimeSpan.FromSeconds(3));
        delay3.IsCompleted.Should().BeTrue();
        delay5.IsCompleted.Should().BeFalse();
        delay10.IsCompleted.Should().BeFalse();

        await timeProvider.AdvanceTimeAsync(TimeSpan.FromSeconds(2));
        delay5.IsCompleted.Should().BeTrue();
        delay10.IsCompleted.Should().BeFalse();

        await timeProvider.AdvanceTimeAsync(TimeSpan.FromSeconds(5));
        delay10.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task MultipleDelays_ShouldAllCompleteAfterSufficientAdvancement()
    {
        using var timeProvider = new VirtualTimeProvider();

        var delays = Enumerable.Range(1, 10)
            .Select(i => timeProvider.DelayAsync(TimeSpan.FromSeconds(i)))
            .ToList();

        await timeProvider.AdvanceTimeAsync(TimeSpan.FromSeconds(10));

        foreach (var delay in delays)
        {
            delay.IsCompleted.Should().BeTrue();
            await delay;
        }
    }

    [Fact]
    public async Task Dispose_ShouldPreventNewDelays()
    {
        var timeProvider = new VirtualTimeProvider();
        timeProvider.Dispose();

        var act = async () => await timeProvider.DelayAsync(TimeSpan.FromSeconds(10));

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task DelayAsync_WithZeroDuration_ShouldCompleteImmediately()
    {
        using var timeProvider = new VirtualTimeProvider();

        var delayTask = timeProvider.DelayAsync(TimeSpan.Zero);

        await timeProvider.AdvanceTimeAsync(TimeSpan.Zero);

        delayTask.IsCompleted.Should().BeTrue();
        await delayTask;
    }

    [Fact]
    public async Task DelayAsync_WithNegativeDuration_ShouldCompleteImmediately()
    {
        using var timeProvider = new VirtualTimeProvider();

        var delayTask = timeProvider.DelayAsync(TimeSpan.FromSeconds(-5));

        await timeProvider.AdvanceTimeAsync(TimeSpan.Zero);

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
            await timeProvider.DelayAsync(TimeSpan.FromSeconds(5));
            executionOrder.Add(1);
        });

        var task2 = Task.Run(async () =>
        {
            await timeProvider.DelayAsync(TimeSpan.FromSeconds(3));
            executionOrder.Add(2);
        });

        var task3 = Task.Run(async () =>
        {
            await timeProvider.DelayAsync(TimeSpan.FromSeconds(7));
            executionOrder.Add(3);
        });

        await Task.Delay(50);

        await timeProvider.AdvanceTimeAsync(TimeSpan.FromSeconds(10));

        await Task.WhenAll(task1, task2, task3);

        executionOrder.Should().Equal(2, 1, 3);
    }

    [Fact]
    public async Task Dispose_ShouldCancelAllPendingDelays()
    {
        var timeProvider = new VirtualTimeProvider();

        var delay1 = timeProvider.DelayAsync(TimeSpan.FromSeconds(10));
        var delay2 = timeProvider.DelayAsync(TimeSpan.FromSeconds(20));

        timeProvider.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => delay1);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => delay2);
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
    public async Task DelayAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        var timeProvider = new VirtualTimeProvider();
        timeProvider.Dispose();

        var act = async () => await timeProvider.DelayAsync(TimeSpan.FromSeconds(1));

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task AdvanceTimeAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        var timeProvider = new VirtualTimeProvider();
        timeProvider.Dispose();

        var act = async () => await timeProvider.AdvanceTimeAsync(TimeSpan.FromSeconds(1));

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task AdvanceTimeAsync_WithLargeTimeSpan_ShouldWork()
    {
        using var timeProvider = new VirtualTimeProvider();

        var delay = timeProvider.DelayAsync(TimeSpan.FromDays(365));

        await timeProvider.AdvanceTimeAsync(TimeSpan.FromDays(365));

        delay.IsCompleted.Should().BeTrue();
        await delay;
    }

    [Fact]
    public async Task ConcurrentDelays_ShouldAllCompleteCorrectly()
    {
        using var timeProvider = new VirtualTimeProvider();

        var tasks = Enumerable.Range(1, 100)
            .Select(i => Task.Run(async () =>
            {
                await timeProvider.DelayAsync(TimeSpan.FromMilliseconds(i * 10));
                return i;
            }))
            .ToList();

        await Task.Delay(100);

        await timeProvider.AdvanceTimeAsync(TimeSpan.FromSeconds(10));

        var results = await Task.WhenAll(tasks);

        results.Should().HaveCount(100);
        results.Should().OnlyContain(i => i >= 1 && i <= 100);
    }
}
