using System.Runtime.CompilerServices;

namespace Rivulet.Core.Internal;

/// <summary>
///     Factory for creating lock objects using the appropriate type for the target framework.
/// </summary>
internal static class LockFactory
{
    /// <summary>
    ///     Creates a lock object. On .NET 9+, uses the new Lock type; otherwise uses object.
    /// </summary>
    /// <returns>A lock object suitable for use with LockHelper.Execute.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static object CreateLock() =>
#if NET9_0_OR_GREATER
#pragma warning disable CS9216 // A value of type 'System.Threading.Lock' converted to a different type will use likely unintended monitor-based locking in 'lock' statement
        new Lock();
#pragma warning restore CS9216 // A value of type 'System.Threading.Lock' converted to a different type will use likely unintended monitor-based locking in 'lock' statement
#else
        new();
#endif
}
