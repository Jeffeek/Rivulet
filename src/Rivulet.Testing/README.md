# Rivulet.Testing

Testing utilities for Rivulet parallel operations including deterministic schedulers, virtual time, fake channels, and chaos injection.

## Features

- **VirtualTimeProvider**: Control time in tests without actual delays
- **FakeChannel**: Testable channel implementation with operation tracking
- **ChaosInjector**: Inject failures and delays for resilience testing
- **ConcurrencyAsserter**: Assert and verify concurrency behavior

## Installation

```bash
dotnet add package Rivulet.Testing
```

## Usage

### VirtualTimeProvider

Test time-dependent operations without waiting for real time to pass:

```csharp
using Rivulet.Testing;

[Test]
public async Task TestTimeoutBehavior()
{
    var timeProvider = new VirtualTimeProvider();

    // Schedule a delay
    var delayTask = timeProvider.DelayAsync(TimeSpan.FromSeconds(10));

    // Fast-forward time
    await timeProvider.AdvanceTimeAsync(TimeSpan.FromSeconds(5));
    Assert.False(delayTask.IsCompleted); // Only 5 seconds passed

    await timeProvider.AdvanceTimeAsync(TimeSpan.FromSeconds(5));
    Assert.True(delayTask.IsCompleted); // Now 10 seconds total

    timeProvider.Dispose();
}
```

### FakeChannel

Track channel operations and control capacity:

```csharp
using Rivulet.Testing;
using System.Threading.Channels;

[Test]
public async Task TestChannelOperations()
{
    var channel = new FakeChannel<int>(boundedCapacity: 10);

    // Write items
    await channel.WriteAsync(1);
    await channel.WriteAsync(2);
    await channel.WriteAsync(3);

    Assert.Equal(3, channel.WriteCount);
    Assert.Equal(0, channel.ReadCount);

    // Read items
    var item1 = await channel.ReadAsync();
    var item2 = await channel.ReadAsync();

    Assert.Equal(3, channel.WriteCount);
    Assert.Equal(2, channel.ReadCount);

    channel.Dispose();
}
```

### ChaosInjector

Test resilience by injecting random failures:

```csharp
using Rivulet.Testing;

[Test]
public async Task TestWithChaos()
{
    var chaos = new ChaosInjector(
        failureRate: 0.3, // 30% failure rate
        artificialDelay: TimeSpan.FromMilliseconds(100)
    );

    var retries = 0;
    var maxRetries = 5;

    while (retries < maxRetries)
    {
        try
        {
            var result = await chaos.ExecuteAsync(async () =>
            {
                // Your operation here
                return await SomeOperationAsync();
            });

            // Success!
            break;
        }
        catch (ChaosException)
        {
            retries++;
            if (retries >= maxRetries)
                throw new Exception("Max retries exceeded");
        }
    }
}
```

### ConcurrencyAsserter

Verify that concurrency limits are respected:

```csharp
using Rivulet.Testing;
using Rivulet.Core;

[Test]
public async Task TestMaxDegreeOfParallelism()
{
    var asserter = new ConcurrencyAsserter();
    var maxDegree = 5;

    var items = Enumerable.Range(1, 100);

    await items.ParallelForEachAsync(
        async item =>
        {
            using var scope = await asserter.EnterAsync();

            // Simulate work
            await Task.Delay(10);
        },
        new ParallelOptions
        {
            MaxDegreeOfParallelism = maxDegree
        }
    );

    Assert.True(asserter.MaxConcurrency <= maxDegree);
    Assert.Equal(0, asserter.CurrentConcurrency); // All completed
}
```

## Advanced Examples

### Combining VirtualTimeProvider with Parallel Operations

```csharp
[Test]
public async Task TestRetryWithVirtualTime()
{
    var timeProvider = new VirtualTimeProvider();
    var attempts = 0;

    var retryTask = Task.Run(async () =>
    {
        while (attempts < 3)
        {
            attempts++;
            await timeProvider.DelayAsync(TimeSpan.FromSeconds(1));
        }
    });

    // Fast-forward through all retries
    await timeProvider.AdvanceTimeAsync(TimeSpan.FromSeconds(3));
    await retryTask;

    Assert.Equal(3, attempts);
}
```

