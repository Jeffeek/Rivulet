using System.Runtime.CompilerServices;

namespace Rivulet.Pipeline.Internal;

/// <summary>
/// Helper methods for stage execution to reduce boilerplate code.
/// </summary>
internal static class StageExecutionHelper
{
    /// <summary>
    /// Executes a typed stage on untyped input by converting types.
    /// This eliminates duplicate code in each stage's ExecuteUntypedAsync implementation.
    /// </summary>
    public static async IAsyncEnumerable<object> ExecuteUntypedAsync<TIn, TOut>(
        IAsyncEnumerable<object> input,
        Func<IAsyncEnumerable<TIn>, IAsyncEnumerable<TOut>> typedExecutor,
        [EnumeratorCancellation]
        CancellationToken cancellationToken
    )
    {
        var typedInput = input.Select(static x => (TIn)x);

        await foreach (var result in typedExecutor(typedInput).WithCancellation(cancellationToken).ConfigureAwait(false))
            yield return result!;
    }
}
