using System.Runtime.CompilerServices;
using Rivulet.Core;

namespace Rivulet.Pipeline.Internal.Stages;

/// <summary>
/// A stage that filters items in parallel using the provided predicate.
/// </summary>
internal sealed class FilterStage<T>(Func<T, CancellationToken, ValueTask<bool>> predicate,
    StageOptions options,
    string name
) : IInternalPipelineStage, IPipelineStage<T, T>
{
    private readonly Func<T, CancellationToken, ValueTask<bool>> _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));

    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));
    public StageOptions Options { get; } = options ?? throw new ArgumentNullException(nameof(options));

    public async IAsyncEnumerable<T> ExecuteAsync(
        IAsyncEnumerable<T> input,
        PipelineContext context,
        [EnumeratorCancellation]
        CancellationToken cancellationToken
    )
    {
        var parallelOptions = Options.GetMergedOptions(context.DefaultStageOptions);
        var metrics = context.GetOrCreateStageMetrics(Name, 0);

        metrics.Start();

        try
        {
            // Use SelectParallelStreamAsync with a wrapper that returns (item, shouldKeep)
            // Then filter on the result
            await foreach (var (item, keep) in input
                               .SelectParallelStreamAsync(
                                   async (item, ct) => (item, keep: await _predicate(item, ct).ConfigureAwait(false)),
                                   parallelOptions,
                                   cancellationToken)
                               .ConfigureAwait(false))
            {
                metrics.IncrementItemsIn();

                if (!keep)
                    continue;

                metrics.IncrementItemsOut();
                yield return item;
            }
        }
        finally
        {
            metrics.Stop();
        }
    }

    public IAsyncEnumerable<object> ExecuteUntypedAsync(
        IAsyncEnumerable<object> input,
        PipelineContext context,
        CancellationToken cancellationToken
    ) => StageExecutionHelper.ExecuteUntypedAsync<T, T>(
        input,
        typedInput => ExecuteAsync(typedInput, context, cancellationToken),
        cancellationToken);
}
