using FluentAssertions;
using Rivulet.Core.Observability;

namespace Rivulet.Core.Tests;

/// <summary>
/// Tests for internal ProgressTracker behavior that requires direct access.
/// These tests access internal members via InternalsVisibleTo.
/// </summary>
public class ProgressTrackerInternalTests
{
    [Fact]
    public void ProgressTracker_DoubleDispose_DoesNotThrow()
    {
        var options = new ProgressOptions
        {
            ReportInterval = TimeSpan.FromMilliseconds(50),
            OnProgress = _ => ValueTask.CompletedTask
        };

        using var cts = new CancellationTokenSource();
        var tracker = new ProgressTracker(100, options, cts.Token);

        tracker.IncrementStarted();
        tracker.IncrementCompleted();

        tracker.Dispose();

        var act = () => tracker.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void ProgressTracker_WithNullCallback_DoesNotReport()
    {
        var options = new ProgressOptions
        {
            ReportInterval = TimeSpan.FromMilliseconds(10),
            OnProgress = null
        };

        using var cts = new CancellationTokenSource();
        var tracker = new ProgressTracker(50, options, cts.Token);

        tracker.IncrementStarted();
        tracker.IncrementCompleted();

        Thread.Sleep(50);

        var act = () => tracker.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task ProgressTracker_RapidCancellation_DisposesCleanly()
    {
        var options = new ProgressOptions
        {
            ReportInterval = TimeSpan.FromMilliseconds(10),
            OnProgress = _ => ValueTask.CompletedTask
        };

        using var cts = new CancellationTokenSource();
        var tracker = new ProgressTracker(100, options, cts.Token);

        tracker.IncrementStarted();

        await cts.CancelAsync();

        var act = () => tracker.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void ProgressTracker_ThrowingCallback_DoesNotPropagateException()
    {
        var callbackCount = 0;
        var options = new ProgressOptions
        {
            ReportInterval = TimeSpan.FromMilliseconds(10),
            OnProgress = _ =>
            {
                Interlocked.Increment(ref callbackCount);
                throw new InvalidOperationException("Test exception");
            }
        };

        using var cts = new CancellationTokenSource();
        var tracker = new ProgressTracker(50, options, cts.Token);

        tracker.IncrementStarted();
        tracker.IncrementCompleted();

        Thread.Sleep(50);

        var act = () => tracker.Dispose();
        act.Should().NotThrow();

        callbackCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ProgressTracker_CancellationDuringDispose_HandlesGracefully()
    {
        var reportCount = 0;
        var options = new ProgressOptions
        {
            ReportInterval = TimeSpan.FromMilliseconds(5),
            OnProgress = _ =>
            {
                Interlocked.Increment(ref reportCount);
                return ValueTask.CompletedTask;
            }
        };

        using var cts = new CancellationTokenSource();
        var tracker = new ProgressTracker(100, options, cts.Token);

        tracker.IncrementStarted();
        tracker.IncrementCompleted();

        await Task.Delay(20, CancellationToken.None);

        await cts.CancelAsync();
        var act = () => tracker.Dispose();
        act.Should().NotThrow();

        reportCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ProgressTracker_StreamingMode_NullTotalItems_CalculatesCorrectly()
    {
        ProgressSnapshot? lastSnapshot = null;
        var options = new ProgressOptions
        {
            ReportInterval = TimeSpan.FromMilliseconds(10),
            OnProgress = snapshot =>
            {
                lastSnapshot = snapshot;
                return ValueTask.CompletedTask;
            }
        };

        using var cts = new CancellationTokenSource();
        var tracker = new ProgressTracker(null, options, cts.Token);

        for (var i = 0; i < 10; i++)
        {
            tracker.IncrementStarted();
            tracker.IncrementCompleted();
        }

        Thread.Sleep(50);
        tracker.Dispose();

        lastSnapshot.Should().NotBeNull();
        lastSnapshot!.TotalItems.Should().BeNull();
        lastSnapshot.PercentComplete.Should().BeNull();
        lastSnapshot.EstimatedTimeRemaining.Should().BeNull();
        lastSnapshot.ItemsCompleted.Should().Be(10);
    }

    [Fact]
    public void ProgressTracker_ErrorCounting_TracksCorrectly()
    {
        ProgressSnapshot? lastSnapshot = null;
        var options = new ProgressOptions
        {
            ReportInterval = TimeSpan.FromMilliseconds(10),
            OnProgress = snapshot =>
            {
                lastSnapshot = snapshot;
                return ValueTask.CompletedTask;
            }
        };

        using var cts = new CancellationTokenSource();
        var tracker = new ProgressTracker(20, options, cts.Token);

        for (var i = 0; i < 15; i++)
        {
            tracker.IncrementStarted();
            if (i % 3 == 0)
                tracker.IncrementErrors();
            else
                tracker.IncrementCompleted();
        }

        Thread.Sleep(50);
        tracker.Dispose();

        lastSnapshot.Should().NotBeNull();
        lastSnapshot!.ErrorCount.Should().Be(5);
        lastSnapshot.ItemsCompleted.Should().Be(10);
        lastSnapshot.ItemsStarted.Should().Be(15);
    }
}
