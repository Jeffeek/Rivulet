using System.Diagnostics.CodeAnalysis;
using Rivulet.Base.Tests;
using Rivulet.Core.Observability;

namespace Rivulet.Core.Tests;

/// <summary>
///     Tests for internal ProgressTracker behavior that requires direct access.
///     These tests access internal members via InternalsVisibleTo.
/// </summary>
[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public sealed class ProgressTrackerInternalTests
{
    [Fact]
    public async Task ProgressTracker_DoubleDispose_DoesNotThrow()
    {
        var options = new ProgressOptions
            { ReportInterval = TimeSpan.FromMilliseconds(50), OnProgress = static _ => ValueTask.CompletedTask };

        using var cts = new CancellationTokenSource();
        var tracker = new ProgressTracker(100, options, cts.Token);

        try
        {
            tracker.IncrementStarted();
            tracker.IncrementCompleted();

            var act = async () => await tracker.DisposeAsync();
            await act.ShouldNotThrowAsync();
        }
        finally
        {
            await tracker.DisposeAsync();
        }
    }

    [Fact]
    public async Task ProgressTracker_WithNullCallback_DoesNotReport()
    {
        var options = new ProgressOptions { ReportInterval = TimeSpan.FromMilliseconds(10), OnProgress = null };

        using var cts = new CancellationTokenSource();
        var tracker = new ProgressTracker(50, options, cts.Token);

        tracker.IncrementStarted();
        tracker.IncrementCompleted();

        await Task.Delay(50, CancellationToken.None);

        var act = async () => await tracker.DisposeAsync();
        await act.ShouldNotThrowAsync();
    }

    [Fact]
    public async Task ProgressTracker_RapidCancellation_DisposesCleanly()
    {
        var options = new ProgressOptions
            { ReportInterval = TimeSpan.FromMilliseconds(10), OnProgress = static _ => ValueTask.CompletedTask };

        using var cts = new CancellationTokenSource();
        var tracker = new ProgressTracker(100, options, cts.Token);

        tracker.IncrementStarted();

        await cts.CancelAsync();

        var act = async () => await tracker.DisposeAsync();
        await act.ShouldNotThrowAsync();
    }

    [Fact]
    public async Task ProgressTracker_ThrowingCallback_DoesNotPropagateException()
    {
        var callbackFired = new TaskCompletionSource<bool>();
        var callbackCount = 0;
        var options = new ProgressOptions
        {
            ReportInterval = TimeSpan.FromMilliseconds(10),
            OnProgress = _ =>
            {
                Interlocked.Increment(ref callbackCount);
                callbackFired.TrySetResult(true);
                throw new InvalidOperationException("Test exception");
            }
        };

        using var cts = new CancellationTokenSource();
        var tracker = new ProgressTracker(50, options, cts.Token);

        tracker.IncrementStarted();
        tracker.IncrementCompleted();

        // Wait for callback to actually fire (with timeout for safety)
        var completedInTime = await Task.WhenAny(callbackFired.Task, Task.Delay(500, CancellationToken.None)) ==
                              callbackFired.Task;
        completedInTime.ShouldBeTrue("callback should fire within 500ms");

        var act = async () => await tracker.DisposeAsync();
        await act.ShouldNotThrowAsync();

        callbackCount.ShouldBeGreaterThan(0);
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
        var act = async () => await tracker.DisposeAsync();
        await act.ShouldNotThrowAsync();

        reportCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ProgressTracker_StreamingMode_NullTotalItems_CalculatesCorrectly()
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

        try
        {
            for (var i = 0; i < 10; i++)
            {
                tracker.IncrementStarted();
                tracker.IncrementCompleted();
            }

            // Poll for snapshot to be captured (timer fires every 10ms but may be delayed in CI)
            await DeadlineExtensions.ApplyDeadlineAsync(
                DateTime.UtcNow.AddMilliseconds(500),
                static () => Task.Delay(20, CancellationToken.None),
                () => lastSnapshot == null);

            lastSnapshot.ShouldNotBeNull();
            lastSnapshot!.TotalItems.ShouldBeNull();
            lastSnapshot.PercentComplete.ShouldBeNull();
            lastSnapshot.EstimatedTimeRemaining.ShouldBeNull();
            lastSnapshot.ItemsCompleted.ShouldBe(10);
        }
        finally
        {
            await tracker.DisposeAsync();
        }
    }

    [Fact]
    public async Task ProgressTracker_ErrorCounting_TracksCorrectly()
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

        try
        {
            // Small delay before starting to allow timer to initialize
            // This ensures the background reporter task has started
            await Task.Delay(20, CancellationToken.None);

            for (var i = 0; i < 15; i++)
            {
                tracker.IncrementStarted();
                if (i % 3 == 0)
                    tracker.IncrementErrors();
                else
                    tracker.IncrementCompleted();

                // Add delay after every iteration to ensure timer has time to fire
                // Report interval is 10ms, so 15ms delay ensures timer can capture each state
                // This is especially important on Windows where timer resolution is ~15ms
                await Task.Delay(30, CancellationToken.None);
            }

            // Disposal completes after the loop, ensure memory visibility
            // Using Task.Yield() to force a context switch, ensuring all memory writes are globally visible
            await Task.Yield();
            await Task.Delay(200, CancellationToken.None);

            // Poll for snapshot to capture all errors (timer fires every 10ms but may be delayed in CI)
            await DeadlineExtensions.ApplyDeadlineAsync(
                DateTime.UtcNow.AddMilliseconds(1500),
                static () => Task.Delay(20, CancellationToken.None),
                () => lastSnapshot is not { ErrorCount: 5 });

            lastSnapshot.ShouldNotBeNull();
            lastSnapshot!.ErrorCount.ShouldBe(5);
            lastSnapshot.ItemsCompleted.ShouldBe(10);
            lastSnapshot.ItemsStarted.ShouldBe(15);
        }
        finally
        {
            await tracker.DisposeAsync();
        }
    }
}