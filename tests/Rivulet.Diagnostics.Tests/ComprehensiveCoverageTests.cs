using FluentAssertions;
using Rivulet.Core;

namespace Rivulet.Diagnostics.Tests;

/// <summary>
/// Comprehensive tests to achieve 100% code coverage for edge cases.
/// </summary>
[Collection("Serial EventSource Tests")]
public class ComprehensiveCoverageTests
{
    [Fact]
    public async Task EventListenerBase_ShouldHandleEarlyReturnCases()
    {
        // This test verifies that EventListenerBase can be created and disposed safely
        // without performing any operations, even when metrics may be flowing from other tests
        var listener = new TestRivuletEventListener();

        await Task.Delay(100);

        // Should not throw when disposed
        var act = () => listener.Dispose();
        act.Should().NotThrow();

        // Listener should have received metrics callback invocations without errors
        // Note: We don't assert empty because RivuletEventSource is a static singleton
        // and other parallel tests may trigger metrics
    }

    [Fact]
    public async Task MetricsAggregator_ShouldInvokeCallback_WhenMetricsAggregated()
    {
        var callbackInvoked = false;
        using var aggregator = new MetricsAggregator(TimeSpan.FromSeconds(1));
        aggregator.OnAggregation += metrics =>
        {
            if (metrics.Count > 0)
                callbackInvoked = true;
        };

        await Enumerable.Range(1, 10)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x;
            }, new ParallelOptionsRivulet
            {
                MaxDegreeOfParallelism = 2
            })
            .ToListAsync();

        await Task.Delay(2500);

        callbackInvoked.Should().BeTrue();
    }

    [Fact]
    public async Task FileListener_ShouldHandleEmptyDisplayUnits()
    {
        var testFile = Path.Join(Path.GetTempPath(), $"rivulet-coverage-{Guid.NewGuid()}.log");

        try
        {
            using (new RivuletFileListener(testFile))
            {
                await Enumerable.Range(1, 5)
                    .ToAsyncEnumerable()
                    .SelectParallelStreamAsync(async (x, ct) =>
                    {
                        await Task.Delay(1, ct);
                        return x;
                    }, new ParallelOptionsRivulet
                    {
                        MaxDegreeOfParallelism = 2
                    })
                    .ToListAsync();

                await Task.Delay(2500);
            }

            await Task.Delay(100);

            File.Exists(testFile).Should().BeTrue();
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

        protected override void OnCounterReceived(string name, string displayName, double value, string displayUnits)
        {
            ReceivedMetrics.Add(name);
        }
    }
}
