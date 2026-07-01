using System.Runtime.CompilerServices;
using Rivulet.Core;
using Rivulet.Core.Resilience;

namespace Rivulet.Pipeline.Internal.Stages;

internal sealed class ThrottleStage<T>(double tokensPerSecond, double burstCapacity, string name)
    : PipelineStageBase<T, T>(name, new StageOptions())
{
    private readonly RateLimitOptions _rateLimitOptions = new()
    {
        TokensPerSecond = tokensPerSecond,
        BurstCapacity = burstCapacity,
        TokensPerOperation = 1.0
    };

    public override async IAsyncEnumerable<T> ExecuteAsync(
        IAsyncEnumerable<T> input,
        PipelineContext context,
        [EnumeratorCancellation]
        CancellationToken cancellationToken
    )
    {
        var metrics = context.GetStageMetrics(Name);
        metrics.Start();

        var tokenBucket = new TokenBucket(_rateLimitOptions);

        try
        {
            await foreach (var item in input.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                metrics.IncrementItemsIn();

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

    protected override IAsyncEnumerable<T> ExecuteCoreAsync(
        IAsyncEnumerable<T> _,
        ParallelOptionsRivulet __,
        PipelineContext ___,
        CancellationToken ____
    ) => throw new NotSupportedException();
}
