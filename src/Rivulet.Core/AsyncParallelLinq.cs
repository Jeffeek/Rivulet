using System.Collections.Concurrent;
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
                foreach (var item in source)
                {
                    token.ThrowIfCancellationRequested();
                    if (!await channel.Writer.WaitToWriteAsync(token))
                        break;

                    await channel.Writer.WriteAsync((i, item), token);
                    if (i++ % options.MaxDegreeOfParallelism == 0 && options.OnThrottleAsync is not null)
                        await options.OnThrottleAsync(i);
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
                        if (options.OnStartItemAsync is not null) await options.OnStartItemAsync(idx);
                        var result = await RetryPolicy.ExecuteWithRetry(item, idx, taskSelector, options, token);

                        if (options.OrderedOutput)
                            orderedResults![idx] = result;
                        else
                            results!.Add(result);

                        if (options.OnCompleteItemAsync is not null) await options.OnCompleteItemAsync(idx);
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);

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
                    if (options.OnStartItemAsync is not null) await options.OnStartItemAsync(idx);
                    var res = await RetryPolicy.ExecuteWithRetry(item, idx, taskSelector, options, token);
                    await output.Writer.WriteAsync((idx, res), token);
                    if (options.OnCompleteItemAsync is not null) await options.OnCompleteItemAsync(idx);
                }
                catch (Exception ex)
                {
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
}
