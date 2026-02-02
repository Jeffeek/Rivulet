using System.Runtime.CompilerServices;

namespace Rivulet.Pipeline.Internal;

/// <summary>
/// The pipeline execution engine that chains stages together.
/// </summary>
internal sealed class Pipeline<TIn, TOut> : IPipeline<TIn, TOut>
{
    private readonly IReadOnlyList<IInternalPipelineStage> _stages;
    private readonly PipelineOptions _options;

    public string Name => _options.Name;
    public int StageCount => _stages.Count;

    internal Pipeline(IReadOnlyList<IInternalPipelineStage> stages, PipelineOptions options)
    {
        _stages = stages;
        _options = options;
    }

    public async IAsyncEnumerable<TOut> ExecuteStreamAsync(
        IAsyncEnumerable<TIn> source,
        [EnumeratorCancellation]
        CancellationToken cancellationToken = default
    )
    {
        var context = new PipelineContext(_options);
        await using (context.ConfigureAwait(false))
        {
            // Invoke pipeline start callback
            if (_options.OnPipelineStartAsync is not null)
                await _options.OnPipelineStartAsync(context).ConfigureAwait(false);

            try
            {
                // Chain all stages together
                var current = source.Select(static x => (object)x!);

                for (var i = 0; i < _stages.Count; i++)
                {
                    var stage = _stages[i];

                    // Update stage index in metrics
                    _ = context.GetOrCreateStageMetrics(stage.Name, i);

                    // Invoke stage start callback
                    if (_options.OnStageStartAsync is not null)
                        await _options.OnStageStartAsync(stage.Name, i).ConfigureAwait(false);

                    // Execute stage - creates the chained async enumerable
                    current = stage.ExecuteUntypedAsync(current, context, cancellationToken);
                }

                // Yield results as they come through
                await foreach (var result in current.WithCancellation(cancellationToken).ConfigureAwait(false))
                    yield return (TOut)result;
            }
            finally
            {
                // Create result and invoke completion callback
                var result = context.CreateResult();

                if (_options.OnPipelineCompleteAsync is not null)
                {
                    try
                    {
                        await _options.OnPipelineCompleteAsync(context, result).ConfigureAwait(false);
                    }
#pragma warning disable CA1031 // Do not catch general exception types - callback failures should not break pipeline cleanup
                    catch
#pragma warning restore CA1031
                    {
                        // Swallow callback exceptions during cleanup
                    }
                }
            }
        }
    }

    public Task<List<TOut>> ExecuteAsync(
        IEnumerable<TIn> source,
        CancellationToken cancellationToken = default
    ) => ExecuteAsync(source.ToAsyncEnumerable(), cancellationToken);

    public async Task<List<TOut>> ExecuteAsync(
        IAsyncEnumerable<TIn> source,
        CancellationToken cancellationToken = default
    )
    {
        var results = new List<TOut>();

        await foreach (var result in ExecuteStreamAsync(source, cancellationToken).ConfigureAwait(false))
            results.Add(result);

        return results;
    }
}
