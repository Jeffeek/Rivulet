using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace Rivulet.Testing.Tests;

[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public class FakeChannelTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithZeroCounts()
    {
        using var channel = new FakeChannel<int>();

        channel.WriteCount.ShouldBe(0);
        channel.ReadCount.ShouldBe(0);
    }

    [Fact]
    public async Task WriteAsync_ShouldIncrementWriteCount()
    {
        using var channel = new FakeChannel<int>();

        await channel.WriteAsync(42);

        channel.WriteCount.ShouldBe(1);
    }

    [Fact]
    public async Task ReadAsync_ShouldIncrementReadCount()
    {
        using var channel = new FakeChannel<int>();

        await channel.WriteAsync(42);
        var result = await channel.ReadAsync();

        channel.ReadCount.ShouldBe(1);
        result.ShouldBe(42);
    }

    [Fact]
    public async Task MultipleWrites_ShouldTrackCount()
    {
        using var channel = new FakeChannel<int>();

        await channel.WriteAsync(1);
        await channel.WriteAsync(2);
        await channel.WriteAsync(3);

        channel.WriteCount.ShouldBe(3);
    }

    [Fact]
    public async Task MultipleReads_ShouldTrackCount()
    {
        using var channel = new FakeChannel<int>();

        await channel.WriteAsync(1);
        await channel.WriteAsync(2);
        await channel.WriteAsync(3);

        await channel.ReadAsync();
        await channel.ReadAsync();

        channel.ReadCount.ShouldBe(2);
        channel.WriteCount.ShouldBe(3);
    }

    [Fact]
    public async Task Complete_ShouldPreventFurtherWrites()
    {
        using var channel = new FakeChannel<int>();

        channel.Complete();

        var result = await channel.WriteAsync(42);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task Complete_ShouldAllowDrainingRemainingItems()
    {
        using var channel = new FakeChannel<int>();

        await channel.WriteAsync(1);
        await channel.WriteAsync(2);
        channel.Complete();

        var result1 = await channel.ReadAsync();
        var result2 = await channel.ReadAsync();

        result1.ShouldBe(1);
        result2.ShouldBe(2);
        channel.ReadCount.ShouldBe(2);
    }

    [Fact]
    public async Task WaitToReadAsync_ShouldReturnFalseAfterCompletion()
    {
        using var channel = new FakeChannel<int>();

        await channel.WriteAsync(1);
        channel.Complete();

        await channel.ReadAsync();

        var canRead = await channel.Reader.WaitToReadAsync();

        canRead.ShouldBeFalse();
    }

    [Fact]
    public void ResetCounters_ShouldResetCounters()
    {
        using var channel = new FakeChannel<int>();

        channel.Writer.TryWrite(1);
        channel.Writer.TryWrite(2);

        channel.ResetCounters();

        channel.WriteCount.ShouldBe(0);
        channel.ReadCount.ShouldBe(0);
    }

    [Fact]
    public async Task ResetCounters_ShouldNotAffectQueuedItems()
    {
        using var channel = new FakeChannel<int>();

        await channel.WriteAsync(42);
        channel.ResetCounters();

        var result = await channel.ReadAsync();

        result.ShouldBe(42);
        channel.ReadCount.ShouldBe(1);
    }

    [Fact]
    public async Task ConcurrentWrites_ShouldTrackAllWrites()
    {
        using var channel = new FakeChannel<int>();

        var writeTasks = Enumerable.Range(1, 100)
            .Select(i => Task.Run(async () => await channel.WriteAsync(i)))
            .ToArray();

        await Task.WhenAll(writeTasks);

        channel.WriteCount.ShouldBe(100);
    }

    [Fact]
    public async Task ConcurrentReads_ShouldTrackAllReads()
    {
        using var channel = new FakeChannel<int>();

        for (var i = 0; i < 100; i++)
        {
            await channel.WriteAsync(i);
        }

        var readTasks = Enumerable.Range(0, 100)
            .Select(_ => Task.Run(async () => await channel.ReadAsync()))
            .ToArray();

        await Task.WhenAll(readTasks);

        channel.ReadCount.ShouldBe(100);
    }

    [Fact]
    public async Task WithBoundedCapacity_ShouldRespectCapacity()
    {
        using var channel = new FakeChannel<int>(boundedCapacity: 2);

        await channel.WriteAsync(1);
        await channel.WriteAsync(2);

        var writeTask = channel.WriteAsync(3);

        await Task.Delay(100);
        writeTask.IsCompleted.ShouldBeFalse();

        await channel.ReadAsync();
        await writeTask;

        channel.WriteCount.ShouldBe(3);
        channel.ReadCount.ShouldBe(1);
    }

    [Fact]
    public async Task UnboundedChannel_ShouldHandleLargeVolume()
    {
        using var channel = new FakeChannel<int>();

        for (var i = 0; i < 10000; i++)
        {
            await channel.WriteAsync(i);
        }

        channel.WriteCount.ShouldBe(10000);

        for (var i = 0; i < 10000; i++)
        {
            await channel.ReadAsync();
        }

        channel.ReadCount.ShouldBe(10000);
    }

    [Fact]
    public async Task Complete_WithException_ShouldThrowOnRead()
    {
        using var channel = new FakeChannel<int>();

        var exception = new InvalidOperationException("Test error");
        channel.Complete(exception);

        var act = async () => await channel.ReadAsync();

        await act.ShouldThrowAsync<ChannelClosedException>();
    }

    [Fact]
    public void Dispose_ShouldCompleteWriter()
    {
        var channel = new FakeChannel<int>();

        channel.Dispose();

        var result = channel.Writer.TryWrite(42);

        result.ShouldBeFalse();
    }

    [Fact]
    public void Dispose_ShouldBeIdempotent()
    {
        var channel = new FakeChannel<int>();

        channel.Dispose();
        var act = () => channel.Dispose();

        act.ShouldNotThrow();
    }

    [Fact]
    public async Task ReadAllAsync_ShouldEnumerateAllItems()
    {
        using var channel = new FakeChannel<int>();

        await channel.WriteAsync(1);
        await channel.WriteAsync(2);
        await channel.WriteAsync(3);
        channel.Complete();

        var results = new List<int>();
        await foreach (var item in channel.Reader.ReadAllAsync())
        {
            results.Add(item);
        }

        results.ShouldBe(new[] { 1, 2, 3 });
    }

    [Fact]
    public void DirectWriterAccess_ShouldNotIncrementCount()
    {
        using var channel = new FakeChannel<int>();

        channel.Writer.TryWrite(1);
        channel.Writer.TryWrite(2);

        channel.WriteCount.ShouldBe(0);
    }

    [Fact]
    public async Task DirectReaderAccess_ShouldNotIncrementCount()
    {
        using var channel = new FakeChannel<int>();

        await channel.WriteAsync(1);
        channel.Reader.TryRead(out _);

        channel.ReadCount.ShouldBe(0);
        channel.WriteCount.ShouldBe(1);
    }

    [Fact]
    public async Task WriteAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        var channel = new FakeChannel<int>();
        channel.Dispose();

        var act = async () => await channel.WriteAsync(42);

        await act.ShouldThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task ReadAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        var channel = new FakeChannel<int>();
        await channel.WriteAsync(42);
        channel.Dispose();

        var act = async () => await channel.ReadAsync();

        await act.ShouldThrowAsync<ObjectDisposedException>();
    }
}
