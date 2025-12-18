namespace Rivulet.Testing;

/// <summary>
///     Injects chaos (failures, delays, timeouts) for resilience testing.
/// </summary>
public sealed class ChaosInjector
{
    private readonly TimeSpan? _artificialDelay;
    private readonly double _failureRate;

    /// <summary>
    ///     Initializes a new chaos injector.
    /// </summary>
    /// <param name="failureRate">Probability of failure (0.0 to 1.0).</param>
    /// <param name="artificialDelay">Optional delay to inject before each operation.</param>
    public ChaosInjector(double failureRate = 0.1, TimeSpan? artificialDelay = null)
    {
        if (failureRate is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(failureRate), "Failure rate must be between 0 and 1");

        _failureRate = failureRate;
        _artificialDelay = artificialDelay;
    }

    /// <summary>
    ///     Executes an action with chaos injection.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        if (_artificialDelay.HasValue)
            await Task.Delay(_artificialDelay.Value, cancellationToken).ConfigureAwait(false);

        return ShouldFail()
            ? throw new ChaosException("Chaos injected failure")
            : await action().ConfigureAwait(false);
    }

    /// <summary>
    ///     Determines if the current operation should fail based on the failure rate.
    /// </summary>
    public bool ShouldFail() => Random.Shared.NextDouble() < _failureRate;
}

/// <summary>
///     Exception thrown by chaos injector.
/// </summary>
public sealed class ChaosException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ChaosException" /> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ChaosException(string message)
        : base(message) { }
}