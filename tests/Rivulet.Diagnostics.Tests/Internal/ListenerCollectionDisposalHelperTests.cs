using System.Diagnostics;
using Rivulet.Diagnostics.Internal;

namespace Rivulet.Diagnostics.Tests.Internal;

public sealed class ListenerCollectionDisposalHelperTests
{
    [Fact]
    public void DisposeAll_WithEmptyCollection_ShouldNotThrow() =>
        ListenerCollectionDisposalHelper.DisposeAll([]);

    [Fact]
    public void DisposeAll_WithSingleListener_ShouldDispose()
    {
        var listener = new TestDisposable();
        var listeners = new List<IDisposable> { listener };

        ListenerCollectionDisposalHelper.DisposeAll(listeners);

        listener.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public void DisposeAll_WithMultipleListeners_ShouldDisposeAll()
    {
        var listener1 = new TestDisposable();
        var listener2 = new TestDisposable();
        var listener3 = new TestDisposable();
        var listeners = new List<IDisposable> { listener1, listener2, listener3 };

        ListenerCollectionDisposalHelper.DisposeAll(listeners);

        listener1.IsDisposed.ShouldBeTrue();
        listener2.IsDisposed.ShouldBeTrue();
        listener3.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public void DisposeAll_WithFailingListener_ShouldSwallowExceptionAndContinue()
    {
        var listener1 = new TestDisposable();
        var listener2 = new TestDisposable { ThrowOnDispose = true };
        var listener3 = new TestDisposable();
        var listeners = new List<IDisposable> { listener1, listener2, listener3 };

        ListenerCollectionDisposalHelper.DisposeAll(listeners);

        listener1.IsDisposed.ShouldBeTrue();
        listener2.IsDisposed.ShouldBeFalse(); // Failed to dispose
        listener3.IsDisposed.ShouldBeTrue();  // Should still try to dispose this one
    }

    [Fact]
    public void DisposeAll_WithAllFailingListeners_ShouldNotThrow()
    {
        var listener1 = new TestDisposable { ThrowOnDispose = true };
        var listener2 = new TestDisposable { ThrowOnDispose = true };
        var listeners = new List<IDisposable> { listener1, listener2 };

        ListenerCollectionDisposalHelper.DisposeAll(listeners);

        // Should complete without throwing despite all failures
        listener1.IsDisposed.ShouldBeFalse();
        listener2.IsDisposed.ShouldBeFalse();
    }

    [Fact]
    public Task DisposeAllAsync_WithEmptyCollection_ShouldNotThrow() =>
        ListenerCollectionDisposalHelper.DisposeAllAsync([]).AsTask();

    [Fact]
    public async Task DisposeAllAsync_WithSingleListener_ShouldDispose()
    {
        var listener = new TestAsyncDisposable();
        var listeners = new List<IAsyncDisposable> { listener };

        await ListenerCollectionDisposalHelper.DisposeAllAsync(listeners);

        listener.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task DisposeAllAsync_WithMultipleListeners_ShouldDisposeAll()
    {
        var listener1 = new TestAsyncDisposable();
        var listener2 = new TestAsyncDisposable();
        var listener3 = new TestAsyncDisposable();
        var listeners = new List<IAsyncDisposable> { listener1, listener2, listener3 };

        await ListenerCollectionDisposalHelper.DisposeAllAsync(listeners);

        listener1.IsDisposed.ShouldBeTrue();
        listener2.IsDisposed.ShouldBeTrue();
        listener3.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task DisposeAllAsync_WithFailingListener_ShouldSwallowExceptionAndContinue()
    {
        var listener1 = new TestAsyncDisposable();
        var listener2 = new TestAsyncDisposable { ThrowOnDispose = true };
        var listener3 = new TestAsyncDisposable();
        var listeners = new List<IAsyncDisposable> { listener1, listener2, listener3 };

        await ListenerCollectionDisposalHelper.DisposeAllAsync(listeners);

        listener1.IsDisposed.ShouldBeTrue();
        listener2.IsDisposed.ShouldBeFalse(); // Failed to dispose
        listener3.IsDisposed.ShouldBeTrue();  // Should still try to dispose this one
    }

    [Fact]
    public async Task DisposeAllAsync_WithAllFailingListeners_ShouldNotThrow()
    {
        var listener1 = new TestAsyncDisposable { ThrowOnDispose = true };
        var listener2 = new TestAsyncDisposable { ThrowOnDispose = true };
        var listeners = new List<IAsyncDisposable> { listener1, listener2 };

        await ListenerCollectionDisposalHelper.DisposeAllAsync(listeners);

        // Should complete without throwing despite all failures
        listener1.IsDisposed.ShouldBeFalse();
        listener2.IsDisposed.ShouldBeFalse();
    }

    [Fact]
    public async Task DisposeAllAsync_WithAsyncOperations_ShouldAwaitAll()
    {
        var listener1 = new TestAsyncDisposable { IsAsync = true };
        var listener2 = new TestAsyncDisposable { IsAsync = true };
        var listeners = new List<IAsyncDisposable> { listener1, listener2 };

        var stopwatch = Stopwatch.StartNew();
        await ListenerCollectionDisposalHelper.DisposeAllAsync(listeners);
        stopwatch.Stop();

        listener1.IsDisposed.ShouldBeTrue();
        listener2.IsDisposed.ShouldBeTrue();
        // Should have awaited the delays (2 * 10ms)
        stopwatch.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(20);
    }

    [Fact]
    public void DisposeAllAsyncBlocking_WithEmptyCollection_ShouldNotThrow() =>
        ListenerCollectionDisposalHelper.DisposeAllAsyncBlocking([]);

    [Fact]
    public void DisposeAllAsyncBlocking_WithSingleListener_ShouldDispose()
    {
        var listener = new TestAsyncDisposable { IsAsync = false };
        var listeners = new List<IAsyncDisposable> { listener };

        ListenerCollectionDisposalHelper.DisposeAllAsyncBlocking(listeners);

        listener.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public void DisposeAllAsyncBlocking_WithMultipleListeners_ShouldDisposeAll()
    {
        var listener1 = new TestAsyncDisposable { IsAsync = false };
        var listener2 = new TestAsyncDisposable { IsAsync = false };
        var listener3 = new TestAsyncDisposable { IsAsync = false };
        var listeners = new List<IAsyncDisposable> { listener1, listener2, listener3 };

        ListenerCollectionDisposalHelper.DisposeAllAsyncBlocking(listeners);

        listener1.IsDisposed.ShouldBeTrue();
        listener2.IsDisposed.ShouldBeTrue();
        listener3.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public void DisposeAllAsyncBlocking_WithAsyncListener_ShouldBlockAndDispose()
    {
        var listener = new TestAsyncDisposable { IsAsync = true };
        var listeners = new List<IAsyncDisposable> { listener };

        var stopwatch = Stopwatch.StartNew();
        ListenerCollectionDisposalHelper.DisposeAllAsyncBlocking(listeners);
        stopwatch.Stop();

        listener.IsDisposed.ShouldBeTrue();
        // Should have blocked for the delay
        stopwatch.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(10);
    }

    [Fact]
    public void DisposeAllAsyncBlocking_WithFailingListener_ShouldSwallowExceptionAndContinue()
    {
        var listener1 = new TestAsyncDisposable { IsAsync = false };
        var listener2 = new TestAsyncDisposable { ThrowOnDispose = true, IsAsync = false };
        var listener3 = new TestAsyncDisposable { IsAsync = false };
        var listeners = new List<IAsyncDisposable> { listener1, listener2, listener3 };

        ListenerCollectionDisposalHelper.DisposeAllAsyncBlocking(listeners);

        listener1.IsDisposed.ShouldBeTrue();
        listener2.IsDisposed.ShouldBeFalse(); // Failed to dispose
        listener3.IsDisposed.ShouldBeTrue();  // Should still try to dispose this one
    }

    [Fact]
    public void DisposeAllAsyncBlocking_WithAllFailingListeners_ShouldNotThrow()
    {
        var listener1 = new TestAsyncDisposable { ThrowOnDispose = true, IsAsync = false };
        var listener2 = new TestAsyncDisposable { ThrowOnDispose = true, IsAsync = false };
        var listeners = new List<IAsyncDisposable> { listener1, listener2 };

        ListenerCollectionDisposalHelper.DisposeAllAsyncBlocking(listeners);

        // Should complete without throwing despite all failures
        listener1.IsDisposed.ShouldBeFalse();
        listener2.IsDisposed.ShouldBeFalse();
    }

    [Fact]
    public void DisposeAllAsyncBlocking_WithCompletedValueTask_ShouldNotBlock()
    {
        var listener1 = new TestAsyncDisposable { IsAsync = false };
        var listener2 = new TestAsyncDisposable { IsAsync = false };
        var listeners = new List<IAsyncDisposable> { listener1, listener2 };

        var stopwatch = Stopwatch.StartNew();
        ListenerCollectionDisposalHelper.DisposeAllAsyncBlocking(listeners);
        stopwatch.Stop();

        listener1.IsDisposed.ShouldBeTrue();
        listener2.IsDisposed.ShouldBeTrue();
        // Should be very fast since ValueTasks are already completed
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(100);
    }

    private sealed class TestDisposable : IDisposable
    {
        public bool IsDisposed { get; private set; }
        public bool ThrowOnDispose { get; init; }

        public void Dispose()
        {
            if (ThrowOnDispose)
                throw new InvalidOperationException("Dispose failed");

            IsDisposed = true;
        }
    }

    private sealed class TestAsyncDisposable : IAsyncDisposable
    {
        public bool IsDisposed { get; private set; }
        public bool ThrowOnDispose { get; init; }
        public bool IsAsync { get; init; } = true;

        public ValueTask DisposeAsync()
        {
            if (ThrowOnDispose)
                throw new InvalidOperationException("DisposeAsync failed");

            IsDisposed = true;

            return IsAsync ? new ValueTask(Task.Delay(10)) : ValueTask.CompletedTask;
        }
    }
}
