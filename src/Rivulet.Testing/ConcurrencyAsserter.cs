namespace Rivulet.Testing;

/// <summary>
/// Helper for asserting concurrency behavior in tests.
/// </summary>
public sealed class ConcurrencyAsserter
{
    private int _currentConcurrency;
    private int _maxConcurrency;

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
    public IDisposable Enter()
    {
        // Increment current concurrency using thread-safe Interlocked operation
        var current = Interlocked.Increment(ref _currentConcurrency);

        // Update max concurrency if current exceeds it (using lock-free compare-exchange loop)
        var max = _maxConcurrency;
        while (current > max)
        {
            var original = Interlocked.CompareExchange(ref _maxConcurrency, current, max);
            if (original == max) break; // Successfully updated
            max = _maxConcurrency; // Retry with latest value
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

    private sealed class ConcurrencyScope(ConcurrencyAsserter asserter) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Interlocked.Decrement(ref asserter._currentConcurrency);
        }
    }
}
