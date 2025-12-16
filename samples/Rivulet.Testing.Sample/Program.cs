using Rivulet.Core;
using Rivulet.Testing;

// ReSharper disable AccessToDisposedClosure
// ReSharper disable ArrangeObjectCreationWhenTypeNotEvident
// ReSharper disable ArgumentsStyleLiteral
// ReSharper disable ArgumentsStyleOther

Console.WriteLine("=== Rivulet.Testing Sample ===\n");

// Sample 1: VirtualTimeProvider - Deterministic time control
Console.WriteLine("1. VirtualTimeProvider - Virtual time control");
using (var virtualTime = new VirtualTimeProvider())
{
    Console.WriteLine($"Virtual time started at: {virtualTime.CurrentTime}");

    // Advance time manually
    virtualTime.AdvanceTime(TimeSpan.FromMinutes(5));
    Console.WriteLine($"After advancing 5 minutes: {virtualTime.CurrentTime}");

    // Create a virtual delay
    var delayTask = virtualTime.CreateDelay(TimeSpan.FromSeconds(10));
    Console.WriteLine($"Created virtual delay for 10 seconds. Completed: {delayTask.IsCompleted}");

    // Advance time to complete the delay
    virtualTime.AdvanceTime(TimeSpan.FromSeconds(5));
    Console.WriteLine($"After 5 seconds - Delay completed: {delayTask.IsCompleted}");

    virtualTime.AdvanceTime(TimeSpan.FromSeconds(6));
    Console.WriteLine($"After 11 seconds total - Delay completed: {delayTask.IsCompleted}");
    Console.WriteLine("✓ Virtual time control demonstrated\n");
}

// Sample 2: VirtualTimeProvider with delays in parallel operations
Console.WriteLine("2. VirtualTimeProvider - Instant delay simulation");
using (var testTime = new VirtualTimeProvider())
{
    var delayedTask = Task.Run(async () =>
    {
        var startTime = testTime.CurrentTime;
        await testTime.CreateDelay(TimeSpan.FromMinutes(30));
        var endTime = testTime.CurrentTime;
        return (endTime - startTime).TotalMinutes;
    });

    // Advance time to complete the delay instantly
    testTime.AdvanceTime(TimeSpan.FromMinutes(30));
    var duration = await delayedTask;
    Console.WriteLine($"Delay completed in virtual time - Duration: {duration} minutes");
    Console.WriteLine("✓ Instant delay simulation demonstrated\n");
}

// Sample 3: ChaosInjector - Simulate failures
Console.WriteLine("3. ChaosInjector - Fault injection testing");
var chaosInjector = new ChaosInjector(failureRate: 0.3); // 30% failure rate

var items = Enumerable.Range(1, 20);
var successCount = 0;
var failureCount = 0;

var results = await items.SelectParallelAsync(
    async (item, ct) =>
    {
        // Use ChaosInjector to potentially fail
        if (chaosInjector.ShouldFail())
        {
            Interlocked.Increment(ref failureCount);
            throw new ChaosException("Chaos-injected failure");
        }

        await Task.Delay(10, ct);
        Interlocked.Increment(ref successCount);
        return item;
    },
    new ParallelOptionsRivulet
    {
        MaxDegreeOfParallelism = 5,
        ErrorMode = ErrorMode.CollectAndContinue,
        MaxRetries = 0
    });

Console.WriteLine($"Items processed: {results.Count}, Failed: {failureCount}");
Console.WriteLine($"Actual failure rate: {(double)failureCount / 20:P1}");
Console.WriteLine("✓ Chaos injection demonstrated\n");

// Sample 4: ChaosInjector with ExecuteAsync
Console.WriteLine("4. ChaosInjector - ExecuteAsync with delays");
var latencyChaos = new ChaosInjector(failureRate: 0.2, artificialDelay: TimeSpan.FromMilliseconds(50));

var startStopwatch = System.Diagnostics.Stopwatch.StartNew();
var data = Enumerable.Range(1, 10);
var executedCount = 0;

await data.ToAsyncEnumerable()
    .ForEachParallelAsync(
        async (item, ct) =>
        {
            try
            {
                await latencyChaos.ExecuteAsync(async () =>
                    {
                        await Task.Delay(10, ct);
                        Interlocked.Increment(ref executedCount);
                        return item;
                    },
                    ct);
            }
            catch (ChaosException)
            {
                // Expected chaos exception
            }
        },
        new ParallelOptionsRivulet { MaxDegreeOfParallelism = 3 });

