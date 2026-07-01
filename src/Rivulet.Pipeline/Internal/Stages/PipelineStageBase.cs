using System.Runtime.CompilerServices;
using Rivulet.Core;

namespace Rivulet.Pipeline.Internal.Stages;

/// <summary>
/// Base class for pipeline stages that share common property and untyped execution patterns.
/// </summary>
/// <typeparam name="TIn">The input type for this stage.</typeparam>
/// <typeparam name="TOut">The output type for this stage.</typeparam>
internal abstract class PipelineStageBase<TIn, TOut>(string name, StageOptions options) : IInternalPipelineStage, IPipelineStage<TIn, TOut>
{
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));
    public StageOptions Options { get; } = options ?? throw new ArgumentNullException(nameof(options));

    public virtual async IAsyncEnumerable<TOut> ExecuteAsync(
        IAsyncEnumerable<TIn> input,
        PipelineContext context,
        [EnumeratorCancellation]
        CancellationToken cancellationToken
    )
    {
        var parallelOptions = Options.GetMergedOptions(context.DefaultStageOptions);
        var metrics = context.GetStageMetrics(Name);

        metrics.Start();

        try
        {
            await foreach (var result in ExecuteCoreAsync(input, parallelOptions, context, cancellationToken).ConfigureAwait(false))
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

    /// <summary>
    /// Core transformation logic for stages that use <see cref="ExecuteAsync"/> as-is.
    /// Override this instead of <see cref="ExecuteAsync"/> to avoid repeating metrics/options boilerplate.
    /// </summary>
    protected abstract IAsyncEnumerable<TOut> ExecuteCoreAsync(
        IAsyncEnumerable<TIn> input,
        ParallelOptionsRivulet parallelOptions,
        PipelineContext context,
        CancellationToken cancellationToken
    );

    public IAsyncEnumerable<object> ExecuteUntypedAsync(
        IAsyncEnumerable<object> input,
        PipelineContext context,
        CancellationToken cancellationToken
    ) => StageExecutionHelper.ExecuteUntypedAsync<TIn, TOut>(
        input,
        typedInput => ExecuteAsync(typedInput, context, cancellationToken),
        cancellationToken);
}
