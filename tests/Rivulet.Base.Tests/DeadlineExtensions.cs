namespace Rivulet.Base.Tests;

public static class DeadlineExtensions
{
    // ReSharper disable once UnusedMember.Global
    public static void ApplyDeadline(DateTime deadlineUtc, Action action, Func<bool> additionalConditional)
    {
        while (DateTime.UtcNow < deadlineUtc && additionalConditional()) action();
    }

    public static async Task ApplyDeadlineAsync(DateTime deadlineUtc, Func<Task> task, Func<bool> additionalConditional)
    {
        while (DateTime.UtcNow < deadlineUtc && additionalConditional()) await task();
    }
}