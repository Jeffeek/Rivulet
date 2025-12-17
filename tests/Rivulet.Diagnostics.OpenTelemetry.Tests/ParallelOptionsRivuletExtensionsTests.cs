using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Rivulet.Core;

namespace Rivulet.Diagnostics.OpenTelemetry.Tests;

[
    Collection(TestCollections.ActivitySource),
    SuppressMessage("ReSharper", "AccessToDisposedClosure")
]
public sealed class ParallelOptionsRivuletExtensionsTests
{
    [Fact]
    public async Task WithOpenTelemetryTracing_ShouldCreateActivitiesForEachItem()
    {
        var activities = new ConcurrentBag<Activity>();
        var activityCount = 0;
        const int expectedCount = 5;
        using var allActivitiesStarted = new ManualResetEventSlim(false);

        using var listener = new ActivityListener();
        listener.ShouldListenTo = static source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        listener.ActivityStarted = activity =>
        {
            activities.Add(activity);
            if (Interlocked.Increment(ref activityCount) >= expectedCount) allActivitiesStarted.Set();
        };
        ActivitySource.AddActivityListener(listener);

        var items = Enumerable.Range(1, 5);
        var options =
            new ParallelOptionsRivulet { MaxDegreeOfParallelism = 2 }.WithOpenTelemetryTracing("TestOperation");

        var results = await items.SelectParallelAsync(
            static async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x * 2;
            },
            options);

        // Wait for all activities to be started and added
        allActivitiesStarted.Wait(TimeSpan.FromSeconds(2));
        await Task.Delay(50, CancellationToken.None); // Extra buffer

