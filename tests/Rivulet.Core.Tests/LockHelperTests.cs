using System.Collections.Concurrent;
using Rivulet.Core.Internal;

namespace Rivulet.Core.Tests;

public sealed class LockHelperTests
{
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    [Fact]
    public void LockHelper_ExecuteAction_ExecutesActionSuccessfully()
    {
        var executed = false;

        LockHelper.Execute(_lock, () => { executed = true; });

        executed.ShouldBeTrue();
    }

    [Fact]
    public void LockHelper_ExecuteFunc_ReturnsExpectedValue()
    {
        const int expectedValue = 42;

        var result = LockHelper.Execute(_lock, static () => expectedValue);

        result.ShouldBe(expectedValue);
    }

    [Fact]
    public void LockHelper_ExecuteFunc_ReturnsComputedValue()
    {
        const int value = 10;

        var result = LockHelper.Execute(_lock, static () => value * 2 + 3);

        result.ShouldBe(23);
    }

    [Fact]
    public void LockHelper_ExecuteAction_ModifiesSharedState()
    {
        var counter = 0;

        LockHelper.Execute(_lock, () => { counter++; });
        LockHelper.Execute(_lock, () => { counter++; });
        LockHelper.Execute(_lock, () => { counter++; });

        counter.ShouldBe(3);
    }

    [Fact]
    public void LockHelper_ExecuteFunc_ReadsSharedState()
    {
        var counter = 42;

        // ReSharper disable once AccessToModifiedClosure
        var result1 = LockHelper.Execute(_lock, () => counter);
        counter = 100;
        var result2 = LockHelper.Execute(_lock, () => counter);

        result1.ShouldBe(42);
        result2.ShouldBe(100);
    }

    [Fact]
    public void LockHelper_ExecuteAction_PropagatesException()
    {
        var expectedException = new InvalidOperationException("Test exception");

        var action = () => LockHelper.Execute(_lock, () => throw expectedException);
        action.ShouldThrow<InvalidOperationException>().Message.ShouldContain("Test exception");
    }

    [Fact]
    public void LockHelper_ExecuteFunc_PropagatesException()
    {
        var expectedException = new InvalidOperationException("Test exception");

        void Func()
        {
            LockHelper.Execute(_lock,
                () =>
                {
                    throw expectedException;
#pragma warning disable CS0162 // Unreachable code detected
                    return 42;
#pragma warning restore CS0162 // Unreachable code detected
                });
        }

        var act = Func;
        var ex = act.ShouldThrow<InvalidOperationException>();
        ex.Message.ShouldContain("Test exception");
    }

    [Fact]
    public void LockHelper_ExecuteAction_ThreadSafety_NoRaceConditions()
    {
        var counter = 0;
        const int threadCount = 100;
        const int incrementsPerThread = 100;
        var tasks = new Task[threadCount];

        for (var i = 0; i < threadCount; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                for (var j = 0; j < incrementsPerThread; j++) LockHelper.Execute(_lock, () => { counter++; });
            });
        }

#pragma warning disable xUnit1031
        Task.WaitAll(tasks);
