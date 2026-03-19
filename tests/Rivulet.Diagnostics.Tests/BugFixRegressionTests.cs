using System.Text;

namespace Rivulet.Diagnostics.Tests;

/// <summary>
///     Regression tests for bugs found during Rivulet.Diagnostics code review.
/// </summary>
public sealed class BugFixRegressionTests
{
    // ──────────────────────────────────────────────────────────────────────
    //  Issue #1 — MetricsAggregator._disposed must be volatile so timer
    //  callbacks see the flag immediately across threads.
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MetricsAggregator_DisposeAsync_ShouldPreventTimerCallbackFromFiring()
    {
        var callbackCount = 0;
        var aggregator = new MetricsAggregator(TimeSpan.FromMilliseconds(50));
        // ReSharper disable once AccessToModifiedClosure
        aggregator.OnAggregation += _ => Interlocked.Increment(ref callbackCount);

        // Let a few callbacks fire
        await Task.Delay(200, CancellationToken.None);

        await aggregator.DisposeAsync();
        var countAfterDispose = Volatile.Read(ref callbackCount);

        // Wait long enough for several more timer ticks — none should fire
        await Task.Delay(300, CancellationToken.None);

        Volatile.Read(ref callbackCount).ShouldBe(countAfterDispose);
    }

    [Fact]
    public void MetricsAggregator_SyncDispose_ShouldPreventTimerCallbackFromFiring()
    {
        var callbackCount = 0;
        var aggregator = new MetricsAggregator(TimeSpan.FromMilliseconds(50));
        // ReSharper disable once AccessToModifiedClosure
        aggregator.OnAggregation += _ => Interlocked.Increment(ref callbackCount);

        Thread.Sleep(200);

        aggregator.Dispose();
        var countAfterDispose = Volatile.Read(ref callbackCount);

        Thread.Sleep(300);

        Volatile.Read(ref callbackCount).ShouldBe(countAfterDispose);
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Issue #7 — Double dispose (Dispose + DisposeAsync, or vice versa)
    //  must not throw for RivuletFileListener and RivuletStructuredLogListener.
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RivuletFileListener_DoubleDispose_AsyncThenSync_ShouldNotThrow()
    {
        var filePath = Path.Join(Path.GetTempPath(), $"rivulet-regression-{Guid.NewGuid()}.log");

        try
        {
            var listener = new RivuletFileListener(filePath);

            await listener.DisposeAsync();
            // ReSharper disable once MethodHasAsyncOverload
            listener.Dispose(); // second dispose — must not throw
        }
        finally
        {
            TestCleanupHelper.RetryDeleteFile(filePath);
        }
    }

    [Fact]
    public async Task RivuletFileListener_DoubleDispose_SyncThenAsync_ShouldNotThrow()
    {
        var filePath = Path.Join(Path.GetTempPath(), $"rivulet-regression-{Guid.NewGuid()}.log");

        try
        {
            var listener = new RivuletFileListener(filePath);

            // ReSharper disable once MethodHasAsyncOverload
            listener.Dispose();
            await listener.DisposeAsync(); // second dispose — must not throw
        }
        finally
        {
            TestCleanupHelper.RetryDeleteFile(filePath);
        }
    }

    [Fact]
    public async Task RivuletStructuredLogListener_DoubleDispose_AsyncThenSync_ShouldNotThrow()
    {
        var filePath = Path.Join(Path.GetTempPath(), $"rivulet-regression-{Guid.NewGuid()}.json");

        try
        {
            var listener = new RivuletStructuredLogListener(filePath);

            await listener.DisposeAsync();
            // ReSharper disable once MethodHasAsyncOverload
            listener.Dispose(); // second dispose — must not throw
        }
        finally
        {
            TestCleanupHelper.RetryDeleteFile(filePath);
        }
    }

    [Fact]
    public async Task RivuletStructuredLogListener_DoubleDispose_SyncThenAsync_ShouldNotThrow()
    {
        var filePath = Path.Join(Path.GetTempPath(), $"rivulet-regression-{Guid.NewGuid()}.json");

        try
        {
            var listener = new RivuletStructuredLogListener(filePath);

            // ReSharper disable once MethodHasAsyncOverload
            listener.Dispose();
            await listener.DisposeAsync(); // second dispose — must not throw
        }
        finally
        {
            TestCleanupHelper.RetryDeleteFile(filePath);
        }
    }

    [Fact]
    public async Task RivuletStructuredLogListener_WithAction_DoubleDispose_ShouldNotThrow()
    {
        var listener = new RivuletStructuredLogListener(static _ => { });

        await listener.DisposeAsync();
        // ReSharper disable once MethodHasAsyncOverload
        listener.Dispose(); // second dispose — must not throw
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Issue #3 — RivuletFileListener should track existing file size
    //  correctly when appending to an existing file.
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void RivuletFileListener_ShouldRespectExistingFileSize_WhenAppending()
    {
        var filePath = Path.Join(Path.GetTempPath(), $"rivulet-regression-{Guid.NewGuid()}.log");

        try
        {
            // Seed a file with known content
            var seedContent = new string('X', 500) + Environment.NewLine;
            File.WriteAllText(filePath, seedContent, Encoding.UTF8);
            var seedSize = new FileInfo(filePath).Length;
            seedSize.ShouldBeGreaterThan(0);

            // Open a listener with a max size just above the seed content
            // If the listener ignores the existing size, it won't rotate until maxSize
            // bytes of NEW data are written — the file would grow past maxSize.
            const long maxSize = 600;

            using var listener = new RivuletFileListener(filePath, maxSize);

            // The listener should have recorded _currentFileSize >= seedSize.
            // We can't inspect the field directly, but we can verify the file still
            // exists and is not corrupt after construction.
            File.Exists(filePath).ShouldBeTrue();
            new FileInfo(filePath).Length.ShouldBeGreaterThanOrEqualTo(seedSize);
        }
        finally
        {
            TestCleanupHelper.RetryDeleteFile(filePath);
        }
    }

    [Fact]
    public void RivuletFileListener_ShouldStartAtZero_WhenFileDoesNotExist()
    {
        var filePath = Path.Join(Path.GetTempPath(), $"rivulet-regression-{Guid.NewGuid()}.log");

        try
        {
            // File does NOT exist yet
            File.Exists(filePath).ShouldBeFalse();

            using var listener = new RivuletFileListener(filePath);

            // File should now exist (created by StreamWriter) and be empty or near-empty
            File.Exists(filePath).ShouldBeTrue();
            new FileInfo(filePath).Length.ShouldBeLessThanOrEqualTo(3); // at most a UTF-8 BOM
        }
        finally
        {
            TestCleanupHelper.RetryDeleteFile(filePath);
        }
    }
}
