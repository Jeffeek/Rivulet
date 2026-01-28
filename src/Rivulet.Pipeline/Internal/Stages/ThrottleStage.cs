using System.Runtime.CompilerServices;
using Rivulet.Core.Resilience;

namespace Rivulet.Pipeline.Internal.Stages;

/// <summary>
/// A stage that rate limits items flowing through using token bucket algorithm.
/// Reuses TokenBucket implementation from Rivulet.Core.Resilience.
/// </summary>
internal sealed class ThrottleStage<T>(double tokensPerSecond, double burstCapacity, string name) : IInternalPipelineStage, IPipelineStage<T, T>
{
    private readonly RateLimitOptions _rateLimitOptions = new()
    {
        TokensPerSecond = tokensPerSecond,
        BurstCapacity = burstCapacity,
        TokensPerOperation = 1.0
    };

    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));
    public StageOptions Options { get; } = new();

    public async IAsyncEnumerable<T> ExecuteAsync(
        IAsyncEnumerable<T> input,
        PipelineContext context,
        [EnumeratorCancellation]
        CancellationToken cancellationToken
    )
    {
        var metrics = context.GetOrCreateStageMetrics(Name, 0);
        metrics.Start();

        // Reuse TokenBucket from Core instead of reimplementing
        var tokenBucket = new TokenBucket(_rateLimitOptions);

        try
        {
            await foreach (var item in input.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                metrics.IncrementItemsIn();

                // Wait for token using existing TokenBucket implementation
                await tokenBucket.AcquireAsync(cancellationToken).ConfigureAwait(false);

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