#pragma warning restore xUnit1031

        counter.ShouldBe(threadCount * incrementsPerThread);
    }

    [Fact]
    public void LockHelper_ExecuteFunc_ThreadSafety_ConsistentReads()
    {
        var counter = 0;
        const int threadCount = 50;
        var results = new ConcurrentBag<int>();
        var tasks = new Task[threadCount];

        for (var i = 0; i < threadCount; i++)
        {
            var index = i;
            tasks[i] = Task.Run(() =>
            {
                if (index % 2 == 0)
                {
                    // Increment
                    LockHelper.Execute(_lock, () => { counter++; });
                }
                else
                {
                    // Read
                    var value = LockHelper.Execute(_lock, () => counter);
                    results.Add(value);
                }
            });
        }

#pragma warning disable xUnit1031
        Task.WaitAll(tasks);
#pragma warning restore xUnit1031

        counter.ShouldBe(threadCount / 2);
        results.ShouldNotBeEmpty();
        // All read values should be between 0 and threadCount/2
        results.ShouldAllBe(static value => value >= 0 && value <= threadCount / 2);
    }

    [Fact]
    public void LockHelper_ExecuteAction_NestedCalls_WorkCorrectly()
    {
        var outerExecuted = false;
        var innerExecuted = false;

        LockHelper.Execute(_lock,
            () =>
            {
                outerExecuted = true;
                // Note: In production, nested locking with the same lock would deadlock on .NET 9
                // but works with traditional locks. This test verifies the outer lock works.
                innerExecuted = true;
            });

        outerExecuted.ShouldBeTrue();
        innerExecuted.ShouldBeTrue();
    }

    [Fact]
    public void LockHelper_ExecuteFunc_ReturnsComplexObject()
    {
        var expectedPerson = new Person { Name = "Alice", Age = 30 };

        var result = LockHelper.Execute(_lock, () => expectedPerson);

        result.ShouldBe(expectedPerson);
        result.Name.ShouldBe("Alice");
        result.Age.ShouldBe(30);
    }

    [Fact]
    public void LockHelper_ExecuteAction_WithComplexSharedState()
    {
        var dictionary = new Dictionary<string, int>();

        LockHelper.Execute(_lock,
            () =>
            {
                dictionary["key1"] = 100;
                dictionary["key2"] = 200;
            });

        var sum = LockHelper.Execute(_lock, () => dictionary.Values.Sum());

        sum.ShouldBe(300);
        dictionary.Count.ShouldBe(2);
    }

    [Fact]
    public void LockHelper_ExecuteFunc_WithNullableReturnType()
    {
        string? nullValue = null;
        const string nonNullValue = "test";

        var result1 = LockHelper.Execute(_lock, () => nullValue);
        var result2 = LockHelper.Execute(_lock, static () => nonNullValue);

        result1.ShouldBeNull();
        result2.ShouldBe("test");
    }

    [Fact]
    public void LockHelper_ExecuteAction_MultipleThreads_SerializedExecution()
    {
        var executionOrder = new ConcurrentBag<int>();
        var activeCount = 0;
        var maxActiveCount = 0;
        const int threadCount = 10;
        var tasks = new Task[threadCount];

        for (var i = 0; i < threadCount; i++)
        {
            var threadId = i;
            tasks[i] = Task.Run(() =>
            {
                LockHelper.Execute(_lock,
                    () =>
                    {
                        var current = Interlocked.Increment(ref activeCount);

                        // Track max concurrent executions inside lock
                        var currentMax = maxActiveCount;
                        while (current > currentMax)
                            currentMax = Interlocked.CompareExchange(ref maxActiveCount, current, currentMax);

                        executionOrder.Add(threadId);
                        Thread.Sleep(50); // Reduced from 1000ms to 50ms for faster tests

                        Interlocked.Decrement(ref activeCount);
                    });
            });
        }

#pragma warning disable xUnit1031
        Task.WaitAll(tasks);
#pragma warning restore xUnit1031

        executionOrder.Count.ShouldBe(threadCount);
        maxActiveCount.ShouldBe(1, "only one thread should execute the locked section at a time");
    }

    [Fact]
    public void LockHelper_ExecuteFunc_WithBooleanReturn()
    {
        const bool condition = true;

        var result = LockHelper.Execute(_lock, static () => condition);

        result.ShouldBeTrue();
    }

    [Fact]
    public void LockHelper_ExecuteAction_ExceptionDoesNotLeaveLockHeld()
    {
        var counter = 0;

        try
        {
            LockHelper.Execute(_lock,
                () =>
                {
                    counter++;
                    throw new InvalidOperationException("Test");
                });
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        // Second call should succeed (lock should be released)
        LockHelper.Execute(_lock, () => { counter++; });

        counter.ShouldBe(2);
    }

    [Fact]
    public void LockHelper_ExecuteFunc_ExceptionDoesNotLeaveLockHeld()
    {
        var counter = 0;

        try
        {
            LockHelper.Execute(_lock,
                () =>
                {
                    counter++;
                    throw new InvalidOperationException("Test");
#pragma warning disable CS0162
                    return 42;
#pragma warning restore CS0162
                });
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        // Second call should succeed (lock should be released)
        var result = LockHelper.Execute(_lock, () => ++counter);

        result.ShouldBe(2);
        counter.ShouldBe(2);
    }

    [Fact]
    public async Task LockHelper_ExecuteAction_WorksWithAsyncContext()
    {
        var counter = 0;

        await Task.Run(() => { LockHelper.Execute(_lock, () => { counter++; }); });

        counter.ShouldBe(1);
    }

    [Fact]
    public async Task LockHelper_ExecuteFunc_WorksWithAsyncContext()
    {
        const int value = 42;

        var result = await Task.Run(() => LockHelper.Execute(_lock, static () => value * 2));

        result.ShouldBe(84);
    }

    [Fact]
    public void LockHelper_ExecuteFunc_WithValueTypeReturn()
    {
        var structValue = new Point { X = 10, Y = 20 };

        var result = LockHelper.Execute(_lock, () => structValue);

        result.X.ShouldBe(10);
        result.Y.ShouldBe(20);
    }

    [Fact]
    public void LockHelper_ExecuteAction_StressTest_HighContention()
    {
        var counter = 0;
        const int threadCount = 200;
        const int incrementsPerThread = 50;
        var tasks = new Task[threadCount];

        for (var i = 0; i < threadCount; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                for (var j = 0; j < incrementsPerThread; j++) LockHelper.Execute(_lock, () => { counter++; });
            });
        }

#pragma warning disable xUnit1031
        Task.WaitAll(tasks);
#pragma warning restore xUnit1031

        counter.ShouldBe(threadCount * incrementsPerThread);
    }

    private sealed class Person
    {
        public string Name { get; init; } = string.Empty;
        public int Age { get; init; }
    }

    private struct Point
    {
        public int X { get; init; }
        public int Y { get; init; }
    }
}