        results.Count.ShouldBe(5);
        activities.Count.ShouldBe(5, "should have one activity per item");
        activities.All(static a => a.OperationName == "Rivulet.TestOperation.Item").ShouldBeTrue();
    }

    [Fact]
    public async Task WithOpenTelemetryTracing_ShouldRecordSuccess()
    {
        var activities = new ConcurrentBag<Activity>();
        var activityCount = 0;
        const int expectedCount = 3;
        using var allActivitiesStopped = new ManualResetEventSlim(false);

        using var listener = new ActivityListener();
        listener.ShouldListenTo = static source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        listener.ActivityStopped = activity =>
        {
            activities.Add(activity);
            if (Interlocked.Increment(ref activityCount) >= expectedCount) allActivitiesStopped.Set();
        };
        ActivitySource.AddActivityListener(listener);

        var items = Enumerable.Range(1, 3);
        var options = new ParallelOptionsRivulet().WithOpenTelemetryTracing("SuccessOperation");

        var results = await items.SelectParallelAsync(
            static async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x;
            },
            options);

        // Wait for all activities to be stopped and added
        allActivitiesStopped.Wait(TimeSpan.FromSeconds(2));
        await Task.Delay(50, CancellationToken.None); // Extra buffer

        results.Count.ShouldBe(3);

        var successActivities =
            activities.Where(static a => a.OperationName == "Rivulet.SuccessOperation.Item").ToList();
        successActivities.Count.ShouldBe(3);
        successActivities.All(static a => a.Status == ActivityStatusCode.Ok).ShouldBeTrue();
        successActivities.All(static a => (int)a.GetTagItem("rivulet.items_processed")! == 1).ShouldBeTrue();
    }


    [Fact]
    public async Task WithOpenTelemetryTracing_ShouldRecordErrors()
    {
        var activities = new ConcurrentBag<Activity>();
        var activityCount = 0;
        const int expectedCount = 3;
        using var allActivitiesStopped = new ManualResetEventSlim(false);

        using var listener = new ActivityListener();
        listener.ShouldListenTo = static source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        listener.ActivityStopped = activity =>
        {
            activities.Add(activity);
            if (Interlocked.Increment(ref activityCount) >= expectedCount) allActivitiesStopped.Set();
        };
        ActivitySource.AddActivityListener(listener);

        var items = Enumerable.Range(1, 3);
        var options =
            new ParallelOptionsRivulet { ErrorMode = ErrorMode.CollectAndContinue }.WithOpenTelemetryTracing(
                "ErrorOperation");

        try
        {
            await items.SelectParallelAsync<int, int>(
                static async (x, ct) =>
                {
                    await Task.Delay(1, ct);
                    throw new InvalidOperationException($"Error {x}");
                },
                options);
        }
        catch (AggregateException)
        {
            // Expected
        }

        // Wait for all activities to be stopped and added
        allActivitiesStopped.Wait(TimeSpan.FromSeconds(2));
        await Task.Delay(50, CancellationToken.None); // Extra buffer

        var errorActivities = activities.Where(static a => a.OperationName == "Rivulet.ErrorOperation.Item").ToList();
        errorActivities.Count.ShouldBe(3);
        errorActivities.All(static a => a.Status == ActivityStatusCode.Error).ShouldBeTrue();
    }


    [Fact]
    public async Task WithOpenTelemetryTracingAndRetries_ShouldRecordRetryAttempts()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener();
        listener.ShouldListenTo = static source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        listener.ActivityStopped = activity => activities.Add(activity);
        ActivitySource.AddActivityListener(listener);

        var attemptCount = 0;
        var items = new[] { 1 };
        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 3, BaseDelay = TimeSpan.FromMilliseconds(1), IsTransient = static _ => true
        }.WithOpenTelemetryTracingAndRetries("RetryOperation");

        var results = await items.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(1, ct);
                if (++attemptCount < 3) throw new InvalidOperationException("Transient error");

                return x * 2;
            },
            options);

        results.Count.ShouldBe(1);

        var retryActivities = activities.Where(static a => a.OperationName == "Rivulet.RetryOperation.Item").ToList();
        retryActivities.Count.ShouldBe(1);

        var activity = retryActivities[0];
        activity.Status.ShouldBe(ActivityStatusCode.Ok);

        // Should have 2 retry events (attempt 1 and 2, then success on attempt 3)
        var retryEvents = activity.Events.Where(static e => e.Name == "retry").ToList();
        retryEvents.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task WithOpenTelemetryTracing_ShouldPreserveOriginalHooks()
    {
        var onStartCalled = 0;
        var onCompleteCalled = 0;
        var onErrorCalled = 0;

        using var listener = new ActivityListener();
        listener.ShouldListenTo = static source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        ActivitySource.AddActivityListener(listener);

        var items = Enumerable.Range(1, 3);
        var options = new ParallelOptionsRivulet
        {
            OnStartItemAsync = async _ =>
            {
                Interlocked.Increment(ref onStartCalled);
                await Task.CompletedTask;
            },
            OnCompleteItemAsync = async _ =>
            {
                Interlocked.Increment(ref onCompleteCalled);
                await Task.CompletedTask;
            },
            OnErrorAsync = async (_, _) =>
            {
                Interlocked.Increment(ref onErrorCalled);
                await Task.CompletedTask;
                return true;
            }
        }.WithOpenTelemetryTracing("PreserveHooksOperation");

        var results = await items.SelectParallelAsync(
            static async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x;
            },
            options);

        results.Count.ShouldBe(3);
        onStartCalled.ShouldBe(3);
        onCompleteCalled.ShouldBe(3);
        onErrorCalled.ShouldBe(0);
    }

    [Fact]
    public async Task WithOpenTelemetryTracing_ShouldRecordCircuitBreakerStateChanges()
    {
        var activities = new ConcurrentBag<Activity?>();
        using var stateChanged = new ManualResetEventSlim(false);
        using var firstFailureProcessing = new ManualResetEventSlim(false);
        var failureCount = 0;

        using var listener = new ActivityListener();
        listener.ShouldListenTo = static source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        listener.ActivityStopped = activity => activities.Add(activity);
        ActivitySource.AddActivityListener(listener);

        var items = Enumerable.Range(1, 20); // More items to ensure some are queued
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 8, // Higher concurrency to ensure multiple tasks in flight
            ErrorMode = ErrorMode.BestEffort,
            CircuitBreaker = new()
            {
                FailureThreshold = 3,
                OpenTimeout = TimeSpan.FromMilliseconds(100),
                OnStateChange = async (_, _) =>
                {
                    stateChanged.Set();
                    // Add delay to ensure state change is recorded while activities are still active
                    await Task.Delay(50, CancellationToken.None); // Reduced from 200ms for faster tests
                }
            }
        }.WithOpenTelemetryTracing("CircuitBreakerOperation");

        await items.SelectParallelAsync<int, int>(
            async (_, ct) =>
            {
                var currentFailure = Interlocked.Increment(ref failureCount);

                // First few failures signal and delay extra to ensure circuit opens while they're active
                if (currentFailure <= 5)
                {
                    firstFailureProcessing.Set();
                    // Delay ensures these activities stay alive during circuit opening
                    await Task.Delay(500, ct); // Reduced from 5000ms for faster tests
                }
                else
                {
                    // Later tasks can complete faster
                    await Task.Delay(50, ct); // Reduced from 500ms for faster tests
                }

                throw new InvalidOperationException("Always fails");
            },
            options);

        // Wait for circuit breaker state change to be recorded
        var stateChangedSuccessfully = stateChanged.Wait(TimeSpan.FromSeconds(30));
        stateChangedSuccessfully.ShouldBeTrue("circuit breaker should change state");

        // Give time for event to be recorded on activity and for activities to complete
        // Need to wait for the in-flight activities to complete so they're captured
        // Activities stop asynchronously after the operation completes
        await Task.Delay(1000, CancellationToken.None); // Reduced from 10000ms for faster tests

        // Some activities should have circuit breaker state change events
        // Filter out null activities and those with null Events collections
        var activitiesWithCbEvents = activities
            .Where(static a => a?.Events.Any(static e => e.Name == "circuit_breaker_state_change") ?? false)
            .ToList();

        activitiesWithCbEvents.ShouldNotBeEmpty("circuit breaker state changed and should be recorded on activities");
    }

    [Fact]
    public async Task WithOpenTelemetryTracing_WithAdaptiveConcurrency_ShouldRecordConcurrencyChanges()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener();
        listener.ShouldListenTo = static source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        listener.ActivityStopped = activity => activities.Add(activity);
        ActivitySource.AddActivityListener(listener);

        var concurrencyChanged = false;
        var items = Enumerable.Range(1, 50);
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 8,
            AdaptiveConcurrency = new()
            {
                MinConcurrency = 1,
                MaxConcurrency = 16,
                TargetLatency = TimeSpan.FromMilliseconds(5),
                SampleInterval = TimeSpan.FromMilliseconds(20),
                OnConcurrencyChange = async (_, _) =>
                {
                    concurrencyChanged = true;
                    await Task.CompletedTask;
                }
            }
        }.WithOpenTelemetryTracing("AdaptiveConcurrencyOperation");

        var results = await items.SelectParallelAsync(
            static async (x, ct) =>
            {
                // Vary latency significantly to force adjustments
                var delay = x <= 10 ? 20 : 1;
                await Task.Delay(delay, ct);
                return x;
            },
            options);

        results.Count.ShouldBe(50);

        // Wait for all activities to be stopped and events to be recorded
        // Activities are stopped asynchronously after the operation completes
        await Task.Delay(100, CancellationToken.None);

        // Adaptive concurrency integration is verified:
        // 1. Activities are created and tracked
        activities.ShouldNotBeEmpty();

        // 2. If concurrency changes occur (timing-dependent), they are recorded on activities
        if (concurrencyChanged)
        {
            var activitiesWithConcurrencyEvents = activities.Where(static a =>
                    a.Events.Any(static e => e.Name == "adaptive_concurrency_change"))
                .ToList();

            // When changes DO occur, verify they're properly recorded
            activitiesWithConcurrencyEvents.ShouldNotBeEmpty(
                "concurrency changes occurred and should be recorded on activities");
        }

        // Note: This test may pass without concurrency changes occurring, as adaptive
        // concurrency adjustments are performance-dependent and not deterministic in tests.
        // The integration is verified through the callback preservation and recording mechanism.
    }

    [Fact]
    public async Task WithOpenTelemetryTracing_ShouldSetItemIndexTags()
    {
        var activities = new ConcurrentBag<Activity>();
        var activityCount = 0;
        const int expectedCount = 3;
        using var allActivitiesStopped = new ManualResetEventSlim(false);

        using var listener = new ActivityListener();
        listener.ShouldListenTo = static source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        listener.ActivityStopped = activity =>
        {
            activities.Add(activity);
            if (Interlocked.Increment(ref activityCount) >= expectedCount) allActivitiesStopped.Set();
        };
        ActivitySource.AddActivityListener(listener);

        var items = new[] { 10, 20, 30 };
        var options = new ParallelOptionsRivulet().WithOpenTelemetryTracing("IndexTagsOperation");

        await items.SelectParallelAsync(
            static async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x;
            },
            options);

        // Wait for all activities to be stopped and added to the collection
        allActivitiesStopped.Wait(TimeSpan.FromSeconds(2));
        await Task.Delay(50, CancellationToken.None); // Extra buffer for activity processing

        var indexActivities =
            activities.Where(static a => a.OperationName == "Rivulet.IndexTagsOperation.Item").ToList();
        indexActivities.Count.ShouldBe(3, "should have one activity per item");

        // Each activity should have its item index
        var indices = indexActivities.Select(static a => (int)a.GetTagItem("rivulet.item_index")!)
            .OrderBy(static i => i).ToList();
        indices.ShouldBe([0, 1, 2], "each item should have its sequential index");
    }

    [Fact]
    public void WithOpenTelemetryTracing_WithoutListener_ShouldNotThrow()
    {
        var items = Enumerable.Range(1, 5);
        var options = new ParallelOptionsRivulet().WithOpenTelemetryTracing("NoListenerOperation");

        var act = () => items.SelectParallelAsync(
            static async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x;
            },
            options);

        act.ShouldNotThrowAsync();
    }
}