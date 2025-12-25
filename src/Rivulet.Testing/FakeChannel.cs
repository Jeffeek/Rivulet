using System.Threading.Channels;

namespace Rivulet.Testing;

/// <summary>
///     Fake channel for testing with controllable behavior and capacity tracking.
/// </summary>
public sealed class FakeChannel<T> : IDisposable
{
    private readonly Channel<T> _innerChannel;
    private bool _disposed;
    private int _readCount;
    private int _writeCount;

    /// <summary>
    ///     Initializes a new fake channel with optional bounded capacity.
    /// </summary>
    public FakeChannel(int? boundedCapacity = null) =>
        _innerChannel = boundedCapacity.HasValue
            ? Channel.CreateBounded<T>(boundedCapacity.Value)
            : Channel.CreateUnbounded<T>();

    /// <summary>
    ///     Gets the channel writer.
    /// </summary>
    // ReSharper disable once MemberCanBeInternal
    public ChannelWriter<T> Writer => _innerChannel.Writer;

    /// <summary>
    ///     Gets the channel reader.
    /// </summary>
    public ChannelReader<T> Reader => _innerChannel.Reader;

    /// <summary>
    ///     Gets the total number of items written to the channel.
    /// </summary>
    public int WriteCount => _writeCount;

    /// <summary>
    ///     Gets the total number of items read from the channel.
    /// </summary>
    public int ReadCount => _readCount;

    /// <summary>
    ///     Disposes the fake channel and completes the writer.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        Writer.TryComplete();
    }

    /// <summary>
    ///     Writes an item to the channel and increments the write counter.
    /// </summary>
    public async ValueTask<bool> WriteAsync(T item, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(FakeChannel<T>));

        var result = await Writer.WaitToWriteAsync(cancellationToken).ConfigureAwait(false);
        if (!result) return result;

        await Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _writeCount);
        return result;
    }

    /// <summary>
    ///     Reads an item from the channel and increments the read counter.
    /// </summary>
    // ReSharper disable once MemberCanBeInternal
    public async ValueTask<T> ReadAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(FakeChannel<T>));

        var item = await Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _readCount);
        return item;
    }

    /// <summary>
    ///     Completes the channel writer.
    /// </summary>
    public void Complete(Exception? exception = null) =>
        Writer.Complete(exception);

    /// <summary>
    ///     Resets the read and write counters using thread-safe operations.
    /// </summary>
    // ReSharper disable once MemberCanBeInternal
    public void ResetCounters()
    {
        Interlocked.Exchange(ref _writeCount, 0);
        Interlocked.Exchange(ref _readCount, 0);
    }
}
