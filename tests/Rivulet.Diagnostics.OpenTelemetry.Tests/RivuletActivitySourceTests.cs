using System.Diagnostics;
using Rivulet.Core;

namespace Rivulet.Diagnostics.OpenTelemetry.Tests;

[Collection(TestCollections.ActivitySource)]
public sealed class RivuletActivitySourceTests
{
    [Fact]
    public void ActivitySource_ShouldHaveCorrectNameAndVersion()
    {
        RivuletActivitySource.Source.Name.ShouldBe(RivuletSharedConstants.RivuletCore);
        RivuletActivitySource.Source.Version.ShouldBe("1.3.0");
    }

    [Fact]
    public void StartOperation_WithNoListener_ShouldReturnNull()
    {
        var activity = RivuletActivitySource.StartOperation("TestOperation", 100);

        activity.ShouldBeNull();
    }

    [Fact]
    public void StartOperation_WithListener_ShouldCreateActivity()
    {
        using var listener = new ActivityListener();
        listener.ShouldListenTo = static source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        ActivitySource.AddActivityListener(listener);

        using var activity = RivuletActivitySource.StartOperation("TestOperation", 100);

        activity.ShouldNotBeNull();
        activity.OperationName.ShouldBe("Rivulet.TestOperation");
        activity.GetTagItem("rivulet.total_items").ShouldBe(100);
    }

    [Fact]
    public void StartItemActivity_WithListener_ShouldCreateActivityWithIndex()
    {
        using var listener = new ActivityListener();
        listener.ShouldListenTo = static source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        ActivitySource.AddActivityListener(listener);

        using var activity = RivuletActivitySource.StartItemActivity("ProcessItem", 42);

        activity.ShouldNotBeNull();
        activity.OperationName.ShouldBe("Rivulet.ProcessItem.Item");
        activity.GetTagItem("rivulet.item_index").ShouldBe(42);
    }

    [Fact]
    public void RecordRetry_ShouldAddEventWithRetryDetails()
    {
        using var listener = new ActivityListener();
        listener.ShouldListenTo = static source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        ActivitySource.AddActivityListener(listener);

        using var activity = RivuletActivitySource.StartItemActivity("ProcessItem", 0);

        var exception = new InvalidOperationException("Test error");
        RivuletActivitySource.RecordRetry(activity, 1, exception);

        if (activity is null) return;

        activity.Events.Count().ShouldBe(1);
        var retryEvent = activity.Events.First();
        retryEvent.Name.ShouldBe("retry");
        retryEvent.Tags.ShouldContain(static tag => tag.Key == "rivulet.retry_attempt" && (int)tag.Value! == 1);
        retryEvent.Tags.ShouldContain(static tag =>
            tag.Key == "exception.type" && ((string)tag.Value!).EndsWith("InvalidOperationException"));
        activity.GetTagItem("rivulet.retries").ShouldBe(1);
    }

    [Fact]
    public void RecordError_ShouldSetErrorStatusAndTags()
    {
        using var listener = new ActivityListener();
        listener.ShouldListenTo = static source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        ActivitySource.AddActivityListener(listener);

        using var activity = RivuletActivitySource.StartItemActivity("ProcessItem", 0);

        var exception = new InvalidOperationException("Test error");
        RivuletActivitySource.RecordError(activity, exception, true);

        activity.ShouldNotBeNull();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.StatusDescription.ShouldBe("Test error");
        activity.GetTagItem("rivulet.error.transient").ShouldBe(true);
    }

    [Fact]
    public void RecordSuccess_ShouldSetOkStatusAndItemsProcessed()
    {
        using var listener = new ActivityListener();
        listener.ShouldListenTo = static source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        ActivitySource.AddActivityListener(listener);

        using var activity = RivuletActivitySource.StartItemActivity("ProcessItem", 0);

        RivuletActivitySource.RecordSuccess(activity, 10);

        activity.ShouldNotBeNull();
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem("rivulet.items_processed").ShouldBe(10);
    }

    [Fact]
    public void RecordCircuitBreakerStateChange_ShouldAddEvent()
    {
        using var listener = new ActivityListener();
        listener.ShouldListenTo = static source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        ActivitySource.AddActivityListener(listener);

        using var activity = RivuletActivitySource.StartOperation("TestOperation");

        RivuletActivitySource.RecordCircuitBreakerStateChange(activity, "Open");

        activity.ShouldNotBeNull();
        activity.Events.Count().ShouldBe(1);
        var cbEvent = activity.Events.First();
        cbEvent.Name.ShouldBe("circuit_breaker_state_change");
        cbEvent.Tags.ShouldContain(static tag =>
            tag.Key == "rivulet.circuit_breaker.state" && (string)tag.Value! == "Open");
    }

    [Fact]
    public void RecordConcurrencyChange_ShouldAddEventAndUpdateTag()
    {
        using var listener = new ActivityListener();
        listener.ShouldListenTo = static source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        ActivitySource.AddActivityListener(listener);

        using var activity = RivuletActivitySource.StartOperation("TestOperation");

        RivuletActivitySource.RecordConcurrencyChange(activity, 16, 32);

        activity.ShouldNotBeNull();
        activity.Events.Count().ShouldBe(1);
        var concurrencyEvent = activity.Events.First();
        concurrencyEvent.Name.ShouldBe("adaptive_concurrency_change");
        concurrencyEvent.Tags.ShouldContain(static tag =>
            tag.Key == "rivulet.concurrency.old" && (int)tag.Value! == 16);
        concurrencyEvent.Tags.ShouldContain(static tag =>
            tag.Key == "rivulet.concurrency.new" && (int)tag.Value! == 32);
        activity.GetTagItem("rivulet.concurrency.current").ShouldBe(32);
    }

    [Fact]
    public void RecordRetry_WithNullActivity_ShouldNotThrow()
    {
        var exception = new InvalidOperationException("Test");
        var act = () => RivuletActivitySource.RecordRetry(null, 1, exception);

        act.ShouldNotThrow();
    }

    [Fact]
    public void RecordError_WithNullActivity_ShouldNotThrow()
    {
        var exception = new InvalidOperationException("Test");
        var act = () => RivuletActivitySource.RecordError(null, exception);

        act.ShouldNotThrow();
    }

    [Fact]
    public void RecordSuccess_WithNullActivity_ShouldNotThrow()
    {
        var act = static () => RivuletActivitySource.RecordSuccess(null, 10);

        act.ShouldNotThrow();
    }
}