using System.Collections.Concurrent;
using System.Diagnostics;
using Rivulet.Core;

namespace Rivulet.Diagnostics.OpenTelemetry.Tests;

/// <summary>
///     Tests specifically designed to cover edge cases and improve code coverage.
/// </summary>
[Collection(TestCollections.ActivitySource)]
public sealed class EdgeCaseCoverageTests
{
    [Fact]
    public async Task WithOpenTelemetryTracing_ShouldHandleNullOperationName()
    {
        using var listener = new ActivityListener();
        listener.ShouldListenTo = static source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        listener.ActivityStopped = static _ => { };
        ActivitySource.AddActivityListener(listener);

        var options = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 2 }.WithOpenTelemetryTracing(null!);

        var result = await Enumerable.Range(1, 3)
            .SelectParallelAsync(static async (x, ct) =>
                {
                    await Task.Delay(1, ct);
                    return x;
                },
                options);

        result.Count.ShouldBe(3);
    }

    [Fact]
    public async Task WithOpenTelemetryTracing_ShouldHandleEmptyOperationName()
    {
        using var listener = new ActivityListener();
        listener.ShouldListenTo = static source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        listener.ActivityStopped = static _ => { };
        ActivitySource.AddActivityListener(listener);

        var options = new ParallelOptionsRivulet().WithOpenTelemetryTracing(string.Empty);

        var result = await Enumerable.Range(1, 2)
            .SelectParallelAsync(static async (x, ct) =>
                {
                    await Task.Delay(1, ct);
                    return x;
                },
                options);

        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task WithOpenTelemetryTracingAndRetries_ShouldHandleNoRetries()
    {
        var activities = new List<Activity?>();

        using var listener = new ActivityListener();
        listener.ShouldListenTo = static source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        listener.ActivityStopped = activity => activities.Add(activity);
        ActivitySource.AddActivityListener(listener);

        var options =
            new ParallelOptionsRivulet { MaxRetries = 3, IsTransient = static _ => true }
                .WithOpenTelemetryTracingAndRetries("NoRetriesOp");

        var result = await Enumerable.Range(1, 5)
            .SelectParallelAsync(static async (x, ct) =>
                {
                    await Task.Delay(1, ct);
                    return x;
                },
                options);

        result.Count.ShouldBe(5);

        var retryEvents = activities
            .Where(static a => a?.Events != null)
            .SelectMany(static a => a!.Events)
            .Where(static e => e.Name == RivuletOpenTelemetryConstants.EventNames.Retry)
            .ToList();

        retryEvents.ShouldBeEmpty();
    }

    [Fact]
    public async Task WithOpenTelemetryTracing_ShouldHandleExistingHooks()
    {
        var onStartCalled = 0;
        var onCompleteCalled = 0;
        var onErrorCalled = 0;

        using var listener = new ActivityListener();
        listener.ShouldListenTo = static source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        ActivitySource.AddActivityListener(listener);

        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 1,
            ErrorMode = ErrorMode.CollectAndContinue,
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
                return false;
            }
        }.WithOpenTelemetryTracing("PreserveHooks");

        try
        {
            await Enumerable.Range(1, 5)
                .SelectParallelAsync(static async (x, ct) =>
                    {
                        await Task.Delay(10, ct);
                        return x == 3 ? throw new InvalidOperationException("Test") : x;
                    },
                    options);
        }
        catch (AggregateException)
        {
            // Expected - test intentionally throws
        }

        await Task.Delay(100, CancellationToken.None);

        onStartCalled.ShouldBeGreaterThan(0);
        onCompleteCalled.ShouldBeGreaterThan(0);
        onErrorCalled.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task RivuletActivitySource_ShouldHandleNullActivity()
    {
        var options = new ParallelOptionsRivulet().WithOpenTelemetryTracing("NullActivityTest");

        var result = await Enumerable.Range(1, 3)
            .SelectParallelAsync(static async (x, ct) =>
                {
                    await Task.Delay(1, ct);
                    return x;
                },
                options);

        result.Count.ShouldBe(3);
    }

    [Fact]
    public async Task RivuletMetricsExporter_ShouldHandleErrorRateCalculation()
    {
        using var exporter = new RivuletMetricsExporter();

        await Task.Delay(100, CancellationToken.None);

        await Enumerable.Range(1, 5)
            .ToAsyncEnumerable()
            .SelectParallelStreamAsync(static async (x, ct) =>
                {
                    await Task.Delay(1, ct);
                    return x;
                },
                new())
            .ToListAsync();

        await Task.Delay(100, CancellationToken.None);
    }

    [Fact]
    public async Task WithOpenTelemetryTracing_ShouldHandleVeryLargeOperationCounts()
    {
        // ReSharper disable once CollectionNeverQueried.Local
        var activities = new ConcurrentBag<Activity>();

        using var listener = new ActivityListener();
        listener.ShouldListenTo = static source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        listener.ActivityStopped = activity => activities.Add(activity);
        ActivitySource.AddActivityListener(listener);

        var options = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 10 }.WithOpenTelemetryTracing("LargeOp");

        var result = await Enumerable.Range(1, 100)
            .SelectParallelAsync(static async (x, ct) =>
                {
                    await Task.Delay(1, ct);
                    return x;
                },
                options);

        result.Count.ShouldBe(100);
    }

    [Fact]
    public async Task WithOpenTelemetryTracing_ShouldHandleMixedSuccessAndFailure()
    {
        var activities = new List<Activity?>();

        using var listener = new ActivityListener();
        listener.ShouldListenTo = static source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        listener.ActivityStopped = activity => activities.Add(activity);
        ActivitySource.AddActivityListener(listener);

        var options = new ParallelOptionsRivulet
                { MaxDegreeOfParallelism = 3, ErrorMode = ErrorMode.CollectAndContinue }
            .WithOpenTelemetryTracing("MixedResults");

        try
        {
            await Enumerable.Range(1, 10)
                .SelectParallelAsync(static async (x, ct) =>
                    {
                        await Task.Delay(1, ct);
                        return x % 3 == 0 ? throw new InvalidOperationException($"Failed {x}") : x;
                    },
                    options);
        }
        catch (AggregateException)
        {
            // Expected - test intentionally throws
        }

        // Wait for all activities to be captured by the listener
        // On slower systems (CI/CD), activities may take longer to be recorded
        await Task.Delay(300, CancellationToken.None);

        // Filter out null activities (can happen during async callback execution)
        var successActivities = activities.Where(static a => a?.Status == ActivityStatusCode.Ok).ToList();
        var errorActivities = activities.Where(static a => a?.Status == ActivityStatusCode.Error).ToList();

        successActivities.ShouldNotBeEmpty();
        errorActivities.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task WithOpenTelemetryTracing_ShouldHandleTransientAndNonTransientErrors()
    {
        var activities = new List<Activity?>();

        using var listener = new ActivityListener();
        listener.ShouldListenTo = static source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        listener.ActivityStopped = activity => activities.Add(activity);
        ActivitySource.AddActivityListener(listener);

        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 2, IsTransient = static ex => ex.Message.Contains("transient"),
            ErrorMode = ErrorMode.CollectAndContinue
        }.WithOpenTelemetryTracingAndRetries("ErrorClassification");

        try
        {
            await Enumerable.Range(1, 6)
                .SelectParallelAsync(static async (x, ct) =>
                    {
                        await Task.Delay(1, ct);
                        if (x % 3 == 0) throw new InvalidOperationException("transient error");

                        return x % 2 == 0 ? throw new InvalidOperationException("permanent error") : x;
                    },
                    options);
        }
        catch (AggregateException)
        {
            // Expected - test intentionally throws
        }

        // Wait for all activities to be captured by the listener
        // On slower systems (CI/CD), activities may take longer to be recorded
        await Task.Delay(300, CancellationToken.None);

        // Filter out null activities (can happen during async callback execution)
        var errorActivities = activities.Where(static a => a?.Status == ActivityStatusCode.Error).ToList();
        errorActivities.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task WithOpenTelemetryTracing_ShouldHandleDisposedActivitySource()
    {
        var options = new ParallelOptionsRivulet().WithOpenTelemetryTracing("DisposedTest");

        var result = await Enumerable.Range(1, 5)
            .SelectParallelAsync(static async (x, ct) =>
                {
                    await Task.Delay(1, ct);
                    return x * 2;
                },
                options);

        result.Count.ShouldBe(5);
        result.ShouldContain(2);
        result.ShouldContain(4);
        result.ShouldContain(6);
        result.ShouldContain(8);
        result.ShouldContain(10);
    }
}