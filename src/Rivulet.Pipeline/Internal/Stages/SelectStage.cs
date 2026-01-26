using System.Runtime.CompilerServices;
using Rivulet.Core;

namespace Rivulet.Pipeline.Internal.Stages;

/// <summary>
/// A stage that transforms each item in parallel using the provided selector.
/// </summary>
internal sealed class SelectStage<TIn, TOut>(Func<TIn, CancellationToken, ValueTask<TOut>> selector,
    StageOptions options,
    string name
) : IInternalPipelineStage, IPipelineStage<TIn, TOut>
{
    private readonly Func<TIn, CancellationToken, ValueTask<TOut>> _selector = selector ?? throw new ArgumentNullException(nameof(selector));

    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));
    public StageOptions Options { get; } = options ?? throw new ArgumentNullException(nameof(options));

    public async IAsyncEnumerable<TOut> ExecuteAsync(
        IAsyncEnumerable<TIn> input,
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
            await foreach (var result in input
                               .SelectParallelStreamAsync(_selector, parallelOptions, cancellationToken)
                               .ConfigureAwait(false))
            {
                metrics.IncrementItemsOut();
                yield return result;
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
    ) => StageExecutionHelper.ExecuteUntypedAsync<TIn, TOut>(
        input,
        typedInput => ExecuteAsync(typedInput, context, cancellationToken),
        cancellationToken);
}
