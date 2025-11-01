namespace Rivulet.Testing;

/// <summary>
/// Helper for asserting concurrency behavior in tests.
/// </summary>
public sealed class ConcurrencyAsserter
{
    private int _currentConcurrency;
    private int _maxConcurrency;
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// Gets the maximum concurrency observed.
    /// </summary>
    public int MaxConcurrency => _maxConcurrency;

    /// <summary>
    /// Gets the current concurrency level.
    /// </summary>
    public int CurrentConcurrency => _currentConcurrency;

    /// <summary>
    /// Tracks entry into a concurrent operation.
    /// </summary>
    public async Task<IDisposable> EnterAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var current = Interlocked.Increment(ref _currentConcurrency);
            var max = _maxConcurrency;
            while (current > max)
            {
                var original = Interlocked.CompareExchange(ref _maxConcurrency, current, max);
                if (original == max) break;
                max = _maxConcurrency;
            }
        }
        finally
        {
            _lock.Release();
        }

        return new ConcurrencyScope(this);
    }

    /// <summary>
    /// Resets the concurrency tracking.
    /// </summary>
    public void Reset()
    {
        _currentConcurrency = 0;
        _maxConcurrency = 0;
    }

    private sealed class ConcurrencyScope : IDisposable
    {
        private readonly ConcurrencyAsserter _asserter;
        private bool _disposed;

        public ConcurrencyScope(ConcurrencyAsserter asserter)
        {
            _asserter = asserter;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Interlocked.Decrement(ref _asserter._currentConcurrency);
        }
    }
}
