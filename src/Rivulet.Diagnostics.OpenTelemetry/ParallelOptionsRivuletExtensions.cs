using Rivulet.Core;
using Rivulet.Core.Resilience;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Rivulet.Diagnostics.OpenTelemetry;

/// <summary>
///     Extension methods for integrating OpenTelemetry with Rivulet parallel operations.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
public static class ParallelOptionsRivuletExtensions
{
    /// <summary>
    ///     Adds OpenTelemetry distributed tracing to Rivulet parallel operations.
    /// </summary>
    public static ParallelOptionsRivulet WithOpenTelemetryTracing(
        this ParallelOptionsRivulet options,
        string operationName) => CreateTracingOptions(options, operationName, false);

    /// <summary>
    ///     Adds OpenTelemetry distributed tracing with retry tracking to Rivulet parallel operations.
    /// </summary>
    public static ParallelOptionsRivulet WithOpenTelemetryTracingAndRetries(
        this ParallelOptionsRivulet options,
        string operationName,
        bool trackRetries = true) =>
        CreateTracingOptions(options, operationName, trackRetries && options.MaxRetries > 0);

    private static ParallelOptionsRivulet CreateTracingOptions(
        ParallelOptionsRivulet options,
        string operationName,
        bool trackRetries)
    {
        var itemActivities = new ConcurrentDictionary<int, Activity>();
        var asyncLocalActivity = new AsyncLocal<Activity?>();

        return new()
        {
            MaxDegreeOfParallelism = options.MaxDegreeOfParallelism,
            PerItemTimeout = options.PerItemTimeout,
            ErrorMode = options.ErrorMode,
            IsTransient = options.IsTransient,
            MaxRetries = options.MaxRetries,
            BaseDelay = options.BaseDelay,
            BackoffStrategy = options.BackoffStrategy,
            ChannelCapacity = options.ChannelCapacity,
            OrderedOutput = options.OrderedOutput,
            Progress = options.Progress,
            Metrics = options.Metrics,
            RateLimit = options.RateLimit,
            OnThrottleAsync = options.OnThrottleAsync,
            OnDrainAsync = options.OnDrainAsync,
            OnStartItemAsync = index =>
            {
                if (!itemActivities.ContainsKey(index))
                {
                    var activity = RivuletActivitySource.StartItemActivity(operationName, index);
                    if (activity is null)
                        return options.OnStartItemAsync?.Invoke(index) ?? ValueTask.CompletedTask;

                    itemActivities[index] = activity;
                    asyncLocalActivity.Value = activity;
                }
                else
                    asyncLocalActivity.Value = itemActivities.GetValueOrDefault(index);

                return options.OnStartItemAsync?.Invoke(index) ?? ValueTask.CompletedTask;
            },
            OnCompleteItemAsync = index =>
            {
                if (!itemActivities.TryRemove(index, out var activity))
                    return options.OnCompleteItemAsync?.Invoke(index) ?? ValueTask.CompletedTask;

                RivuletActivitySource.RecordSuccess(activity, 1);
                activity.Stop();
                activity.Dispose();
                asyncLocalActivity.Value = null;

                return options.OnCompleteItemAsync?.Invoke(index) ?? ValueTask.CompletedTask;
            },
            OnRetryAsync = trackRetries
                ? (index, attemptNumber, exception) =>
                {
                    var activity = asyncLocalActivity.Value ?? itemActivities.GetValueOrDefault(index);
                    if (activity is not null) RivuletActivitySource.RecordRetry(activity, attemptNumber, exception);

                    return options.OnRetryAsync?.Invoke(index, attemptNumber, exception) ?? ValueTask.CompletedTask;
                }
                : options.OnRetryAsync,
            OnErrorAsync = async (index, exception) =>
            {
                var activity = asyncLocalActivity.Value ?? itemActivities.GetValueOrDefault(index);

                if (activity is null)
                    return options.OnErrorAsync is null || await options.OnErrorAsync(index, exception).ConfigureAwait(false);

                var previousActivity = Activity.Current;
                try
                {
                    Activity.Current = activity;

                    var isTransient = options.IsTransient?.Invoke(exception) ?? false;
                    RivuletActivitySource.RecordError(activity, exception, isTransient);

                    var willRetry = isTransient && options.MaxRetries > 0;
                    if (willRetry || !itemActivities.TryRemove(index, out _))
                        return options.OnErrorAsync is null || await options.OnErrorAsync(index, exception).ConfigureAwait(false);

                    try
                    {
                        activity.Stop();
                    }
                    finally
                    {
                        asyncLocalActivity.Value = null;
                    }

                    return options.OnErrorAsync is null ||
                           await options.OnErrorAsync(index, exception).ConfigureAwait(false);
                }
                finally
                {
                    Activity.Current = previousActivity;
                }
            },
            CircuitBreaker = CreateCircuitBreakerOptions(options.CircuitBreaker, itemActivities, asyncLocalActivity),
            AdaptiveConcurrency =
                CreateAdaptiveConcurrencyOptions(options.AdaptiveConcurrency, itemActivities, asyncLocalActivity)
        };
    }

    private static CircuitBreakerOptions? CreateCircuitBreakerOptions(
        CircuitBreakerOptions? sourceOptions,
        ConcurrentDictionary<int, Activity> itemActivities,
        AsyncLocal<Activity?> asyncLocalActivity)
    {
        if (sourceOptions is null) return null;

        return new()
        {
            FailureThreshold = sourceOptions.FailureThreshold,
            SuccessThreshold = sourceOptions.SuccessThreshold,
            OpenTimeout = sourceOptions.OpenTimeout,
            SamplingDuration = sourceOptions.SamplingDuration,
            OnStateChange = (oldState, newState) =>
            {
                var currentActivity = asyncLocalActivity.Value ?? Activity.Current
                    ?? itemActivities.Values.FirstOrDefault();
                RivuletActivitySource.RecordCircuitBreakerStateChange(currentActivity, newState.ToString());

                return sourceOptions.OnStateChange?.Invoke(oldState, newState) ?? ValueTask.CompletedTask;
            }
        };
    }

    private static AdaptiveConcurrencyOptions? CreateAdaptiveConcurrencyOptions(
        AdaptiveConcurrencyOptions? sourceOptions,
        ConcurrentDictionary<int, Activity> itemActivities,
        AsyncLocal<Activity?> asyncLocalActivity)
    {
        if (sourceOptions is null) return null;

        return new()
        {
            MinConcurrency = sourceOptions.MinConcurrency,
            MaxConcurrency = sourceOptions.MaxConcurrency,
            InitialConcurrency = sourceOptions.InitialConcurrency,
            SampleInterval = sourceOptions.SampleInterval,
            TargetLatency = sourceOptions.TargetLatency,
            MinSuccessRate = sourceOptions.MinSuccessRate,
            IncreaseStrategy = sourceOptions.IncreaseStrategy,
            DecreaseStrategy = sourceOptions.DecreaseStrategy,
            OnConcurrencyChange = (oldValue, newValue) =>
            {
                var currentActivity = asyncLocalActivity.Value ?? Activity.Current
                    ?? itemActivities.Values.FirstOrDefault();
                RivuletActivitySource.RecordConcurrencyChange(currentActivity, oldValue, newValue);

                return sourceOptions.OnConcurrencyChange?.Invoke(oldValue, newValue) ?? ValueTask.CompletedTask;
            }
        };
    }
}