using Rivulet.Core;
using Rivulet.Core.Resilience;
using Rivulet.Pipeline.Internal;
using Rivulet.Pipeline.Internal.Stages;

namespace Rivulet.Pipeline;

/// <summary>
/// Fluent builder for constructing multi-stage pipelines.
/// </summary>
/// <typeparam name="TIn">The pipeline input type.</typeparam>
/// <typeparam name="TCurrent">The current stage output type (becomes input for next stage).</typeparam>
public sealed class PipelineBuilder<TIn, TCurrent>
{
    private readonly List<IInternalPipelineStage> _stages;
    private readonly PipelineOptions _options;

    private PipelineBuilder(List<IInternalPipelineStage> stages, PipelineOptions options)
    {
        _stages = stages;
        _options = options;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TRANSFORMATION STAGES
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Adds a parallel select (map) stage that transforms each item.
    /// </summary>
    /// <typeparam name="TOut">The output type after transformation.</typeparam>
    /// <param name="selector">The async transformation function.</param>
    /// <param name="options">Stage-specific options. If null, uses pipeline defaults.</param>
    /// <param name="name">Stage name for diagnostics. If null, auto-generated.</param>
    /// <returns>A new builder with the updated pipeline configuration.</returns>
    public PipelineBuilder<TIn, TOut> SelectParallel<TOut>(
        Func<TCurrent, CancellationToken, ValueTask<TOut>> selector,
        StageOptions? options = null,
        string? name = null
    )
    {
        var stage = new SelectStage<TCurrent, TOut>(
            selector,
            options ?? new StageOptions(),
            name ?? $"Select_{_stages.Count}");

        return AddStage<TOut>(stage);
    }

    /// <summary>
    /// Adds a parallel select stage with a synchronous selector.
    /// </summary>
    /// <typeparam name="TOut">The output type after transformation.</typeparam>
    /// <param name="selector">The synchronous transformation function.</param>
    /// <param name="options">Stage-specific options. If null, uses pipeline defaults.</param>
    /// <param name="name">Stage name for diagnostics. If null, auto-generated.</param>
    /// <returns>A new builder with the updated pipeline configuration.</returns>
    public PipelineBuilder<TIn, TOut> SelectParallel<TOut>(
        Func<TCurrent, TOut> selector,
        StageOptions? options = null,
        string? name = null
    ) =>
        SelectParallel(
            (item, _) => ValueTask.FromResult(selector(item)),
            options,
            name);

    // ═══════════════════════════════════════════════════════════════════════════
    // FILTERING STAGES
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Adds a parallel filter stage that keeps only items matching the predicate.
    /// </summary>
    /// <param name="predicate">The async predicate function. Items returning true are kept.</param>
    /// <param name="options">Stage-specific options. If null, uses pipeline defaults.</param>
    /// <param name="name">Stage name for diagnostics. If null, auto-generated.</param>
    /// <returns>A new builder with the updated pipeline configuration.</returns>
    public PipelineBuilder<TIn, TCurrent> WhereParallel(
        Func<TCurrent, CancellationToken, ValueTask<bool>> predicate,
        StageOptions? options = null,
        string? name = null
    )
    {
        var stage = new FilterStage<TCurrent>(
            predicate,
            options ?? new StageOptions(),
            name ?? $"Where_{_stages.Count}");

        return AddStage<TCurrent>(stage);
    }

    /// <summary>
    /// Adds a parallel filter stage with a synchronous predicate.
    /// </summary>
    /// <param name="predicate">The synchronous predicate function. Items returning true are kept.</param>
    /// <param name="options">Stage-specific options. If null, uses pipeline defaults.</param>
    /// <param name="name">Stage name for diagnostics. If null, auto-generated.</param>
    /// <returns>A new builder with the updated pipeline configuration.</returns>
    public PipelineBuilder<TIn, TCurrent> WhereParallel(
        Func<TCurrent, bool> predicate,
        StageOptions? options = null,
        string? name = null
    ) => WhereParallel(
        (item, _) => ValueTask.FromResult(predicate(item)),
        options,
        name);

    // ═══════════════════════════════════════════════════════════════════════════
    // BATCHING STAGES
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Adds a batching stage that groups items into fixed-size batches.
    /// </summary>
    /// <param name="batchSize">The maximum number of items per batch.</param>
    /// <param name="flushTimeout">Optional timeout to flush incomplete batches.</param>
    /// <param name="name">Stage name for diagnostics. If null, auto-generated.</param>
    /// <returns>A new builder with the updated pipeline configuration.</returns>
    public PipelineBuilder<TIn, IReadOnlyList<TCurrent>> Batch(
        int batchSize,
        TimeSpan? flushTimeout = null,
        string? name = null
    )
    {
        if (batchSize < 1)
            throw new ArgumentException("Batch size must be at least 1.", nameof(batchSize));

        var stage = new BatchStage<TCurrent>(
            batchSize,
            flushTimeout,
            name ?? $"Batch_{_stages.Count}");

        return AddStage<IReadOnlyList<TCurrent>>(stage);
    }

    /// <summary>
    /// Adds a batch processing stage that groups items and transforms each batch.
    /// </summary>
    /// <typeparam name="TOut">The output type after batch transformation.</typeparam>
    /// <param name="batchSize">The maximum number of items per batch.</param>
    /// <param name="batchSelector">The async function to transform each batch.</param>
    /// <param name="flushTimeout">Optional timeout to flush incomplete batches.</param>
    /// <param name="options">Stage-specific options. If null, uses pipeline defaults.</param>
    /// <param name="name">Stage name for diagnostics. If null, auto-generated.</param>
    /// <returns>A new builder with the updated pipeline configuration.</returns>
    public PipelineBuilder<TIn, TOut> BatchSelectParallel<TOut>(
        int batchSize,
        Func<IReadOnlyList<TCurrent>, CancellationToken, ValueTask<TOut>> batchSelector,
        TimeSpan? flushTimeout = null,
        StageOptions? options = null,
        string? name = null
    )
    {
        if (batchSize < 1)
            throw new ArgumentException("Batch size must be at least 1.", nameof(batchSize));

        var stage = new BatchSelectStage<TCurrent, TOut>(
            batchSize,
            batchSelector,
            flushTimeout,
            options ?? new StageOptions(),
            name ?? $"BatchSelect_{_stages.Count}");

        return AddStage<TOut>(stage);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // FLAT MAP / SELECT MANY STAGES
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Adds a parallel SelectMany stage that flattens collections.
    /// </summary>
    /// <typeparam name="TOut">The element type of the flattened collections.</typeparam>
    /// <param name="selector">The async function that returns a collection for each input item.</param>
    /// <param name="options">Stage-specific options. If null, uses pipeline defaults.</param>
    /// <param name="name">Stage name for diagnostics. If null, auto-generated.</param>
    /// <returns>A new builder with the updated pipeline configuration.</returns>
    public PipelineBuilder<TIn, TOut> SelectManyParallel<TOut>(
        Func<TCurrent, CancellationToken, ValueTask<IEnumerable<TOut>>> selector,
        StageOptions? options = null,
        string? name = null
    )
    {
        var stage = new SelectManyStage<TCurrent, TOut>(
            selector,
            options ?? new StageOptions(),
            name ?? $"SelectMany_{_stages.Count}");

        return AddStage<TOut>(stage);
    }

    /// <summary>
    /// Adds a parallel SelectMany stage with a synchronous selector.
    /// </summary>
    /// <typeparam name="TOut">The element type of the flattened collections.</typeparam>
    /// <param name="selector">The synchronous function that returns a collection for each input item.</param>
    /// <param name="options">Stage-specific options. If null, uses pipeline defaults.</param>
    /// <param name="name">Stage name for diagnostics. If null, auto-generated.</param>
    /// <returns>A new builder with the updated pipeline configuration.</returns>
    public PipelineBuilder<TIn, TOut> SelectManyParallel<TOut>(
        Func<TCurrent, IEnumerable<TOut>> selector,
        StageOptions? options = null,
        string? name = null
    ) => SelectManyParallel(
        (item, _) => ValueTask.FromResult(selector(item)),
        options,
        name);

    // ═══════════════════════════════════════════════════════════════════════════
    // SIDE EFFECT STAGES
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Adds a tap stage that executes a side effect on each item without transforming it.
    /// Useful for logging, metrics, or other side effects.
    /// </summary>
    /// <param name="action">The async action to execute for each item.</param>
    /// <param name="options">Stage-specific options. If null, uses pipeline defaults.</param>
    /// <param name="name">Stage name for diagnostics. If null, auto-generated.</param>
    /// <returns>A new builder with the updated pipeline configuration.</returns>
    public PipelineBuilder<TIn, TCurrent> Tap(
        Func<TCurrent, CancellationToken, ValueTask> action,
        StageOptions? options = null,
        string? name = null
    )
    {
        var stage = new TapStage<TCurrent>(
            action,
            options ?? new StageOptions(),
            name ?? $"Tap_{_stages.Count}");

        return AddStage<TCurrent>(stage);
    }

    /// <summary>
    /// Adds a tap stage with a synchronous action.
    /// </summary>
    /// <param name="action">The synchronous action to execute for each item.</param>
    /// <param name="options">Stage-specific options. If null, uses pipeline defaults.</param>
    /// <param name="name">Stage name for diagnostics. If null, auto-generated.</param>
    /// <returns>A new builder with the updated pipeline configuration.</returns>
    public PipelineBuilder<TIn, TCurrent> Tap(
        Action<TCurrent> action,
        StageOptions? options = null,
        string? name = null
    ) => Tap(
        (item, _) =>
        {
            action(item);
            return ValueTask.CompletedTask;
        },
        options,
        name);

    // ═══════════════════════════════════════════════════════════════════════════
    // BUFFERING / FLOW CONTROL STAGES
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Adds a buffer stage that decouples upstream and downstream processing.
    /// </summary>
    /// <param name="capacity">The buffer capacity.</param>
    /// <param name="name">Stage name for diagnostics. If null, auto-generated.</param>
    /// <returns>A new builder with the updated pipeline configuration.</returns>
    public PipelineBuilder<TIn, TCurrent> Buffer(
        int capacity,
        string? name = null
    )
    {
        if (capacity < 1)
            throw new ArgumentException("Buffer capacity must be at least 1.", nameof(capacity));

        var stage = new BufferStage<TCurrent>(
            capacity,
            name ?? $"Buffer_{_stages.Count}");

        return AddStage<TCurrent>(stage);
    }

    /// <summary>
    /// Adds a throttle stage that limits the rate of items flowing through.
    /// </summary>
    /// <param name="itemsPerSecond">The maximum items per second.</param>
    /// <param name="burstCapacity">The burst capacity. Defaults to itemsPerSecond.</param>
    /// <param name="name">Stage name for diagnostics. If null, auto-generated.</param>
    /// <returns>A new builder with the updated pipeline configuration.</returns>
    public PipelineBuilder<TIn, TCurrent> Throttle(
        double itemsPerSecond,
        double? burstCapacity = null,
        string? name = null
    )
    {
        if (itemsPerSecond <= 0)
            throw new ArgumentException("Items per second must be positive.", nameof(itemsPerSecond));

        var stage = new ThrottleStage<TCurrent>(
            itemsPerSecond,
            burstCapacity ?? itemsPerSecond,
            name ?? $"Throttle_{_stages.Count}");

        return AddStage<TCurrent>(stage);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PIPELINE-WIDE CONFIGURATION
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Configures pipeline-wide retry policy applied to all stages by default.
    /// </summary>
    /// <param name="maxRetries">The maximum number of retry attempts.</param>
    /// <param name="baseDelay">The base delay between retries. Defaults to 100ms.</param>
    /// <param name="strategy">The backoff strategy. Defaults to ExponentialJitter.</param>
    /// <param name="isTransient">The predicate to determine if an exception is transient.</param>
    /// <returns>A new builder with the updated configuration.</returns>
    public PipelineBuilder<TIn, TCurrent> WithRetries(
        int maxRetries,
        TimeSpan? baseDelay = null,
        BackoffStrategy? strategy = null,
        Func<Exception, bool>? isTransient = null
    )
    {
        var newDefaults = new ParallelOptionsRivulet(_options.DefaultStageOptions)
        {
            MaxRetries = maxRetries,
            BaseDelay = baseDelay ?? _options.DefaultStageOptions.BaseDelay,
            BackoffStrategy = strategy ?? _options.DefaultStageOptions.BackoffStrategy,
            IsTransient = isTransient ?? _options.DefaultStageOptions.IsTransient
        };

        return WithUpdatedDefaults(newDefaults);
    }

    /// <summary>
    /// Configures pipeline-wide circuit breaker applied to all stages by default.
    /// </summary>
    /// <param name="failureThreshold">The number of failures before opening the circuit.</param>
    /// <param name="openTimeout">The duration the circuit stays open. Defaults to 30 seconds.</param>
    /// <param name="successThreshold">The successes needed in half-open to close. Defaults to 2.</param>
    /// <returns>A new builder with the updated configuration.</returns>
    public PipelineBuilder<TIn, TCurrent> WithCircuitBreaker(
        int failureThreshold,
        TimeSpan? openTimeout = null,
        int? successThreshold = null
    )
    {
        var newDefaults = new ParallelOptionsRivulet(_options.DefaultStageOptions)
        {
            CircuitBreaker = new CircuitBreakerOptions
            {
                FailureThreshold = failureThreshold,
                OpenTimeout = openTimeout ?? TimeSpan.FromSeconds(30),
                SuccessThreshold = successThreshold ?? 2
            }
        };

        return WithUpdatedDefaults(newDefaults);
    }

    /// <summary>
    /// Configures pipeline-wide rate limiting applied to all stages by default.
    /// </summary>
    /// <param name="tokensPerSecond">The sustained rate in tokens per second.</param>
    /// <param name="burstCapacity">The burst capacity. Defaults to tokensPerSecond.</param>
    /// <returns>A new builder with the updated configuration.</returns>
    public PipelineBuilder<TIn, TCurrent> WithRateLimit(
        double tokensPerSecond,
        double? burstCapacity = null
    )
    {
        var newDefaults = new ParallelOptionsRivulet(_options.DefaultStageOptions)
        {
            RateLimit = new RateLimitOptions
            {
                TokensPerSecond = tokensPerSecond,
                BurstCapacity = burstCapacity ?? tokensPerSecond
            }
        };

        return WithUpdatedDefaults(newDefaults);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // BUILD
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Builds the pipeline with the current configuration.
    /// </summary>
    /// <returns>An executable pipeline.</returns>
    public IPipeline<TIn, TCurrent> Build() =>
        _stages.Count == 0
            ? throw new InvalidOperationException("Pipeline must have at least one stage.")
            : new Pipeline<TIn, TCurrent>([.. _stages], _options);

    // ═══════════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ═══════════════════════════════════════════════════════════════════════════

    private PipelineBuilder<TIn, TOut> AddStage<TOut>(IInternalPipelineStage stage)
    {
        var newStages = new List<IInternalPipelineStage>(_stages) { stage };
        return new PipelineBuilder<TIn, TOut>(newStages, _options);
    }

    private PipelineBuilder<TIn, TCurrent> WithUpdatedDefaults(ParallelOptionsRivulet newDefaults)
    {
        var newOptions = new PipelineOptions(_options)
        {
            DefaultStageOptions = newDefaults
        };

        return new PipelineBuilder<TIn, TCurrent>(_stages, newOptions);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // STATIC FACTORY
    // ═══════════════════════════════════════════════════════════════════════════

    internal static PipelineBuilder<T, T> CreateInternal<T>(PipelineOptions options) =>
        new([], options);
}

/// <summary>
/// Static factory methods for creating pipeline builders.
/// </summary>
public static class PipelineBuilder
{
    /// <summary>
    /// Creates a new pipeline builder with default options.
    /// </summary>
    /// <typeparam name="T">The pipeline input type.</typeparam>
    /// <returns>A new pipeline builder.</returns>
    public static PipelineBuilder<T, T> Create<T>() =>
        PipelineBuilder<T, T>.CreateInternal<T>(new PipelineOptions());

    /// <summary>
    /// Creates a new pipeline builder with a name.
    /// </summary>
    /// <typeparam name="T">The pipeline input type.</typeparam>
    /// <param name="name">The pipeline name for diagnostics.</param>
    /// <returns>A new pipeline builder.</returns>
    public static PipelineBuilder<T, T> Create<T>(string name) =>
        PipelineBuilder<T, T>.CreateInternal<T>(new PipelineOptions { Name = name });

    /// <summary>
    /// Creates a new pipeline builder with custom options.
    /// </summary>
    /// <typeparam name="T">The pipeline input type.</typeparam>
    /// <param name="options">The pipeline options.</param>
    /// <returns>A new pipeline builder.</returns>
    public static PipelineBuilder<T, T> Create<T>(PipelineOptions options) =>
        PipelineBuilder<T, T>.CreateInternal<T>(options);
}
