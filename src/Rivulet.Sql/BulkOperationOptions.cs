namespace Rivulet.Sql;

/// <summary>
///     Configuration options for bulk SQL operations.
/// </summary>
public sealed class BulkOperationOptions
{
    /// <summary>
    ///     SQL execution options.
    /// </summary>
    public SqlOptions? SqlOptions { get; init; }

    /// <summary>
    ///     Batch size for bulk operations. Default: 1000.
    ///     Items will be grouped into batches of this size before executing.
    /// </summary>
    public int BatchSize { get; init; } = 1000;

    /// <summary>
    ///     Whether to use transactions for each batch. Default: true.
    ///     When enabled, each batch is wrapped in a transaction that will rollback on error.
    /// </summary>
    public bool UseTransaction { get; init; } = true;

    /// <summary>
    ///     Callback invoked before a batch is executed.
    ///     Provides the batch items and batch number.
    /// </summary>
    public Func<IReadOnlyList<object>, int, ValueTask>? OnBatchStartAsync { get; init; }

    /// <summary>
    ///     Callback invoked after a batch completes successfully.
    ///     Provides the batch items, batch number, and affected row count.
    /// </summary>
    public Func<IReadOnlyList<object>, int, int, ValueTask>? OnBatchCompleteAsync { get; init; }

    /// <summary>
    ///     Callback invoked when a batch fails.
    ///     Provides the batch items, batch number, and exception.
    /// </summary>
    public Func<IReadOnlyList<object>, int, Exception, ValueTask>? OnBatchErrorAsync { get; init; }
}