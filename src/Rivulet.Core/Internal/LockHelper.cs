namespace Rivulet.Core.Internal;

/// <summary>
///     Helper class for cross-platform locking that supports both .NET 9+ Lock and traditional object locks.
/// </summary>
internal static class LockHelper
{
#if NET9_0_OR_GREATER
    /// <summary>
    ///     Executes an action within a lock scope using .NET 9+ Lock.
    /// </summary>
    /// <param name="lock">The Lock instance (wrapped in object) to use for synchronization.</param>
    /// <param name="action">The action to execute while holding the lock.</param>
    public static void Execute(object @lock, Action action)
    {
        var lockObj = (Lock)@lock;
        lockObj.Enter();
        try
        {
            action();
        }
        finally
        {
            lockObj.Exit();
        }
    }

    /// <summary>
    ///     Executes a function within a lock scope using .NET 9+ Lock and returns a result.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="lock">The Lock instance (wrapped in object) to use for synchronization.</param>
    /// <param name="func">The function to execute while holding the lock.</param>
    /// <returns>The result of the function.</returns>
    public static T Execute<T>(object @lock, Func<T> func)
    {
        var lockObj = (Lock)@lock;
        lockObj.Enter();
        try
        {
            return func();
        }
        finally
        {
            lockObj.Exit();
        }
    }
#else
    /// <summary>
    /// Executes an action within a lock scope using traditional object lock.
    /// </summary>
    /// <param name="lock">The object to use for synchronization.</param>
    /// <param name="action">The action to execute while holding the lock.</param>
    public static void Execute(object @lock, Action action)
    {
        lock (@lock) action();
    }

    /// <summary>
    /// Executes a function within a lock scope using traditional object lock and returns a result.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="lock">The object to use for synchronization.</param>
    /// <param name="func">The function to execute while holding the lock.</param>
    /// <returns>The result of the function.</returns>
    public static T Execute<T>(object @lock, Func<T> func)
    {
        lock (@lock) return func();
    }
#endif
}