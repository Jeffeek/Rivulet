namespace Rivulet.Core;

/// <summary>
///     Defines how errors are handled during parallel async processing.
/// </summary>
public enum ErrorMode
{
    /// <summary>
    ///     Stops processing on the first error. Throws immediately and cancels all remaining work.
    /// </summary>
    FailFast,
    /// <summary>
    ///     Collects all errors and throws an <see cref="AggregateException" /> at the end.
    ///     Processing continues for other items even when errors occur.
    /// </summary>
    CollectAndContinue,
    /// <summary>
    ///     Swallows errors for individual items and continues processing.
    ///     The <see cref="ParallelOptionsRivulet.OnErrorAsync" /> callback is still invoked if provided.
    ///     No exception is thrown at the end.
    /// </summary>
    BestEffort
}