using System.Runtime.CompilerServices;
using Rivulet.Core.Observability;

namespace Rivulet.Core.Internal;

/// <summary>
///     Helper methods for safe callback invocation with automatic error logging.
/// </summary>
internal static class CallbackHelper
{
    /// <summary>
    ///     Invokes a callback in a fire-and-forget manner with automatic error logging.
    ///     Used for state change notifications that should not block the caller.
    /// </summary>
    /// <typeparam name="T1">Type of the first argument.</typeparam>
    /// <typeparam name="T2">Type of the second argument.</typeparam>
    /// <param name="callback">The callback to invoke.</param>
    /// <param name="arg1">First argument to pass to the callback.</param>
    /// <param name="arg2">Second argument to pass to the callback.</param>
    /// <param name="callbackName">Name of the callback for error logging.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void InvokeFireAndForget<T1, T2>(
        Func<T1, T2, ValueTask>? callback,
        T1 arg1,
        T2 arg2,
        string callbackName
    )
    {
        if (callback is null) return;

        _ = Task.Run(async () =>
            {
                try
                {
                    await callback(arg1, arg2).ConfigureAwait(false);
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    // User callbacks can throw any exception type - must catch all and log via EventSource
                    RivuletEventSource.Log.CallbackFailed(callbackName, ex.GetType().Name, ex.Message);
                }
            },
            CancellationToken.None);
    }

    /// <summary>
    ///     Invokes a callback safely with automatic error logging.
    ///     Used for synchronous callback invocations that should await completion.
    /// </summary>
    /// <typeparam name="T">Type of the argument.</typeparam>
    /// <param name="callback">The callback to invoke.</param>
    /// <param name="arg">Argument to pass to the callback.</param>
    /// <param name="callbackName">Name of the callback for error logging.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static async ValueTask InvokeSafelyAsync<T>(
        Func<T, ValueTask>? callback,
        T arg,
        string callbackName
    )
    {
        if (callback is null) return;

        try
        {
            await callback(arg).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // User callbacks can throw any exception type - must catch all and log via EventSource
            RivuletEventSource.Log.CallbackFailed(callbackName, ex.GetType().Name, ex.Message);
        }
    }
}
