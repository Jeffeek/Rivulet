using System.Diagnostics.Tracing;
using Rivulet.Core;

namespace Rivulet.Diagnostics;

/// <summary>
///     Base class for Rivulet EventListener implementations.
///     Provides common functionality for listening to Rivulet.Core EventCounters.
/// </summary>
public abstract class RivuletEventListenerBase : EventListener
{
    /// <summary>
    ///     Gets or sets whether the listener is enabled.
    /// </summary>
    private bool IsEnabled { get; set; }

    /// <summary>
    ///     Called when an EventSource is created.
    /// </summary>
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name != RivuletSharedConstants.RivuletCore) return;

        EnableEvents(eventSource,
            EventLevel.LogAlways,
            EventKeywords.All,
            new Dictionary<string, string?> { [RivuletDiagnosticsConstants.EventCounterKeys.IntervalSec] = "1" });
        IsEnabled = true;
    }

    /// <summary>
    ///     Called when an event is written.
    /// </summary>
    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (!IsEnabled || eventData.EventSource.Name != RivuletSharedConstants.RivuletCore) return;

        if (eventData.Payload == null || eventData.Payload.Count == 0) return;

        for (var i = 0; i < eventData.Payload.Count; i++)
        {
            if (eventData.Payload[i] is not IDictionary<string, object> eventPayload) continue;

            if (!eventPayload.TryGetValue(RivuletDiagnosticsConstants.EventCounterKeys.Name, out var nameObj) ||
                nameObj is not string name) continue;

            if (!eventPayload.TryGetValue(RivuletDiagnosticsConstants.EventCounterKeys.Mean, out var meanObj) &&
                !eventPayload.TryGetValue(RivuletDiagnosticsConstants.EventCounterKeys.Increment, out meanObj))
                continue;

            var value = Convert.ToDouble(meanObj);
            var displayName = eventPayload.TryGetValue(RivuletDiagnosticsConstants.EventCounterKeys.DisplayName,
                out var displayNameObj)
                ? displayNameObj.ToString()
                : name;
            var displayUnits = eventPayload.TryGetValue(RivuletDiagnosticsConstants.EventCounterKeys.DisplayUnits,
                out var displayUnitsObj)
                ? displayUnitsObj.ToString()
                : string.Empty;

            OnCounterReceived(name, displayName ?? name, value, displayUnits ?? string.Empty);
        }
    }

    /// <summary>
    ///     Called when a counter value is received.
    /// </summary>
    /// <param name="name">The counter name.</param>
    /// <param name="displayName">The counter display name.</param>
    /// <param name="value">The counter value.</param>
    /// <param name="displayUnits">The counter display units.</param>
    protected abstract void OnCounterReceived(string name,
        string displayName,
        double value,
        string displayUnits);
}