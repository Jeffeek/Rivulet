namespace Rivulet.Pipeline.Internal;

/// <summary>
/// Internal non-generic interface for pipeline stages to enable storage in collections.
/// </summary>
internal interface IInternalPipelineStage
{
    /// <summary>
    /// Gets the stage name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the stage options.
    /// </summary>
    StageOptions Options { get; }

    /// <summary>
    /// Executes the stage with object-typed input and output for internal chaining.
    /// </summary>
    IAsyncEnumerable<object> ExecuteUntypedAsync(
        IAsyncEnumerable<object> input,
        PipelineContext context,
        CancellationToken cancellationToken
    );
}
