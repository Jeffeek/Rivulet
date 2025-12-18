using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Rivulet.Core.Observability;
using Rivulet.Core.Resilience;

namespace Rivulet.Core;

/// <summary>
///     Provides async-first parallel LINQ operators with bounded concurrency, retries, and backpressure for I/O-heavy
///     workloads.
/// </summary>
public static class AsyncParallelLinq
{
    /// <summary>
    ///     Applies a transformation function to each element in a collection in parallel with bounded concurrency,
    ///     returning a materialized list of results. Supports retry policies, per-item timeouts, and configurable error
    ///     handling.
    /// </summary>
    /// <typeparam name="TSource">The type of elements in the source collection.</typeparam>
    /// <typeparam name="TResult">The type of elements in the result collection.</typeparam>
    /// <param name="source">The source enumerable to process.</param>
    /// <param name="taskSelector">The async transformation function to apply to each element.</param>
    /// <param name="options">
    ///     Configuration options for parallel execution, including concurrency limits, retry policies, and
    ///     lifecycle hooks. If null, defaults are used.
    /// </param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing a list of all transformed results.</returns>
    /// <exception cref="AggregateException">
    ///     Thrown when <see cref="ErrorMode.CollectAndContinue" /> is enabled and one or more
    ///     errors occurred during processing.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the cancellation token.</exception>
    public static async Task<List<TResult>> SelectParallelAsync<TSource, TResult>(
        this IEnumerable<TSource> source,
        Func<TSource, CancellationToken, ValueTask<TResult>> taskSelector,
        ParallelOptionsRivulet? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = cts.Token;

        var results = options.OrderedOutput
            ? null
            : new ConcurrentBag<TResult>();
        var orderedResults = options.OrderedOutput
            ? new ConcurrentDictionary<int, TResult>()
            : null;
        var errors = new ConcurrentBag<Exception>();

        var sourceList = source as ICollection<TSource> ?? [..source];
        var totalItems = sourceList.Count;

#pragma warning disable CA2007
        await using var infrastructure = InitializeInfrastructure(options, totalItems, token);
#pragma warning restore CA2007

        var effectiveMaxWorkers = infrastructure.EffectiveMaxWorkers;

        var channel = Channel.CreateBounded<(int idx, TSource item)>(new BoundedChannelOptions(options.ChannelCapacity)
        {
            SingleReader = false, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait
        });

        var writerTask = Task.Run(async () =>
            {
                var i = 0;
                try
                {
                    foreach (var item in sourceList)
                    {
                        token.ThrowIfCancellationRequested();
                        if (!await channel.Writer.WaitToWriteAsync(token).ConfigureAwait(false)) break;

                        await channel.Writer.WriteAsync((i, item), token).ConfigureAwait(false);
                        if (i++ % effectiveMaxWorkers != 0 || options.OnThrottleAsync is null) continue;

                        // ReSharper disable once AccessToDisposedClosure
                        infrastructure.MetricsTracker.IncrementThrottleEvents();
                        await options.OnThrottleAsync(i).ConfigureAwait(false);
                    }
                }
                finally
                {
                    channel.Writer.TryComplete();
                }
            },
            token);

        var workers = Enumerable.Range(0, effectiveMaxWorkers)
            .Select(_ => Task.Run(async () =>
                {
                    await foreach (var (idx, item) in channel.Reader.ReadAllAsync(token).ConfigureAwait(false))
                    {
                        var startTime = Stopwatch.GetTimestamp();
                        var success = false;
                        var acquiredAdaptive = false;

                        try
                        {
                            // ReSharper disable AccessToDisposedClosure
                            if (infrastructure.AdaptiveController is not null)
                            {
                                await infrastructure.AdaptiveController.AcquireAsync(token).ConfigureAwait(false);
                                // ReSharper restore AccessToDisposedClosure
                                acquiredAdaptive = true;
                            }

                            // ReSharper disable AccessToDisposedClosure
                            if (infrastructure.TokenBucket is not null)
                                await infrastructure.TokenBucket.AcquireAsync(token).ConfigureAwait(false);
                            // ReSharper restore AccessToDisposedClosure

                            // ReSharper disable AccessToDisposedClosure
                            infrastructure.ProgressTracker?.IncrementStarted();
                            infrastructure.MetricsTracker.IncrementItemsStarted();
                            // ReSharper restore AccessToDisposedClosure

                            if (options.OnStartItemAsync is not null)
                                await options.OnStartItemAsync(idx).ConfigureAwait(false);

                            ValueTask<TResult> ToExecute() =>
                                RetryPolicy.ExecuteWithRetry(item,
                                    taskSelector,
                                    options,
                                    // ReSharper disable once AccessToDisposedClosure
                                    infrastructure.MetricsTracker,
                                    idx,
                                    token);

                            // ReSharper disable AccessToDisposedClosure
                            var result = infrastructure.CircuitBreaker is not null
                                ? await infrastructure.CircuitBreaker.ExecuteAsync(ToExecute, token).ConfigureAwait(false)
                                // ReSharper restore AccessToDisposedClosure
                                : await ToExecute().ConfigureAwait(false);

                            if (options.OrderedOutput && orderedResults != null)
                                orderedResults[idx] = result;
                            else
                                results?.Add(result);

                            // ReSharper disable AccessToDisposedClosure
                            infrastructure.ProgressTracker?.IncrementCompleted();
                            infrastructure.MetricsTracker.IncrementItemsCompleted();
                            // ReSharper restore AccessToDisposedClosure

                            if (options.OnCompleteItemAsync is not null)
                                await options.OnCompleteItemAsync(idx).ConfigureAwait(false);

                            success = true;
                        }
                        catch (Exception ex)
                        {
                            // ReSharper disable once AccessToDisposedClosure
                            await HandleProcessingErrorAsync(
                                ex,
                                idx,
                                options,
                                infrastructure,
                                // ReSharper disable once AccessToDisposedClosure
                                cts,
                                errors).ConfigureAwait(false);
                        }
                        finally
                        {
                            // ReSharper disable once AccessToDisposedClosure
                            ReleaseAdaptiveController(infrastructure.AdaptiveController, acquiredAdaptive, startTime, success);
                        }
                    }
                },
                token))
            .ToArray();

        try
        {
            await Task.WhenAll(workers.Prepend(writerTask)).ConfigureAwait(false);
        }
        catch when (options.ErrorMode != ErrorMode.FailFast)
        {
            // Errors are collected in the errors list and handled after cleanup
        }

        if (options.ErrorMode == ErrorMode.CollectAndContinue && !errors.IsEmpty) throw new AggregateException(errors);

        if (!options.OrderedOutput) return [.. results ?? []];

        var result = new List<TResult>(totalItems);

        for (var i = 0; i < totalItems; i++)
        {
            if (orderedResults is not null && orderedResults.TryGetValue(i, out var value))
                result.Add(value);
        }

        return result;
    }

