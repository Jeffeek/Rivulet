using System.Runtime.CompilerServices;
using Rivulet.Core;

namespace Rivulet.Pipeline.Internal.Stages;

/// <summary>
/// A stage that executes a side effect on each item without transforming it.
/// </summary>
internal sealed class TapStage<T>(Func<T, CancellationToken, ValueTask> action,
    StageOptions options,
    string name
) : IInternalPipelineStage, IPipelineStage<T, T>
{
    private readonly Func<T, CancellationToken, ValueTask> _action = action ?? throw new ArgumentNullException(nameof(action));

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
            // Execute action and return the same item
            await foreach (var item in input
                               .SelectParallelStreamAsync(
                                   async (item, ct) =>
                                   {
                                       await _action(item, ct).ConfigureAwait(false);
                                       return item;
                                   },
                                   parallelOptions,
                                   cancellationToken)
                               .ConfigureAwait(false))
            {
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