startStopwatch.Stop();
Console.WriteLine($"Executed {executedCount}/10 operations in {startStopwatch.ElapsedMilliseconds}ms");
Console.WriteLine("✓ ChaosInjector.ExecuteAsync demonstrated\n");

// Sample 5: ConcurrencyAsserter - Verify concurrency limits
Console.WriteLine("5. ConcurrencyAsserter - Concurrency verification");
var asserter = new ConcurrencyAsserter();

var workItems = Enumerable.Range(1, 30);

await workItems.SelectParallelAsync(
    async (item, ct) =>
    {
        using (asserter.Enter())
        {
            // This code runs with concurrency tracking
            await Task.Delay(Random.Shared.Next(20, 50), ct);
            return item;
        }
    },
    new ParallelOptionsRivulet { MaxDegreeOfParallelism = 5 });

Console.WriteLine($"Max concurrent operations observed: {asserter.MaxConcurrency}");
Console.WriteLine($"Current concurrent operations: {asserter.CurrentConcurrency}");
Console.WriteLine($"✓ Concurrency stayed within limit (Max was {asserter.MaxConcurrency}, expected ≤ 5)\n");

// Sample 6: ConcurrencyAsserter - Check max concurrency
Console.WriteLine("6. ConcurrencyAsserter - Concurrency tracking");
var asserter2 = new ConcurrencyAsserter();

// Launch many tasks simultaneously
var tasks = Enumerable.Range(1, 20)
    .Select(async i =>
    {
        using (asserter2.Enter())
        {
            await Task.Delay(50);
            return i;
        }
    });

await Task.WhenAll(tasks);

Console.WriteLine($"Max concurrent operations: {asserter2.MaxConcurrency}");
Console.WriteLine("✓ Tracked maximum concurrency successfully\n");

// Sample 7: FakeChannel - Controlled channel testing
Console.WriteLine("7. FakeChannel - Deterministic channel behavior");
using (var fakeChannel = new FakeChannel<int>())
{
    // Write items to the channel
    _ = Task.Run(async () =>
    {
        await fakeChannel.WriteAsync(1);
        await fakeChannel.WriteAsync(2);
        await fakeChannel.WriteAsync(3);
        fakeChannel.Complete();
    });

    // Read items from the channel
    Console.WriteLine("Reading items from FakeChannel:");
    await foreach (var item in fakeChannel.Reader.ReadAllAsync()) Console.WriteLine($"  Read: {item}");

    Console.WriteLine($"Total writes: {fakeChannel.WriteCount}");
    Console.WriteLine($"Total reads: {fakeChannel.ReadCount}");
    Console.WriteLine("✓ Fake channel demonstrated\n");
}

// Sample 8: FakeChannel - Tracking read/write operations
Console.WriteLine("8. FakeChannel - Operation tracking");
using (var trackedChannel = new FakeChannel<string>(boundedCapacity: 10))
{
    // Producer
    var producerTask = Task.Run(async () =>
    {
        for (var i = 0; i < 5; i++) await trackedChannel.WriteAsync($"Message-{i}");

        trackedChannel.Complete();
    });

    // Consumer
    var consumerTask = Task.Run(async () =>
    {
        var messages = new List<string>();
        await foreach (var msg in trackedChannel.Reader.ReadAllAsync()) messages.Add(msg);

        return messages;
    });

    await producerTask;
    var receivedMessages = await consumerTask;

    Console.WriteLine($"Produced: {trackedChannel.WriteCount} messages");
    Console.WriteLine($"Consumed: {trackedChannel.ReadCount} messages");
    Console.WriteLine($"Messages: {string.Join(", ", receivedMessages)}");

    // Dispose will be called automatically at the end of this block
    Console.WriteLine("✓ Operation tracking demonstrated\n");
}

Console.WriteLine("=== All testing samples completed successfully ===");
Console.WriteLine("\nTesting utilities summary:");
Console.WriteLine("  - VirtualTimeProvider: Control time for deterministic tests");
Console.WriteLine("  - ChaosInjector: Inject failures and latency");
Console.WriteLine("  - ConcurrencyAsserter: Verify concurrency limits");
Console.WriteLine("  - FakeChannel: Track read/write operations on channels");
