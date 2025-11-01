using System.Diagnostics;
using FluentAssertions;

namespace Rivulet.Diagnostics.OpenTelemetry.Tests;

[Collection("ActivitySource Tests")]
public class RivuletActivitySourceTests
{

    [Fact]
    public void ActivitySource_ShouldHaveCorrectNameAndVersion()
    {
        RivuletActivitySource.Source.Name.Should().Be("Rivulet.Core");
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
        listener.ShouldListenTo = source => source.Name == RivuletActivitySource.SourceName;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        ActivitySource.AddActivityListener(listener);

        var activity = RivuletActivitySource.StartOperation("TestOperation", 100);

        try
        {
            activity.Should().NotBeNull();
            activity!.OperationName.Should().Be("Rivulet.TestOperation");
            activity.GetTagItem("rivulet.total_items").Should().Be(100);
        }
        finally
        {
            activity?.Dispose();
        }
    }

    [Fact]
    public void StartItemActivity_WithListener_ShouldCreateActivityWithIndex()
    {
        using var listener = new ActivityListener();
        listener.ShouldListenTo = source => source.Name == RivuletActivitySource.SourceName;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        ActivitySource.AddActivityListener(listener);

        var activity = RivuletActivitySource.StartItemActivity("ProcessItem", 42);

        try
        {
            activity.Should().NotBeNull();
            activity!.OperationName.Should().Be("Rivulet.ProcessItem.Item");
            activity.GetTagItem("rivulet.item_index").Should().Be(42);
        }
        finally
        {
            activity?.Dispose();
        }
    }

    [Fact]
    public void RecordRetry_ShouldAddEventWithRetryDetails()
    {
        using var listener = new ActivityListener();
        listener.ShouldListenTo = source => source.Name == RivuletActivitySource.SourceName;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        ActivitySource.AddActivityListener(listener);

        var activity = RivuletActivitySource.StartItemActivity("ProcessItem", 0);

        try
        {
            var exception = new InvalidOperationException("Test error");
            RivuletActivitySource.RecordRetry(activity, 1, exception);

            activity!.Events.Should().HaveCount(1);
            var retryEvent = activity.Events.First();
            retryEvent.Name.Should().Be("retry");
            retryEvent.Tags.Should().Contain(tag => tag.Key == "rivulet.retry_attempt" && (int)tag.Value! == 1);
            retryEvent.Tags.Should().Contain(tag => tag.Key == "exception.type" && ((string)tag.Value!).EndsWith("InvalidOperationException"));
            activity.GetTagItem("rivulet.retries").Should().Be(1);
        }
        finally
        {
            activity?.Dispose();
        }
    }

    [Fact]
    public void RecordError_ShouldSetErrorStatusAndTags()
    {
        using var listener = new ActivityListener();
        listener.ShouldListenTo = source => source.Name == RivuletActivitySource.SourceName;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        ActivitySource.AddActivityListener(listener);

        var activity = RivuletActivitySource.StartItemActivity("ProcessItem", 0);

        try
        {
            var exception = new InvalidOperationException("Test error");
            RivuletActivitySource.RecordError(activity, exception, isTransient: true);

            activity!.Status.Should().Be(ActivityStatusCode.Error);
            activity.StatusDescription.Should().Be("Test error");
            activity.GetTagItem("rivulet.error.transient").Should().Be(true);
        }
        finally
        {
            activity?.Dispose();
        }
    }

    [Fact]
    public void RecordSuccess_ShouldSetOkStatusAndItemsProcessed()
    {
        using var listener = new ActivityListener();
        listener.ShouldListenTo = source => source.Name == RivuletActivitySource.SourceName;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        ActivitySource.AddActivityListener(listener);

        var activity = RivuletActivitySource.StartItemActivity("ProcessItem", 0);

        try
        {
            RivuletActivitySource.RecordSuccess(activity, 10);

            activity!.Status.Should().Be(ActivityStatusCode.Ok);
            activity.GetTagItem("rivulet.items_processed").Should().Be(10);
        }
        finally
        {
            activity?.Dispose();
        }
    }

    [Fact]
    public void RecordCircuitBreakerStateChange_ShouldAddEvent()
    {
        using var listener = new ActivityListener();
        listener.ShouldListenTo = source => source.Name == RivuletActivitySource.SourceName;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        ActivitySource.AddActivityListener(listener);

        var activity = RivuletActivitySource.StartOperation("TestOperation");

        try
        {
            RivuletActivitySource.RecordCircuitBreakerStateChange(activity, "Open");

            activity!.Events.Should().HaveCount(1);
            var cbEvent = activity.Events.First();
            cbEvent.Name.Should().Be("circuit_breaker_state_change");
            cbEvent.Tags.Should().Contain(tag => tag.Key == "rivulet.circuit_breaker.state" && (string)tag.Value! == "Open");
        }
        finally
        {
            activity?.Dispose();
        }
    }

    [Fact]
    public void RecordConcurrencyChange_ShouldAddEventAndUpdateTag()
    {
        using var listener = new ActivityListener();
        listener.ShouldListenTo = source => source.Name == RivuletActivitySource.SourceName;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        ActivitySource.AddActivityListener(listener);

        var activity = RivuletActivitySource.StartOperation("TestOperation");

        try
        {
            RivuletActivitySource.RecordConcurrencyChange(activity, 16, 32);

            activity!.Events.Should().HaveCount(1);
            var concurrencyEvent = activity.Events.First();
            concurrencyEvent.Name.Should().Be("adaptive_concurrency_change");
            concurrencyEvent.Tags.Should().Contain(tag => tag.Key == "rivulet.concurrency.old" && (int)tag.Value! == 16);
            concurrencyEvent.Tags.Should().Contain(tag => tag.Key == "rivulet.concurrency.new" && (int)tag.Value! == 32);
            activity.GetTagItem("rivulet.concurrency.current").Should().Be(32);
        }
        finally
        {
            activity?.Dispose();
        }
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
