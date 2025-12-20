using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rivulet.Core;
using Rivulet.Hosting.Internal;

namespace Rivulet.Hosting;

/// <summary>
///     Base class for background services that process items in parallel.
/// </summary>
public abstract class ParallelBackgroundService<T> : BackgroundService
{
    private readonly ILogger _logger;
    private readonly ParallelOptionsRivulet? _options;

    /// <summary>
    ///     Initializes a new instance of the ParallelBackgroundService class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">Optional parallel processing options.</param>
    protected ParallelBackgroundService(ILogger logger, ParallelOptionsRivulet? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options;
    }

    /// <summary>
    ///     Executes the background service asynchronously.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token that triggers when the application is stopping.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting {ServiceName}", GetType().Name);

        return BackgroundServiceExecutionHelper.ExecuteWithErrorHandlingAsync(
            ct => GetItemsAsync(ct).ForEachParallelAsync(ProcessItemAsync, _options, ct),
            GetType().Name,
            _logger,
            stoppingToken);
    }

    /// <summary>
    ///     Gets the stream of items to process. Override this to provide your data source.
    /// </summary>
    protected abstract IAsyncEnumerable<T> GetItemsAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Processes a single item. Override this to define your processing logic.
    /// </summary>
    protected abstract ValueTask ProcessItemAsync(T item, CancellationToken cancellationToken);
}