using Rivulet.Core;
using System.Diagnostics;

namespace Rivulet.Diagnostics.OpenTelemetry.Tests;

[Collection(TestCollections.ActivitySource)]
public class RivuletActivitySourceTests
{

    [Fact]
    public void ActivitySource_ShouldHaveCorrectNameAndVersion()
    {
        RivuletActivitySource.Source.Name.Should().Be(RivuletSharedConstants.RivuletCore);
        RivuletActivitySource.Source.Version.Should().Be("1.2.0");
    }

    [Fact]
    public void StartOperation_WithNoListener_ShouldReturnNull()
    {
        var activity = RivuletActivitySource.StartOperation("TestOperation", 100);

        activity.Should().BeNull();
    }

    [Fact]
    public void StartOperation_WithListener_ShouldCreateActivity()
    {
        using var listener = new ActivityListener();
        listener.ShouldListenTo = source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        ActivitySource.AddActivityListener(listener);

        using var activity = RivuletActivitySource.StartOperation("TestOperation", 100);

        activity.Should().NotBeNull();
        activity.OperationName.Should().Be("Rivulet.TestOperation");
        activity.GetTagItem("rivulet.total_items").Should().Be(100);
    }

    [Fact]
    public void StartItemActivity_WithListener_ShouldCreateActivityWithIndex()
    {
        using var listener = new ActivityListener();
        listener.ShouldListenTo = source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        ActivitySource.AddActivityListener(listener);

        using var activity = RivuletActivitySource.StartItemActivity("ProcessItem", 42);

        activity.Should().NotBeNull();
        activity.OperationName.Should().Be("Rivulet.ProcessItem.Item");
        activity.GetTagItem("rivulet.item_index").Should().Be(42);
    }

    [Fact]
    public void RecordRetry_ShouldAddEventWithRetryDetails()
    {
        using var listener = new ActivityListener();
        listener.ShouldListenTo = source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        ActivitySource.AddActivityListener(listener);

        using var activity = RivuletActivitySource.StartItemActivity("ProcessItem", 0);

        var exception = new InvalidOperationException("Test error");
        RivuletActivitySource.RecordRetry(activity, 1, exception);

        if (activity is null) return;
        activity.Events.Should().HaveCount(1);
        var retryEvent = activity.Events.First();
        retryEvent.Name.Should().Be("retry");
        retryEvent.Tags.Should().Contain(tag => tag.Key == "rivulet.retry_attempt" && (int)tag.Value! == 1);
        retryEvent.Tags.Should().Contain(tag => tag.Key == "exception.type" && ((string)tag.Value!).EndsWith("InvalidOperationException"));
        activity.GetTagItem("rivulet.retries").Should().Be(1);
    }

    [Fact]
    public void RecordError_ShouldSetErrorStatusAndTags()
    {
        using var listener = new ActivityListener();
        listener.ShouldListenTo = source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        ActivitySource.AddActivityListener(listener);

        using var activity = RivuletActivitySource.StartItemActivity("ProcessItem", 0);

        var exception = new InvalidOperationException("Test error");
        RivuletActivitySource.RecordError(activity, exception, isTransient: true);

        activity.Should().NotBeNull();
        activity.Status.Should().Be(ActivityStatusCode.Error);
        activity.StatusDescription.Should().Be("Test error");
        activity.GetTagItem("rivulet.error.transient").Should().Be(true);
    }

    [Fact]
    public void RecordSuccess_ShouldSetOkStatusAndItemsProcessed()
    {
        using var listener = new ActivityListener();
        listener.ShouldListenTo = source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        ActivitySource.AddActivityListener(listener);

        using var activity = RivuletActivitySource.StartItemActivity("ProcessItem", 0);

        RivuletActivitySource.RecordSuccess(activity, 10);

        activity.Should().NotBeNull();
        activity.Status.Should().Be(ActivityStatusCode.Ok);
        activity.GetTagItem("rivulet.items_processed").Should().Be(10);
    }

    [Fact]
    public void RecordCircuitBreakerStateChange_ShouldAddEvent()
    {
        using var listener = new ActivityListener();
        listener.ShouldListenTo = source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        ActivitySource.AddActivityListener(listener);

        using var activity = RivuletActivitySource.StartOperation("TestOperation");

        RivuletActivitySource.RecordCircuitBreakerStateChange(activity, "Open");

        activity.Should().NotBeNull();
        activity.Events.Should().HaveCount(1);
        var cbEvent = activity.Events.First();
        cbEvent.Name.Should().Be("circuit_breaker_state_change");
        cbEvent.Tags.Should().Contain(tag => tag.Key == "rivulet.circuit_breaker.state" && (string)tag.Value! == "Open");
    }

    [Fact]
    public void RecordConcurrencyChange_ShouldAddEventAndUpdateTag()
    {
        using var listener = new ActivityListener();
        listener.ShouldListenTo = source => source.Name == RivuletSharedConstants.RivuletCore;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        ActivitySource.AddActivityListener(listener);

        using var activity = RivuletActivitySource.StartOperation("TestOperation");

        RivuletActivitySource.RecordConcurrencyChange(activity, 16, 32);

        activity.Should().NotBeNull();
        activity.Events.Should().HaveCount(1);
        var concurrencyEvent = activity.Events.First();
        concurrencyEvent.Name.Should().Be("adaptive_concurrency_change");
        concurrencyEvent.Tags.Should().Contain(tag => tag.Key == "rivulet.concurrency.old" && (int)tag.Value! == 16);
        concurrencyEvent.Tags.Should().Contain(tag => tag.Key == "rivulet.concurrency.new" && (int)tag.Value! == 32);
        activity.GetTagItem("rivulet.concurrency.current").Should().Be(32);
    }

    [Fact]
    public void RecordRetry_WithNullActivity_ShouldNotThrow()
    {
        var exception = new InvalidOperationException("Test");
        var act = () => RivuletActivitySource.RecordRetry(null, 1, exception);

        act.Should().NotThrow();
    }

    [Fact]
    public void RecordError_WithNullActivity_ShouldNotThrow()
    {
        var exception = new InvalidOperationException("Test");
        var act = () => RivuletActivitySource.RecordError(null, exception);

        act.Should().NotThrow();
    }

    [Fact]
    public void RecordSuccess_WithNullActivity_ShouldNotThrow()
    {
        var act = () => RivuletActivitySource.RecordSuccess(null, 10);

        act.Should().NotThrow();
    }
}