### Testing Channel Backpressure

```csharp
[Test]
public async Task TestBoundedChannelBackpressure()
{
    var channel = new FakeChannel<int>(boundedCapacity: 2);

    // Fill the channel
    await channel.WriteAsync(1);
    await channel.WriteAsync(2);

    // This will block until space is available
    var writeTask = Task.Run(() => channel.WriteAsync(3));

    await Task.Delay(100);
    Assert.False(writeTask.IsCompleted); // Still blocked

    // Read one item to make space
    await channel.ReadAsync();

    await writeTask; // Now completes
    Assert.Equal(3, channel.WriteCount);
}
```

### Chaos Testing with Retry Logic

```csharp
[Test]
public async Task TestRetryLogicUnderChaos()
{
    var chaos = new ChaosInjector(failureRate: 0.5);
    var successCount = 0;
    var failureCount = 0;

    async Task<int> OperationWithRetry(int value)
    {
        var maxRetries = 10;
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                return await chaos.ExecuteAsync(async () =>
                {
                    await Task.Delay(1);
                    return value * 2;
                });
            }
            catch (ChaosException)
            {
                failureCount++;
                if (i == maxRetries - 1)
                    throw;
            }
        }
        return -1;
    }

    var items = Enumerable.Range(1, 10);
    var results = await items.ParallelSelectAsync(
        async item => await OperationWithRetry(item),
        new ParallelOptions { MaxDegreeOfParallelism = 3 }
    );

    Assert.Equal(10, results.Count());
    Assert.True(failureCount > 0); // Some failures occurred
}
```

### Verifying Concurrency Patterns

```csharp
[Test]
public async Task TestThrottling()
{
    var asserter = new ConcurrencyAsserter();
    var throttle = new SemaphoreSlim(3, 3); // Max 3 concurrent

    var tasks = Enumerable.Range(1, 20).Select(async i =>
    {
        await throttle.WaitAsync();
        try
        {
            using var scope = await asserter.EnterAsync();
            await Task.Delay(50);
        }
        finally
        {
            throttle.Release();
        }
    });

    await Task.WhenAll(tasks);

    Assert.True(asserter.MaxConcurrency <= 3);
}
```

## Integration with Rivulet.Core

These testing utilities work seamlessly with Rivulet.Core parallel operations:

```csharp
using Rivulet.Core;
using Rivulet.Testing;

[Test]
public async Task TestParallelOperationsWithTestingTools()
{
    var channel = new FakeChannel<int>();
    var asserter = new ConcurrencyAsserter();
    var chaos = new ChaosInjector(failureRate: 0.2);

    // Producer
    var producerTask = Task.Run(async () =>
    {
        for (int i = 0; i < 100; i++)
        {
            await channel.WriteAsync(i);
        }
        channel.Complete();
    });

    // Consumer with chaos and concurrency tracking
    var results = new List<int>();
    await channel.Reader.ToAsyncEnumerable()
        .ParallelForEachAsync(
            async item =>
            {
                using var scope = await asserter.EnterAsync();

                var result = await chaos.ExecuteAsync(async () =>
                {
                    await Task.Delay(1);
                    return item * 2;
                });

                lock (results)
                {
                    results.Add(result);
                }
            },
            new ParallelOptions { MaxDegreeOfParallelism = 10 }
        );

    await producerTask;

    Assert.Equal(100, channel.WriteCount);
    Assert.Equal(100, channel.ReadCount);
    Assert.True(asserter.MaxConcurrency <= 10);
}
```

## Best Practices

1. **VirtualTimeProvider**: Always dispose of the provider to clean up resources
2. **FakeChannel**: Use bounded capacity to test backpressure scenarios
3. **ChaosInjector**: Start with low failure rates (0.1-0.3) and increase gradually
4. **ConcurrencyAsserter**: Reset between test cases to avoid contamination

## License

MIT License - see LICENSE file for details.

## Contributing

Contributions are welcome! Please see the main Rivulet repository for contribution guidelines.