    /// <summary>
    ///     Applies a transformation function to each element in an async stream in parallel with bounded concurrency,
    ///     returning results as a streaming async enumerable with built-in backpressure control.
    ///     Results may be yielded out-of-order relative to the input sequence unless
    ///     <see cref="ParallelOptionsRivulet.OrderedOutput" /> is true.
    /// </summary>
    /// <typeparam name="TSource">The type of elements in the source collection.</typeparam>
    /// <typeparam name="TResult">The type of elements in the result stream.</typeparam>
    /// <param name="source">The async source enumerable to process.</param>
    /// <param name="taskSelector">The async transformation function to apply to each element.</param>
    /// <param name="options">
    ///     Configuration options for parallel execution, including concurrency limits, retry policies, and
    ///     lifecycle hooks. If null, defaults are used.
    /// </param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>An async enumerable that yields transformed results as they complete.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the cancellation token.</exception>
    [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
    public static async IAsyncEnumerable<TResult> SelectParallelStreamAsync<TSource, TResult>(
        this IAsyncEnumerable<TSource> source,
        Func<TSource, CancellationToken, ValueTask<TResult>> taskSelector,
        ParallelOptionsRivulet? options = null,
        [EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        options ??= new();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = cts.Token;

#pragma warning disable CA2007
        await using var infrastructure = InitializeInfrastructure(options, null, token);
#pragma warning restore CA2007

        var effectiveMaxWorkers = infrastructure.EffectiveMaxWorkers;

        var input = Channel.CreateBounded<(int idx, TSource item)>(new BoundedChannelOptions(options.ChannelCapacity)
        {
            SingleReader = false, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait
        });
        var output = Channel.CreateBounded<(int idx, TResult result)>(new BoundedChannelOptions(options.ChannelCapacity)
        {
            SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait
        });

        var writer = Task.Run(async () =>
            {
                var i = 0;
                try
                {
                    await foreach (var item in source.WithCancellation(token).ConfigureAwait(false))
                    {
                        token.ThrowIfCancellationRequested();
                        await input.Writer.WriteAsync((i++, item), token).ConfigureAwait(false);
                    }
                }
                finally
                {
                    input.Writer.TryComplete();
                }
            },
            token);

        var workers = Enumerable.Range(0, effectiveMaxWorkers)
            .Select(_ => Task.Run(async () =>
                {
                    await foreach (var (idx, item) in input.Reader.ReadAllAsync(token).ConfigureAwait(false))
                    {
                        var startTime = Stopwatch.GetTimestamp();
                        var success = false;
                        var acquiredAdaptive = false;

                        try
                        {
                            if (infrastructure.AdaptiveController is not null)
                            {
                                await infrastructure.AdaptiveController.AcquireAsync(token).ConfigureAwait(false);
                                acquiredAdaptive = true;
                            }

                            if (infrastructure.TokenBucket is not null)
                                await infrastructure.TokenBucket.AcquireAsync(token).ConfigureAwait(false);

                            infrastructure.ProgressTracker?.IncrementStarted();
                            infrastructure.MetricsTracker.IncrementItemsStarted();
                            if (options.OnStartItemAsync is not null)
                                await options.OnStartItemAsync(idx).ConfigureAwait(false);

                            ValueTask<TResult> ToExecute() =>
                                RetryPolicy.ExecuteWithRetry(item,
                                    taskSelector,
                                    options,
                                    infrastructure.MetricsTracker,
                                    idx,
                                    token);

                            var result = infrastructure.CircuitBreaker is not null
                                ? await infrastructure.CircuitBreaker.ExecuteAsync(ToExecute, token).ConfigureAwait(false)
                                : await ToExecute().ConfigureAwait(false);

                            await output.Writer.WriteAsync((idx, result), token).ConfigureAwait(false);
                            infrastructure.ProgressTracker?.IncrementCompleted();
                            infrastructure.MetricsTracker.IncrementItemsCompleted();
                            if (options.OnCompleteItemAsync is not null)
                                await options.OnCompleteItemAsync(idx).ConfigureAwait(false);

                            success = true;
                        }
                        catch (Exception ex)
                        {
                            await HandleProcessingErrorAsync(
                                ex,
                                idx,
                                options,
                                infrastructure,
                                cts,
                                null).ConfigureAwait(false);
                        }
                        finally
                        {
                            ReleaseAdaptiveController(infrastructure.AdaptiveController, acquiredAdaptive, startTime, success);
                        }
                    }
                },
                token))
            .ToArray();

        var reader = Task.Run(async () =>
            {
                try
                {
                    await Task.WhenAll(workers).ConfigureAwait(false);
                }
                finally
                {
                    output.Writer.TryComplete();
                }
            },
            token);

        if (options.OrderedOutput)
        {
            var buffer = new Dictionary<int, TResult>(options.ChannelCapacity);
            var nextIndex = 0;

            await foreach (var (idx, result) in output.Reader.ReadAllAsync(token).ConfigureAwait(false))
            {
                token.ThrowIfCancellationRequested();

                buffer[idx] = result;

                while (buffer.Remove(nextIndex, out var orderedResult))
                {
                    yield return orderedResult;

                    nextIndex++;
                }
            }

            foreach (var idx in buffer.Keys.OrderBy(static k => k)) yield return buffer[idx];
        }
        else
        {
            await foreach (var (_, result) in output.Reader.ReadAllAsync(token).ConfigureAwait(false))
            {
                token.ThrowIfCancellationRequested();
                yield return result;
            }
        }

        await Task.WhenAll(writer, reader).ConfigureAwait(false);
    }

    /// <summary>
    ///     Executes an async action on each element in a stream in parallel with bounded concurrency.
    ///     No results are returned; this is suitable for side effect operations like logging, updates, or fire-and-forget
    ///     processing.
    /// </summary>
    /// <typeparam name="TSource">The type of elements in the source collection.</typeparam>
    /// <param name="source">The async source enumerable to process.</param>
    /// <param name="action">The async action to execute for each element.</param>
    /// <param name="options">
    ///     Configuration options for parallel execution, including concurrency limits, retry policies, and
    ///     lifecycle hooks. If null, defaults are used.
    /// </param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that completes when all items have been processed.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the cancellation token.</exception>
    public static Task ForEachParallelAsync<TSource>(
        this IAsyncEnumerable<TSource> source,
        Func<TSource, CancellationToken, ValueTask> action,
        ParallelOptionsRivulet? options = null,
        CancellationToken cancellationToken = default) =>
        SelectParallelStreamAsync(
                source,
                async (item, ct) =>
                {
                    await action(item, ct).ConfigureAwait(false);
                    return true;
                },
                options,
                cancellationToken
            )
            .CollectAsync(cancellationToken);

    /// <summary>
    ///     Executes an async action on each element in an enumerable in parallel with bounded concurrency.
    ///     No results are returned; this is suitable for side effect operations like logging, updates, or fire-and-forget
    ///     processing.
    /// </summary>
    /// <typeparam name="TSource">The type of elements in the source collection.</typeparam>
    /// <param name="source">The source enumerable to process.</param>
    /// <param name="action">The async action to execute for each element.</param>
    /// <param name="options">
    ///     Configuration options for parallel execution, including concurrency limits, retry policies, and
    ///     lifecycle hooks. If null, defaults are used.
    /// </param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that completes when all items have been processed.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the cancellation token.</exception>
    public static Task ForEachParallelAsync<TSource>(
        this IEnumerable<TSource> source,
        Func<TSource, CancellationToken, ValueTask> action,
        ParallelOptionsRivulet? options = null,
        CancellationToken cancellationToken = default) =>
        source.ToAsyncEnumerable().ForEachParallelAsync(action, options, cancellationToken);

    private static async Task CollectAsync<T>(this IAsyncEnumerable<T> source, CancellationToken ct)
    {
        await foreach (var _ in source.WithCancellation(ct).ConfigureAwait(false)) { }
    }

    /// <summary>
    ///     Groups items into batches and processes each batch in parallel with bounded concurrency.
    ///     Returns a materialized list of all batch results.
    /// </summary>
    /// <typeparam name="TSource">The type of elements in the source collection.</typeparam>
    /// <typeparam name="TResult">The type of result returned by processing each batch.</typeparam>
    /// <param name="source">The source enumerable to process.</param>
    /// <param name="batchSize">The maximum number of items in each batch.</param>
    /// <param name="batchSelector">The async function to apply to each batch.</param>
    /// <param name="options">Configuration options for parallel execution. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing a list of all batch results.</returns>
    /// <exception cref="ArgumentException">Thrown when batchSize is less than 1.</exception>
    /// <exception cref="AggregateException">
    ///     Thrown when <see cref="ErrorMode.CollectAndContinue" /> is enabled and one or more
    ///     errors occurred during processing.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the cancellation token.</exception>
    public static Task<List<TResult>> BatchParallelAsync<TSource, TResult>(
        this IEnumerable<TSource> source,
        int batchSize,
        Func<IReadOnlyList<TSource>, CancellationToken, ValueTask<TResult>> batchSelector,
        ParallelOptionsRivulet? options = null,
        CancellationToken cancellationToken = default)
    {
        if (batchSize < 1) throw new ArgumentException("Batch size must be at least 1.", nameof(batchSize));

        var batches = CreateBatches(source, batchSize);
        return batches.SelectParallelAsync(batchSelector, options, cancellationToken);
    }

    /// <summary>
    ///     Groups items from an async stream into batches and processes each batch in parallel with bounded concurrency.
    ///     Returns results as a streaming async enumerable with built-in backpressure control.
    /// </summary>
    /// <typeparam name="TSource">The type of elements in the source collection.</typeparam>
    /// <typeparam name="TResult">The type of result returned by processing each batch.</typeparam>
    /// <param name="source">The async source enumerable to process.</param>
    /// <param name="batchSize">The maximum number of items in each batch.</param>
    /// <param name="batchSelector">The async function to apply to each batch.</param>
    /// <param name="options">Configuration options for parallel execution. If null, defaults are used.</param>
    /// <param name="batchTimeout">Optional timeout to flush incomplete batches. If null, only size triggers batching.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>An async enumerable that yields batch results as they complete.</returns>
    /// <exception cref="ArgumentException">Thrown when batchSize is less than 1.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the cancellation token.</exception>
    public static async IAsyncEnumerable<TResult> BatchParallelStreamAsync<TSource, TResult>(
        this IAsyncEnumerable<TSource> source,
        int batchSize,
        Func<IReadOnlyList<TSource>, CancellationToken, ValueTask<TResult>> batchSelector,
        ParallelOptionsRivulet? options = null,
        TimeSpan? batchTimeout = null,
        [EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        if (batchSize < 1) throw new ArgumentException("Batch size must be at least 1.", nameof(batchSize));

        var batches = CreateBatchesAsync(source, batchSize, batchTimeout, cancellationToken);
        await foreach (var result in batches.SelectParallelStreamAsync(batchSelector, options, cancellationToken).ConfigureAwait(false))
            yield return result;
    }

    private static IEnumerable<IReadOnlyList<TSource>> CreateBatches<TSource>(IEnumerable<TSource> source,
        int batchSize)
    {
        var batch = new List<TSource>(batchSize);
        foreach (var item in source)
        {
            batch.Add(item);
            if (batch.Count < batchSize) continue;

            yield return batch;

            batch = new(batchSize);
        }

        if (batch.Count > 0) yield return batch;
    }

    private static async IAsyncEnumerable<IReadOnlyList<TSource>> CreateBatchesAsync<TSource>(
        IAsyncEnumerable<TSource> source,
        int batchSize,
        TimeSpan? batchTimeout,
        [EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
        var batch = new List<TSource>(batchSize);

        if (batchTimeout.HasValue)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = cts.Token;
            var timeout = batchTimeout.Value;

            var channel = Channel.CreateBounded<IReadOnlyList<TSource>>(new BoundedChannelOptions(16)
            {
                SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait
            });

            var producer = Task.Run(async () =>
                {
                    try
                    {
                        var flushTimer = Task.Delay(timeout, token);

                        await foreach (var item in source.WithCancellation(token).ConfigureAwait(false))
                        {
                            batch.Add(item);

                            if (batch.Count < batchSize && (!flushTimer.IsCompleted || batch.Count <= 0)) continue;

                            await channel.Writer.WriteAsync(batch, token).ConfigureAwait(false);
                            batch = new(batchSize);
                            flushTimer = Task.Delay(timeout, token);
                        }

                        if (batch.Count > 0) await channel.Writer.WriteAsync(batch, token).ConfigureAwait(false);
                    }
                    finally
                    {
                        channel.Writer.TryComplete();
                    }
                },
                token);

            await foreach (var batchResult in channel.Reader.ReadAllAsync(token).ConfigureAwait(false))
                yield return batchResult;

            await producer.ConfigureAwait(false);
        }
        else
        {
            await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                batch.Add(item);
                if (batch.Count < batchSize) continue;

                yield return batch;

                batch = new(batchSize);
            }

            if (batch.Count > 0) yield return batch;
        }
    }

    /// <summary>
    ///     Infrastructure components for parallel processing operations.
    /// </summary>
    private readonly record struct ParallelInfrastructure(
        ProgressTracker? ProgressTracker,
        MetricsTrackerBase MetricsTracker,
        TokenBucket? TokenBucket,
        CircuitBreaker? CircuitBreaker,
        AdaptiveConcurrencyController? AdaptiveController,
        int EffectiveMaxWorkers
    ) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            if (ProgressTracker is not null)
                await ProgressTracker.DisposeAsync().ConfigureAwait(false);

            await MetricsTracker.DisposeAsync().ConfigureAwait(false);

            if (AdaptiveController is not null)
                await AdaptiveController.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Initializes all infrastructure components needed for parallel processing.
    /// </summary>
    private static ParallelInfrastructure InitializeInfrastructure(
        ParallelOptionsRivulet options,
        int? totalItems,
        CancellationToken token)
    {
        var progressTracker = options.Progress is not null
            ? new ProgressTracker(totalItems, options.Progress, token)
            : null;

        var metricsTracker = MetricsTrackerBase.Create(options.Metrics, token);

        var tokenBucket = options.RateLimit is not null
            ? new TokenBucket(options.RateLimit)
            : null;

        var circuitBreaker = options.CircuitBreaker is not null
            ? new CircuitBreaker(options.CircuitBreaker)
            : null;

        var adaptiveController = options.AdaptiveConcurrency is not null
            ? new AdaptiveConcurrencyController(options.AdaptiveConcurrency)
            : null;

        var effectiveMaxWorkers = options.AdaptiveConcurrency?.MaxConcurrency
                                  ?? options.MaxDegreeOfParallelism;
        metricsTracker.SetActiveWorkers(effectiveMaxWorkers);

        return new ParallelInfrastructure(
            progressTracker,
            metricsTracker,
            tokenBucket,
            circuitBreaker,
            adaptiveController,
            effectiveMaxWorkers);
    }

    /// <summary>
    ///     Handles errors that occur during item processing.
    /// </summary>
    private static async ValueTask HandleProcessingErrorAsync(
        Exception ex,
        int idx,
        ParallelOptionsRivulet options,
        ParallelInfrastructure infrastructure,
        CancellationTokenSource cts,
        ConcurrentBag<Exception>? errors
    )
    {
        errors?.Add(ex);

        infrastructure.ProgressTracker?.IncrementErrors();
        infrastructure.MetricsTracker.IncrementFailures();

        if (options.OnErrorAsync is not null)
        {
            var cont = await options.OnErrorAsync(idx, ex).ConfigureAwait(false);
            if (!cont) await cts.CancelAsync().ConfigureAwait(false);
        }

        if (options.ErrorMode == ErrorMode.FailFast)
        {
            await cts.CancelAsync().ConfigureAwait(false);
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
        }
    }

    /// <summary>
    ///     Releases adaptive concurrency controller after item processing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReleaseAdaptiveController(
        AdaptiveConcurrencyController? controller,
        bool acquired,
        long startTime,
        bool success)
    {
        if (controller is null || !acquired)
            return;

        var elapsed = Stopwatch.GetElapsedTime(startTime);
        controller.Release(elapsed, success);
    }
}