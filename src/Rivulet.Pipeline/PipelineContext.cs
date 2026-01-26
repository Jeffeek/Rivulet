using System.Collections.Concurrent;
using System.Diagnostics;
using Rivulet.Core;
using Rivulet.Pipeline.Results;

namespace Rivulet.Pipeline;

/// <summary>
/// Shared context for pipeline execution containing pipeline-wide resources and tracking.
/// </summary>
public sealed class PipelineContext : IAsyncDisposable
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly ConcurrentDictionary<string, StageMetrics> _stageMetrics = new();

    /// <summary>
    /// Gets the pipeline name.
    /// </summary>
    public string PipelineName { get; }

    /// <summary>
    /// Gets the unique execution ID for this pipeline run.
    /// </summary>
    public Guid ExecutionId { get; } = Guid.NewGuid();

    /// <summary>
    /// Gets the UTC timestamp when the pipeline started.
    /// </summary>
    public DateTimeOffset StartTime { get; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the elapsed time since the pipeline started.
    /// </summary>
    public TimeSpan Elapsed => _stopwatch.Elapsed;

    /// <summary>
    /// Gets the default stage options from the pipeline configuration.
    /// </summary>
    internal ParallelOptionsRivulet DefaultStageOptions { get; }

    /// <summary>
    /// Gets the pipeline options.
    /// </summary>
    internal PipelineOptions Options { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineContext"/> class.
    /// </summary>
    /// <param name="options">The pipeline options.</param>
    internal PipelineContext(PipelineOptions options)
    {
        Options = options;
        PipelineName = options.Name;
        DefaultStageOptions = options.DefaultStageOptions;
    }

    /// <summary>
    /// Records metrics for a stage.
    /// </summary>
    internal void RecordStageMetrics(string stageName, StageMetrics metrics) =>
        _stageMetrics[stageName] = metrics;

    /// <summary>
    /// Gets or creates metrics tracking for a stage.
    /// </summary>
    internal StageMetrics GetOrCreateStageMetrics(string stageName, int stageIndex) =>
        _stageMetrics.GetOrAdd(stageName, _ => new StageMetrics(stageName, stageIndex));

    /// <summary>
    /// Creates the pipeline result summary.
    /// </summary>
    internal PipelineResult CreateResult()
    {
        _stopwatch.Stop();

        var stageResults = _stageMetrics.Values
            .OrderBy(static m => m.StageIndex)
            .Select(static m => m.ToStageResult())
            .ToList();

        var totalIn = stageResults.FirstOrDefault()?.ItemsIn ?? 0;
        var totalOut = stageResults.LastOrDefault()?.ItemsOut ?? 0;
        var totalFailed = stageResults.Sum(static r => r.ItemsFailed);

        return new PipelineResult
        {
            ItemsProcessed = totalIn,
            ItemsCompleted = totalOut,
            ItemsFailed = totalFailed,
            Elapsed = _stopwatch.Elapsed,
            StageResults = stageResults
        };
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _stopwatch.Stop();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Internal metrics tracking for a single stage.
/// </summary>
internal sealed class StageMetrics(string stageName, int stageIndex)
{
    private readonly Stopwatch _stopwatch = new();
    private long _itemsIn;
    private long _itemsOut;
    private long _itemsFailed;
    private long _totalRetries;

    public string StageName { get; } = stageName;
    public int StageIndex { get; } = stageIndex;

    public void Start() => _stopwatch.Start();
    public void Stop() => _stopwatch.Stop();

    public void IncrementItemsIn() => Interlocked.Increment(ref _itemsIn);
    public void IncrementItemsOut() => Interlocked.Increment(ref _itemsOut);
    public void IncrementItemsFailed() => Interlocked.Increment(ref _itemsFailed);
    public void IncrementRetries() => Interlocked.Increment(ref _totalRetries);

    public StageResult ToStageResult() => new()
    {
        StageName = StageName,
        StageIndex = StageIndex,
        ItemsIn = Interlocked.Read(ref _itemsIn),
        ItemsOut = Interlocked.Read(ref _itemsOut),
        ItemsFailed = Interlocked.Read(ref _itemsFailed),
        TotalRetries = Interlocked.Read(ref _totalRetries),
        Elapsed = _stopwatch.Elapsed
    };
}
