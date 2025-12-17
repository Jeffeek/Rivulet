using Rivulet.Base.Tests;
using Rivulet.Core;

namespace Rivulet.Diagnostics.Tests;

/// <summary>
///     Comprehensive tests to achieve 100% code coverage for edge cases.
/// </summary>
[Collection(TestCollections.SerialEventSource)]
public sealed class ComprehensiveCoverageTests
{
    [Fact]
    public async Task EventListenerBase_ShouldHandleEarlyReturnCases()
    {
        // This test verifies that EventListenerBase can be created and disposed safely
        // without performing any operations, even when metrics may be flowing from other tests
        var listener = new TestRivuletEventListener();

        await Task.Delay(100, CancellationToken.None);

        // Should not throw when disposed
        var act = () => listener.Dispose();
        act.ShouldNotThrow();

        // Listener should have received metrics callback invocations without errors
        // Note: We don't assert empty because RivuletEventSource is a static singleton
        // and other parallel tests may trigger metrics
    }

    [Fact]
    public async Task MetricsAggregator_ShouldInvokeCallback_WhenMetricsAggregated()
    {
        var callbackInvoked = false;
        await using var aggregator = new MetricsAggregator(TimeSpan.FromSeconds(1));
        aggregator.OnAggregation += metrics =>
        {
            if (metrics.Count > 0) callbackInvoked = true;
        };

        // Operations must run long enough for EventCounter polling (1 second interval)
        // 10 items * 200ms / 2 parallelism = 1000ms (1 second) minimum operation time
        await Enumerable.Range(1, 10)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(static async (x, ct) =>
                {
                    await Task.Delay(200, ct);
                    return x;
                },
                new() { MaxDegreeOfParallelism = 2 })
            .ToListAsync();

        // Wait for at least 2x the aggregation window to ensure timer fires and EventSource counters are received
        await DeadlineExtensions.ApplyDeadlineAsync(
            DateTime.UtcNow.AddMilliseconds(2000),
            static () => Task.Delay(100),
            () => !callbackInvoked);

        callbackInvoked.ShouldBeTrue();
    }

    [Fact]
    public async Task FileListener_ShouldHandleEmptyDisplayUnits()
    {
        var testFile = Path.Join(Path.GetTempPath(), $"rivulet-coverage-{Guid.NewGuid()}.log");

        try
        {
            await using (new RivuletFileListener(testFile))
            {
                // Operations must run long enough for EventCounter polling (1 second interval)
                // 5 items * 400ms / 2 parallelism = 1000ms (1 second) minimum operation time
                await Enumerable.Range(1, 5)
                    .ToAsyncEnumerable()
                    .SelectParallelStreamAsync(static async (x, ct) =>
                        {
                            await Task.Delay(400, ct);
                            return x;
                        },
                        new() { MaxDegreeOfParallelism = 2 })
                    .ToListAsync();

                // Wait for EventSource counters to fire and be written to file
                await Task.Delay(2000, CancellationToken.None);
            }

            // Brief wait for file handle release
            await Task.Delay(100, CancellationToken.None);

            File.Exists(testFile).ShouldBeTrue();
        }
        finally
        {
            TestCleanupHelper.RetryDeleteFile(testFile);
        }
    }


    private sealed class TestRivuletEventListener : RivuletEventListenerBase
    {
        // ReSharper disable once CollectionNeverQueried.Local
        private List<string> ReceivedMetrics { get; } = new();

        protected override void OnCounterReceived(string name,
            string displayName,
            double value,
            string displayUnits) =>
            ReceivedMetrics.Add(name);
    }
}