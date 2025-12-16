using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rivulet.Core;

namespace Rivulet.Hosting;

/// <summary>
///     Background service that continuously processes items from a source using parallel operations.
/// </summary>
public abstract class ParallelWorkerService<TSource, TResult> : BackgroundService
{
    private readonly ILogger _logger;

    /// <summary>
    ///     Initializes a new instance of the ParallelWorkerService class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">Optional parallel processing options.</param>
    protected ParallelWorkerService(ILogger logger, ParallelOptionsRivulet? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Options = options ?? new ParallelOptionsRivulet { MaxDegreeOfParallelism = Environment.ProcessorCount };
    }

    /// <summary>
    ///     Gets the parallel options used by this worker service.
    /// </summary>
    // ReSharper disable once MemberCanBePrivate.Global
    protected ParallelOptionsRivulet Options { get; }

    /// <summary>
    ///     Executes the worker service asynchronously.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token that triggers when the application is stopping.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting worker service {ServiceName} with MaxDegreeOfParallelism={Parallelism}",
            GetType().Name,
            Options.MaxDegreeOfParallelism);

        try
        {
            var source = GetSourceItems(stoppingToken);

            await source.SelectParallelStreamAsync(
                    async (item, ct) =>
                    {
                        var result = await ProcessAsync(item, ct).ConfigureAwait(false);
                        await OnResultAsync(result, ct).ConfigureAwait(false);
                        return result;
                    },
                    Options,
                    stoppingToken)
                .CountAsync(stoppingToken)
                .ConfigureAwait(false);

            _logger.LogInformation("Worker service {ServiceName} completed", GetType().Name);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Worker service {ServiceName} is stopping", GetType().Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in worker service {ServiceName}", GetType().Name);
            throw;
        }
    }

    /// <summary>
    ///     Gets the source items to process.
    /// </summary>
    protected abstract IAsyncEnumerable<TSource> GetSourceItems(CancellationToken cancellationToken);

    /// <summary>
    ///     Processes a single item and returns the result.
    /// </summary>
    protected abstract Task<TResult> ProcessAsync(TSource item, CancellationToken cancellationToken);

    /// <summary>
    ///     Called when a result is available. Override to handle results (e.g., save to database, send to queue).
    /// </summary>
    protected virtual Task OnResultAsync(TResult result, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}