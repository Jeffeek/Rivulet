﻿using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace Rivulet.Core;

/// <summary>
/// Provides async-first parallel LINQ operators with bounded concurrency, retries, and backpressure for I/O-heavy workloads.
/// </summary>
[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public static class AsyncParallelLinq
{
    /// <summary>
    /// Applies a transformation function to each element in a collection in parallel with bounded concurrency,
    /// returning a materialized list of results. Supports retry policies, per-item timeouts, and configurable error handling.
    /// </summary>
    /// <typeparam name="TSource">The type of elements in the source collection.</typeparam>
    /// <typeparam name="TResult">The type of elements in the result collection.</typeparam>
    /// <param name="source">The source enumerable to process.</param>
    /// <param name="taskSelector">The async transformation function to apply to each element.</param>
    /// <param name="options">Configuration options for parallel execution, including concurrency limits, retry policies, and lifecycle hooks. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing a list of all transformed results.</returns>
    /// <exception cref="AggregateException">Thrown when <see cref="ErrorMode.CollectAndContinue"/> is enabled and one or more errors occurred during processing.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the cancellation token.</exception>
    public static async Task<List<TResult>> SelectParallelAsync<TSource, TResult>(
        this IEnumerable<TSource> source,
        Func<TSource, CancellationToken, ValueTask<TResult>> taskSelector,
        ParallelOptionsRivulet? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ParallelOptionsRivulet();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = cts.Token;

        var results = options.OrderedOutput
            ? null
            : new ConcurrentBag<TResult>();
        var orderedResults = options.OrderedOutput
            ? new ConcurrentDictionary<int, TResult>()
            : null;
        var errors = new ConcurrentBag<Exception>();

        var sourceList = source as ICollection<TSource> ?? source.ToList();
        var totalItems = sourceList.Count;

        var progressTracker = options.Progress is not null
            ? new ProgressTracker(totalItems, options.Progress, token)
            : null;

        var metricsTracker = new MetricsTracker(options.Metrics, token);
        metricsTracker.SetActiveWorkers(options.MaxDegreeOfParallelism);

        var tokenBucket = options.RateLimit is not null
            ? new TokenBucket(options.RateLimit)
            : null;

        var circuitBreaker = options.CircuitBreaker is not null
            ? new CircuitBreaker(options.CircuitBreaker)
            : null;

        var channel = Channel.CreateBounded<(int idx, TSource item)>(new BoundedChannelOptions(options.ChannelCapacity)
        {
            SingleReader = false,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        });

        var writerTask = Task.Run(async () =>
        {
            var i = 0;
            try
            {
                foreach (var item in sourceList)
                {
                    token.ThrowIfCancellationRequested();
                    if (!await channel.Writer.WaitToWriteAsync(token))
                        break;

                    await channel.Writer.WriteAsync((i, item), token);
                    if (i++ % options.MaxDegreeOfParallelism == 0 && options.OnThrottleAsync is not null)
                    {
                        metricsTracker.IncrementThrottleEvents();
                        await options.OnThrottleAsync(i);
                    }
                }
            }
            finally
            {
                channel.Writer.TryComplete();
            }
        }, token);

        var workers = Enumerable.Range(0, options.MaxDegreeOfParallelism)
            .Select(_ => Task.Run(async () =>
            {
                await foreach (var (idx, item) in channel.Reader.ReadAllAsync(token))
                {
                    try
                    {
                        if (tokenBucket is not null)
                            await tokenBucket.AcquireAsync(token);

                        progressTracker?.IncrementStarted();
                        metricsTracker.IncrementItemsStarted();
                        if (options.OnStartItemAsync is not null) await options.OnStartItemAsync(idx);

                        TResult result;
                        if (circuitBreaker is not null)
                        {
                            result = await circuitBreaker.ExecuteAsync(async () =>
                                await RetryPolicy.ExecuteWithRetry(item, taskSelector, options, metricsTracker, token), token);
                        }
                        else
                        {
                            result = await RetryPolicy.ExecuteWithRetry(item, taskSelector, options, metricsTracker, token);
                        }

                        if (options.OrderedOutput)
                            orderedResults![idx] = result;
                        else
                            results!.Add(result);

                        progressTracker?.IncrementCompleted();
                        metricsTracker.IncrementItemsCompleted();
                        if (options.OnCompleteItemAsync is not null) await options.OnCompleteItemAsync(idx);
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                        progressTracker?.IncrementErrors();
                        metricsTracker.IncrementFailures();

                        if (options.OnErrorAsync is not null)
                        {
                            var cont = await options.OnErrorAsync(idx, ex);
                            if (!cont)
                                await cts.CancelAsync();
                        }

                        if (options.ErrorMode == ErrorMode.FailFast)
                        {
                            await cts.CancelAsync();
                            throw;
                        }
                    }
                }
            }, token)).ToArray();

        try
        {
            await Task.WhenAll(workers.Prepend(writerTask));
        }
        catch when (options.ErrorMode != ErrorMode.FailFast)
        {
            // Swallow here; handled by mode below
        }
        finally
        {
            progressTracker?.Dispose();
            metricsTracker.Dispose();
        }

        if (options.ErrorMode == ErrorMode.CollectAndContinue && errors.Count > 0)
            throw new AggregateException(errors);

        return options.OrderedOutput
            ? orderedResults!.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value).ToList()
            : results!.ToList();
    }

    /// <summary>
    /// Applies a transformation function to each element in an async stream in parallel with bounded concurrency,
    /// returning results as a streaming async enumerable with built-in backpressure control.
    /// Results may be yielded out-of-order relative to the input sequence unless <see cref="ParallelOptionsRivulet.OrderedOutput"/> is true.
    /// </summary>
    /// <typeparam name="TSource">The type of elements in the source collection.</typeparam>
    /// <typeparam name="TResult">The type of elements in the result stream.</typeparam>
    /// <param name="source">The async source enumerable to process.</param>
    /// <param name="taskSelector">The async transformation function to apply to each element.</param>
    /// <param name="options">Configuration options for parallel execution, including concurrency limits, retry policies, and lifecycle hooks. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>An async enumerable that yields transformed results as they complete.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the cancellation token.</exception>
    public static async IAsyncEnumerable<TResult> SelectParallelStreamAsync<TSource, TResult>(
        this IAsyncEnumerable<TSource> source,
        Func<TSource, CancellationToken, ValueTask<TResult>> taskSelector,
        ParallelOptionsRivulet? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new ParallelOptionsRivulet();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = cts.Token;

        var progressTracker = options.Progress is not null
            ? new ProgressTracker(null, options.Progress, token)
            : null;

        var metricsTracker = new MetricsTracker(options.Metrics, token);
        metricsTracker.SetActiveWorkers(options.MaxDegreeOfParallelism);

        var tokenBucket = options.RateLimit is not null
            ? new TokenBucket(options.RateLimit)
            : null;

        var circuitBreaker = options.CircuitBreaker is not null
            ? new CircuitBreaker(options.CircuitBreaker)
            : null;

        var input = Channel.CreateBounded<(int idx, TSource item)>(new BoundedChannelOptions(options.ChannelCapacity)
        {
            SingleReader = false,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        });
        var output = Channel.CreateBounded<(int idx, TResult result)>(new BoundedChannelOptions(options.ChannelCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        var writer = Task.Run(async () =>
        {
            var i = 0;
            try
            {
                await foreach (var item in source.WithCancellation(token))
                {
                    token.ThrowIfCancellationRequested();
                    await input.Writer.WriteAsync((i++, item), token);
                }
            }
            finally
            {
                input.Writer.TryComplete();
            }
        }, token);

        var workers = Enumerable.Range(0, options.MaxDegreeOfParallelism).Select(_ => Task.Run(async () =>
        {
            await foreach (var (idx, item) in input.Reader.ReadAllAsync(token))
            {
                try
                {
                    if (tokenBucket is not null)
                        await tokenBucket.AcquireAsync(token);

                    progressTracker?.IncrementStarted();
                    metricsTracker.IncrementItemsStarted();
                    if (options.OnStartItemAsync is not null) await options.OnStartItemAsync(idx);

                    TResult res;
                    if (circuitBreaker is not null)
                    {
                        res = await circuitBreaker.ExecuteAsync(async () =>
                            await RetryPolicy.ExecuteWithRetry(item, taskSelector, options, metricsTracker, token), token);
                    }
                    else
                    {
                        res = await RetryPolicy.ExecuteWithRetry(item, taskSelector, options, metricsTracker, token);
                    }

                    await output.Writer.WriteAsync((idx, res), token);
                    progressTracker?.IncrementCompleted();
                    metricsTracker.IncrementItemsCompleted();
                    if (options.OnCompleteItemAsync is not null) await options.OnCompleteItemAsync(idx);
                }
                catch (Exception ex)
                {
                    progressTracker?.IncrementErrors();
                    metricsTracker.IncrementFailures();

                    if (options.OnErrorAsync is not null)
                    {
                        var cont = await options.OnErrorAsync(idx, ex);
                        if (!cont) await cts.CancelAsync();
                    }

                    switch (options.ErrorMode)
                    {
                        case ErrorMode.FailFast:
                            await cts.CancelAsync();
                            throw;
                        case ErrorMode.CollectAndContinue or ErrorMode.BestEffort:
                            break;
                    }
                }
            }
        }, token)).ToArray();

        var reader = Task.Run(async () =>
        {
            try
            {
                await Task.WhenAll(workers);
            }
            finally
            {
                output.Writer.TryComplete();
                progressTracker?.Dispose();
                metricsTracker.Dispose();
            }
        }, token);

        if (options.OrderedOutput)
        {
            var buffer = new Dictionary<int, TResult>();
            var nextIndex = 0;

            await foreach (var (idx, result) in output.Reader.ReadAllAsync(token))
            {
                token.ThrowIfCancellationRequested();

                buffer[idx] = result;

                while (buffer.Remove(nextIndex, out var orderedResult))
                {
                    yield return orderedResult;
                    nextIndex++;
                }
            }

            // Yield any remaining buffered results in order
            foreach (var idx in buffer.Keys.OrderBy(k => k))
            {
                yield return buffer[idx];
            }
        }
        else
        {
            await foreach (var (_, result) in output.Reader.ReadAllAsync(token))
            {
                token.ThrowIfCancellationRequested();
                yield return result;
            }
        }

        await Task.WhenAll(writer, reader);
    }

    /// <summary>
    /// Executes an async action on each element in a stream in parallel with bounded concurrency.
    /// No results are returned; this is suitable for side effect operations like logging, updates, or fire-and-forget processing.
    /// </summary>
    /// <typeparam name="TSource">The type of elements in the source collection.</typeparam>
    /// <param name="source">The async source enumerable to process.</param>
    /// <param name="action">The async action to execute for each element.</param>
    /// <param name="options">Configuration options for parallel execution, including concurrency limits, retry policies, and lifecycle hooks. If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that completes when all items have been processed.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the cancellation token.</exception>
    public static async Task ForEachParallelAsync<TSource>(
        this IAsyncEnumerable<TSource> source,
        Func<TSource, CancellationToken, ValueTask> action,
        ParallelOptionsRivulet? options = null,
        CancellationToken cancellationToken = default) =>
        await SelectParallelStreamAsync(
            source,
            async (item, ct) => { await action(item, ct); return true; },
            options,
            cancellationToken
        ).CollectAsync(cancellationToken);

    private static async Task CollectAsync<T>(this IAsyncEnumerable<T> source, CancellationToken ct)
    {
        await foreach (var _ in source.WithCancellation(ct)) { }
    }

    /// <summary>
    /// Groups items into batches and processes each batch in parallel with bounded concurrency.
    /// Returns a materialized list of all batch results.
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
    /// <exception cref="AggregateException">Thrown when <see cref="ErrorMode.CollectAndContinue"/> is enabled and one or more errors occurred during processing.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the cancellation token.</exception>
    public static async Task<List<TResult>> BatchParallelAsync<TSource, TResult>(
        this IEnumerable<TSource> source,
        int batchSize,
        Func<IReadOnlyList<TSource>, CancellationToken, ValueTask<TResult>> batchSelector,
        ParallelOptionsRivulet? options = null,
        CancellationToken cancellationToken = default)
    {
        if (batchSize < 1)
            throw new ArgumentException("Batch size must be at least 1.", nameof(batchSize));

        var batches = CreateBatches(source, batchSize);
        return await batches.SelectParallelAsync(batchSelector, options, cancellationToken);
    }

    /// <summary>
    /// Groups items from an async stream into batches and processes each batch in parallel with bounded concurrency.
    /// Returns results as a streaming async enumerable with built-in backpressure control.
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
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (batchSize < 1)
            throw new ArgumentException("Batch size must be at least 1.", nameof(batchSize));

        var batches = CreateBatchesAsync(source, batchSize, batchTimeout, cancellationToken);
        await foreach (var result in batches.SelectParallelStreamAsync(batchSelector, options, cancellationToken))
        {
            yield return result;
        }
    }

    private static IEnumerable<IReadOnlyList<TSource>> CreateBatches<TSource>(IEnumerable<TSource> source, int batchSize)
    {
        var batch = new List<TSource>(batchSize);
        foreach (var item in source)
        {
            batch.Add(item);
            if (batch.Count < batchSize) continue;

            yield return batch;
            batch = new List<TSource>(batchSize);
        }

        if (batch.Count > 0)
        {
            yield return batch;
        }
    }

    private static async IAsyncEnumerable<IReadOnlyList<TSource>> CreateBatchesAsync<TSource>(
        IAsyncEnumerable<TSource> source,
        int batchSize,
        TimeSpan? batchTimeout,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var batch = new List<TSource>(batchSize);

        if (batchTimeout.HasValue)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = cts.Token;

            var channel = Channel.CreateBounded<IReadOnlyList<TSource>>(new BoundedChannelOptions(16)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            });

            var producer = Task.Run(async () =>
            {
                try
                {
                    var flushTimer = Task.Delay(batchTimeout.Value, token);

                    await foreach (var item in source.WithCancellation(token))
                    {
                        batch.Add(item);
                        _ = DateTime.UtcNow;

                        if (batch.Count >= batchSize)
                        {
                            await channel.Writer.WriteAsync(batch, token);
                            batch = new List<TSource>(batchSize);
                            _ = DateTime.UtcNow;
                            flushTimer = Task.Delay(batchTimeout.Value, token);
                        }
                        else if (flushTimer.IsCompleted && batch.Count > 0)
                        {
                            await channel.Writer.WriteAsync(batch, token);
                            batch = new List<TSource>(batchSize);
                            _ = DateTime.UtcNow;
                            flushTimer = Task.Delay(batchTimeout.Value, token);
                        }
                    }

                    if (batch.Count > 0)
                    {
                        await channel.Writer.WriteAsync(batch, token);
                    }
                }
                finally
                {
                    channel.Writer.TryComplete();
                }
            }, token);

            await foreach (var batchResult in channel.Reader.ReadAllAsync(token))
            {
                yield return batchResult;
            }

            await producer;
        }
        else
        {
            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                batch.Add(item);
                if (batch.Count < batchSize) continue;

                yield return batch;
                batch = new List<TSource>(batchSize);
            }

            if (batch.Count > 0)
            {
                yield return batch;
            }
        }
    }
}
