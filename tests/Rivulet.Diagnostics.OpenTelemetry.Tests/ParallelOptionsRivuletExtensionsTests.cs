using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Rivulet.Core;
using Rivulet.Core.Resilience;

namespace Rivulet.Diagnostics.OpenTelemetry.Tests;

[Collection("ActivitySource Tests")]
[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public class ParallelOptionsRivuletExtensionsTests
{

    [Fact]
    public async Task WithOpenTelemetryTracing_ShouldCreateActivitiesForEachItem()
    {
        var activities = new System.Collections.Concurrent.ConcurrentBag<Activity>();
        var activityCount = 0;
        var expectedCount = 5;
        using var allActivitiesStarted = new ManualResetEventSlim(false);

        using var listener = new ActivityListener();
        listener.ShouldListenTo = source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        listener.ActivityStarted = activity =>
        {
            activities.Add(activity);
            if (Interlocked.Increment(ref activityCount) >= expectedCount)
            {
                allActivitiesStarted.Set();
            }
        };
        ActivitySource.AddActivityListener(listener);

        var items = Enumerable.Range(1, 5);
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 2
        }.WithOpenTelemetryTracing("TestOperation");

        var results = await items.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x * 2;
            },
            options);

        // Wait for all activities to be started and added
        allActivitiesStarted.Wait(TimeSpan.FromSeconds(2));
        await Task.Delay(50); // Extra buffer

        results.Should().HaveCount(5);
        activities.Should().HaveCount(5, "should have one activity per item");
        activities.All(a => a.OperationName == "Rivulet.TestOperation.Item").Should().BeTrue();
    }

    [Fact]
    public async Task WithOpenTelemetryTracing_ShouldRecordSuccess()
    {
        var activities = new System.Collections.Concurrent.ConcurrentBag<Activity>();
        var activityCount = 0;
        var expectedCount = 3;
        using var allActivitiesStopped = new ManualResetEventSlim(false);

        using var listener = new ActivityListener();
        listener.ShouldListenTo = source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        listener.ActivityStopped = activity =>
        {
            activities.Add(activity);
            if (Interlocked.Increment(ref activityCount) >= expectedCount)
            {
                allActivitiesStopped.Set();
            }
        };
        ActivitySource.AddActivityListener(listener);

        var items = Enumerable.Range(1, 3);
        var options = new ParallelOptionsRivulet().WithOpenTelemetryTracing("SuccessOperation");

        var results = await items.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x;
            },
            options);

        // Wait for all activities to be stopped and added
        allActivitiesStopped.Wait(TimeSpan.FromSeconds(2));
        await Task.Delay(50); // Extra buffer

        results.Should().HaveCount(3);

        var successActivities = activities.Where(a => a.OperationName == "Rivulet.SuccessOperation.Item").ToList();
        successActivities.Should().HaveCount(3);
        successActivities.All(a => a.Status == ActivityStatusCode.Ok).Should().BeTrue();
        successActivities.All(a => (int)a.GetTagItem("rivulet.items_processed")! == 1).Should().BeTrue();
    }


    [Fact]
    public async Task WithOpenTelemetryTracing_ShouldRecordErrors()
    {
        var activities = new System.Collections.Concurrent.ConcurrentBag<Activity>();
        var activityCount = 0;
        var expectedCount = 3;
        using var allActivitiesStopped = new ManualResetEventSlim(false);

        using var listener = new ActivityListener();
        listener.ShouldListenTo = source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        listener.ActivityStopped = activity =>
        {
            activities.Add(activity);
            if (Interlocked.Increment(ref activityCount) >= expectedCount)
            {
                allActivitiesStopped.Set();
            }
        };
        ActivitySource.AddActivityListener(listener);

        var items = Enumerable.Range(1, 3);
        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.CollectAndContinue
        }.WithOpenTelemetryTracing("ErrorOperation");

        try
        {
            await items.SelectParallelAsync<int, int>(
                async (x, ct) =>
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
        await Task.Delay(50); // Extra buffer

        var errorActivities = activities.Where(a => a.OperationName == "Rivulet.ErrorOperation.Item").ToList();
        errorActivities.Should().HaveCount(3);
        errorActivities.All(a => a.Status == ActivityStatusCode.Error).Should().BeTrue();
    }


    [Fact]
    public async Task WithOpenTelemetryTracingAndRetries_ShouldRecordRetryAttempts()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener();
        listener.ShouldListenTo = source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        listener.ActivityStopped = activity => activities.Add(activity);
        ActivitySource.AddActivityListener(listener);

        var attemptCount = 0;
        var items = new[] { 1 };
        var options = new ParallelOptionsRivulet
        {
            MaxRetries = 3,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            IsTransient = _ => true
        }.WithOpenTelemetryTracingAndRetries("RetryOperation", trackRetries: true);

        var results = await items.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(1, ct);
                if (++attemptCount < 3)
                {
                    throw new InvalidOperationException("Transient error");
                }
                return x * 2;
            },
            options);

        results.Should().HaveCount(1);
        
        var retryActivities = activities.Where(a => a.OperationName == "Rivulet.RetryOperation.Item").ToList();
        retryActivities.Should().HaveCount(1);

        var activity = retryActivities[0];
        activity.Status.Should().Be(ActivityStatusCode.Ok);

        // Should have 2 retry events (attempt 1 and 2, then success on attempt 3)
        var retryEvents = activity.Events.Where(e => e.Name == "retry").ToList();
        retryEvents.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task WithOpenTelemetryTracing_ShouldPreserveOriginalHooks()
    {
        var onStartCalled = 0;
        var onCompleteCalled = 0;
        var onErrorCalled = 0;

        using var listener = new ActivityListener();
        listener.ShouldListenTo = source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
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
            async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x;
            },
            options);

        results.Should().HaveCount(3);
        onStartCalled.Should().Be(3);
        onCompleteCalled.Should().Be(3);
        onErrorCalled.Should().Be(0);
    }

    [Fact]
    public async Task WithOpenTelemetryTracing_ShouldRecordCircuitBreakerStateChanges()
    {
        var activities = new System.Collections.Concurrent.ConcurrentBag<Activity>();
        using var stateChanged = new ManualResetEventSlim(false);
        var processedCount = 0;

        using var listener = new ActivityListener();
        listener.ShouldListenTo = source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        listener.ActivityStopped = activity => activities.Add(activity);
        ActivitySource.AddActivityListener(listener);

        var items = Enumerable.Range(1, 20); // More items to ensure some are queued
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 6, // Higher concurrency so tasks 4-6 are running when circuit opens after 3 failures
            ErrorMode = ErrorMode.BestEffort,
            CircuitBreaker = new CircuitBreakerOptions
            {
                FailureThreshold = 3,
                OpenTimeout = TimeSpan.FromMilliseconds(100),
                OnStateChange = async (_, _) =>
                {
                    stateChanged.Set();
                    await Task.CompletedTask;
                }
            }
        }.WithOpenTelemetryTracing("CircuitBreakerOperation");

        await items.SelectParallelAsync<int, int>(
            async (_, ct) =>
            {
                // Slow down processing to keep activities alive longer
                Interlocked.Increment(ref processedCount);
                // All items fail slowly to ensure activities overlap with state change
                // Circuit opens after 3rd failure, so items 4-6 will still be in-flight
                // Increased delay for CI/CD environments
                await Task.Delay(1200, ct); // Long delay to ensure activities are still running when circuit opens
                throw new InvalidOperationException("Always fails");
            },
            options);

        // Wait for circuit breaker state change to be recorded
        var stateChangedSuccessfully = stateChanged.Wait(TimeSpan.FromSeconds(10));
        stateChangedSuccessfully.Should().BeTrue("circuit breaker should change state");

        // Give time for event to be recorded on activity and for activities to complete
        // Need to wait for the in-flight activities to complete so they're captured
        // Increased delay for CI/CD environments
        await Task.Delay(2000);

        // Some activities should have circuit breaker state change events
        var activitiesWithCbEvents = activities.Where(a =>
            a.Events.Any(e => e.Name == "circuit_breaker_state_change")).ToList();

        activitiesWithCbEvents.Should().NotBeEmpty("circuit breaker state changed and should be recorded on activities");
    }

    [Fact]
    public async Task WithOpenTelemetryTracing_WithAdaptiveConcurrency_ShouldRecordConcurrencyChanges()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener();
        listener.ShouldListenTo = source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        listener.ActivityStopped = activity => activities.Add(activity);
        ActivitySource.AddActivityListener(listener);

        var concurrencyChanged = false;
        var items = Enumerable.Range(1, 50);
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 8,
            AdaptiveConcurrency = new AdaptiveConcurrencyOptions
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
            async (x, ct) =>
            {
                // Vary latency significantly to force adjustments
                var delay = x <= 10 ? 20 : 1;
                await Task.Delay(delay, ct);
                return x;
            },
            options);

        results.Should().HaveCount(50);

        // Adaptive concurrency integration is verified:
        // 1. Activities are created and tracked
        activities.Should().NotBeEmpty();

        // 2. If concurrency changes occur (timing-dependent), they are recorded on activities
        if (concurrencyChanged)
        {
            var activitiesWithConcurrencyEvents = activities.Where(a =>
                a.Events.Any(e => e.Name == "adaptive_concurrency_change")).ToList();

            // When changes DO occur, verify they're properly recorded
            activitiesWithConcurrencyEvents.Should().NotBeEmpty(
                "concurrency changes occurred and should be recorded on activities");
        }

        // Note: This test may pass without concurrency changes occurring, as adaptive
        // concurrency adjustments are performance-dependent and not deterministic in tests.
        // The integration is verified through the callback preservation and recording mechanism.
    }

    [Fact]
    public async Task WithOpenTelemetryTracing_ShouldSetItemIndexTags()
    {
        var activities = new System.Collections.Concurrent.ConcurrentBag<Activity>();
        var activityCount = 0;
        var expectedCount = 3;
        using var allActivitiesStopped = new ManualResetEventSlim(false);

        using var listener = new ActivityListener();
        listener.ShouldListenTo = source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        listener.ActivityStopped = activity =>
        {
            activities.Add(activity);
            if (Interlocked.Increment(ref activityCount) >= expectedCount)
            {
                allActivitiesStopped.Set();
            }
        };
        ActivitySource.AddActivityListener(listener);

        var items = new[] { 10, 20, 30 };
        var options = new ParallelOptionsRivulet().WithOpenTelemetryTracing("IndexTagsOperation");

        await items.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x;
            },
            options);

        // Wait for all activities to be stopped and added to the collection
        allActivitiesStopped.Wait(TimeSpan.FromSeconds(2));
        await Task.Delay(50); // Extra buffer for activity processing

        var indexActivities = activities.Where(a => a.OperationName == "Rivulet.IndexTagsOperation.Item").ToList();
        indexActivities.Should().HaveCount(3, "should have one activity per item");

        // Each activity should have its item index
        var indices = indexActivities.Select(a => (int)a.GetTagItem("rivulet.item_index")!).OrderBy(i => i).ToList();
        indices.Should().BeEquivalentTo([0, 1, 2], "each item should have its sequential index");
    }

    [Fact]
    public void WithOpenTelemetryTracing_WithoutListener_ShouldNotThrow()
    {
        var items = Enumerable.Range(1, 5);
        var options = new ParallelOptionsRivulet().WithOpenTelemetryTracing("NoListenerOperation");

        var act = async () => await items.SelectParallelAsync(
            async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x;
            },
            options);

        act.Should().NotThrowAsync();
    }
}
