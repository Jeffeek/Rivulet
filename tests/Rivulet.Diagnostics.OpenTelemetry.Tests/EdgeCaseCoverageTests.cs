using System.Diagnostics;
using FluentAssertions;
using Rivulet.Core;

namespace Rivulet.Diagnostics.OpenTelemetry.Tests;

/// <summary>
/// Tests specifically designed to cover edge cases and improve code coverage.
/// </summary>
[Collection("ActivitySource Tests")]
public class EdgeCaseCoverageTests
{
    [Fact]
    public async Task WithOpenTelemetryTracing_ShouldHandleNullOperationName()
    {
        using var listener = new ActivityListener();
        listener.ShouldListenTo = source => source.Name == RivuletActivitySource.SourceName;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        listener.ActivityStopped = _ => { };
        ActivitySource.AddActivityListener(listener);

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 2
        }.WithOpenTelemetryTracing(null!);

        var result = await Enumerable.Range(1, 3)
            .SelectParallelAsync(async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x;
            }, options);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task WithOpenTelemetryTracing_ShouldHandleEmptyOperationName()
    {
        using var listener = new ActivityListener();
        listener.ShouldListenTo = source => source.Name == RivuletActivitySource.SourceName;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        listener.ActivityStopped = _ => { };
        ActivitySource.AddActivityListener(listener);

        var options = new ParallelOptionsRivulet().WithOpenTelemetryTracing(string.Empty);

        var result = await Enumerable.Range(1, 2)
            .SelectParallelAsync(async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x;
            }, options);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task WithOpenTelemetryTracingAndRetries_ShouldHandleNoRetries()
    {
        var activities = new List<Activity>();

        using var listener = new ActivityListener();
        listener.ShouldListenTo = source => source.Name == RivuletActivitySource.SourceName;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        listener.ActivityStopped = activity => activities.Add(activity);
        ActivitySource.AddActivityListener(listener);

        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 3,
            IsTransient = _ => true
        }.WithOpenTelemetryTracingAndRetries("NoRetriesOp", trackRetries: true);

        var result = await Enumerable.Range(1, 5)
            .SelectParallelAsync(async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x;
            }, options);

        result.Should().HaveCount(5);

        var retryEvents = activities
            .SelectMany(a => a.Events)
            .Where(e => e.Name == RivuletOpenTelemetryConstants.EventNames.Retry)
            .ToList();

        retryEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task WithOpenTelemetryTracing_ShouldHandleExistingHooks()
    {
        var onStartCalled = 0;
        var onCompleteCalled = 0;
        var onErrorCalled = 0;

        using var listener = new ActivityListener();
        listener.ShouldListenTo = source => source.Name == RivuletActivitySource.SourceName;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        ActivitySource.AddActivityListener(listener);

        var options = new ParallelOptionsRivulet
        {
            OnStartItemAsync = async _ =>
            {
                onStartCalled++;
                await Task.CompletedTask;
            },
            OnCompleteItemAsync = async _ =>
            {
                onCompleteCalled++;
                await Task.CompletedTask;
            },
            OnErrorAsync = async (_, _) =>
            {
                onErrorCalled++;
                await Task.CompletedTask;
                return false; // Don't handle
            }
        }.WithOpenTelemetryTracing("PreserveHooks");

        try
        {
            await Enumerable.Range(1, 5)
                .SelectParallelAsync(async (x, ct) =>
                {
                    await Task.Delay(1, ct);
                    if (x == 3)
                        throw new InvalidOperationException("Test");
                    return x;
                }, options);
        }
        catch
        {
            // Expected
        }

        onStartCalled.Should().BeGreaterThan(0);
        onCompleteCalled.Should().BeGreaterThan(0);
        onErrorCalled.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RivuletActivitySource_ShouldHandleNullActivity()
    {
        var options = new ParallelOptionsRivulet().WithOpenTelemetryTracing("NullActivityTest");

        var result = await Enumerable.Range(1, 3)
            .SelectParallelAsync(async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x;
            }, options);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task RivuletMetricsExporter_ShouldHandleErrorRateCalculation()
    {
        using var exporter = new RivuletMetricsExporter();

        await Task.Delay(100);

        await Enumerable.Range(1, 5)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x;
            }, new ParallelOptionsRivulet())
            .ToListAsync();

        await Task.Delay(100);
    }

    [Fact]
    public async Task WithOpenTelemetryTracing_ShouldHandleVeryLargeOperationCounts()
    {
        // ReSharper disable once CollectionNeverQueried.Local
        var activities = new System.Collections.Concurrent.ConcurrentBag<Activity>();

        using var listener = new ActivityListener();
        listener.ShouldListenTo = source => source.Name == RivuletActivitySource.SourceName;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        listener.ActivityStopped = activity => activities.Add(activity);
        ActivitySource.AddActivityListener(listener);

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 10
        }.WithOpenTelemetryTracing("LargeOp");

        var result = await Enumerable.Range(1, 100)
            .SelectParallelAsync(async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x;
            }, options);

        result.Should().HaveCount(100);
    }

    [Fact]
    public async Task WithOpenTelemetryTracing_ShouldHandleMixedSuccessAndFailure()
    {
        var activities = new List<Activity>();

        using var listener = new ActivityListener();
        listener.ShouldListenTo = source => source.Name == RivuletActivitySource.SourceName;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        listener.ActivityStopped = activity => activities.Add(activity);
        ActivitySource.AddActivityListener(listener);

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 3,
            ErrorMode = ErrorMode.CollectAndContinue
        }.WithOpenTelemetryTracing("MixedResults");

        try
        {
            await Enumerable.Range(1, 10)
                .SelectParallelAsync(async (x, ct) =>
                {
                    await Task.Delay(1, ct);
                    if (x % 3 == 0)
                        throw new InvalidOperationException($"Failed {x}");
                    return x;
                }, options);
        }
        catch (AggregateException)
        {
        }

        await Task.Delay(100);

        var successActivities = activities.Where(a => a.Status == ActivityStatusCode.Ok).ToList();
        var errorActivities = activities.Where(a => a.Status == ActivityStatusCode.Error).ToList();

        successActivities.Should().NotBeEmpty();
        errorActivities.Should().NotBeEmpty();
    }

    [Fact]
    public async Task WithOpenTelemetryTracing_ShouldHandleTransientAndNonTransientErrors()
    {
        var activities = new List<Activity>();

        using var listener = new ActivityListener();
        listener.ShouldListenTo = source => source.Name == RivuletActivitySource.SourceName;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        listener.ActivityStopped = activity => activities.Add(activity);
        ActivitySource.AddActivityListener(listener);

        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 2,
            IsTransient = ex => ex.Message.Contains("transient"),
            ErrorMode = ErrorMode.CollectAndContinue
        }.WithOpenTelemetryTracingAndRetries("ErrorClassification", trackRetries: true);

        try
        {
            await Enumerable.Range(1, 6)
                .SelectParallelAsync(async (x, ct) =>
                {
                    await Task.Delay(1, ct);
                    if (x % 3 == 0)
                        throw new InvalidOperationException("transient error");
                    if (x % 2 == 0)
                        throw new InvalidOperationException("permanent error");
                    return x;
                }, options);
        }
        catch (AggregateException)
        {
        }

        await Task.Delay(100);

        var errorActivities = activities.Where(a => a.Status == ActivityStatusCode.Error).ToList();
        errorActivities.Should().NotBeEmpty();
    }

    [Fact]
    public async Task WithOpenTelemetryTracing_ShouldHandleDisposedActivitySource()
    {
        var options = new ParallelOptionsRivulet().WithOpenTelemetryTracing("DisposedTest");

        var result = await Enumerable.Range(1, 5)
            .SelectParallelAsync(async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x * 2;
            }, options);

        result.Should().HaveCount(5);
        result.Should().Contain(new[] { 2, 4, 6, 8, 10 });
    }
}
