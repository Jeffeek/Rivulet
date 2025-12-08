using System.Diagnostics;
using Rivulet.Core;
using Rivulet.Core.Resilience;

namespace Rivulet.Diagnostics.OpenTelemetry;

/// <summary>
/// Extension methods for integrating OpenTelemetry with Rivulet parallel operations.
/// </summary>
public static class ParallelOptionsRivuletExtensions
{
    /// <summary>
    /// Adds OpenTelemetry distributed tracing to Rivulet parallel operations.
    /// </summary>
    public static ParallelOptionsRivulet WithOpenTelemetryTracing(
        this ParallelOptionsRivulet options,
        string operationName) => CreateTracingOptions(options, operationName, trackRetries: false);

    /// <summary>
    /// Adds OpenTelemetry distributed tracing with retry tracking to Rivulet parallel operations.
    /// </summary>
    public static ParallelOptionsRivulet WithOpenTelemetryTracingAndRetries(
        this ParallelOptionsRivulet options,
        string operationName,
        bool trackRetries = true) => CreateTracingOptions(options, operationName, trackRetries && options.MaxRetries > 0);

    private static ParallelOptionsRivulet CreateTracingOptions(
        ParallelOptionsRivulet options,
        string operationName,
        bool trackRetries)
    {
        var itemActivities = new System.Collections.Concurrent.ConcurrentDictionary<int, Activity>();
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
            OnStartItemAsync = async index =>
            {
                if (!itemActivities.ContainsKey(index))
                {
                    var activity = RivuletActivitySource.StartItemActivity(operationName, index);
                    if (activity is not null)
                    {
                        itemActivities[index] = activity;
                        asyncLocalActivity.Value = activity;
                    }
                }
                else
                {
                    asyncLocalActivity.Value = itemActivities.GetValueOrDefault(index);
                }

                if (options.OnStartItemAsync is not null)
                    await options.OnStartItemAsync(index).ConfigureAwait(false);
            },
            OnCompleteItemAsync = async index =>
            {
                if (itemActivities.TryRemove(index, out var activity))
                {
                    RivuletActivitySource.RecordSuccess(activity, 1);
                    activity.Stop();
                    activity.Dispose();
                    asyncLocalActivity.Value = null;
                }

                if (options.OnCompleteItemAsync is not null)
                    await options.OnCompleteItemAsync(index).ConfigureAwait(false);
            },
            OnRetryAsync = trackRetries
                ? async (index, attemptNumber, exception) =>
                {
                    var activity = asyncLocalActivity.Value ?? itemActivities.GetValueOrDefault(index);
                    if (activity is not null)
                        RivuletActivitySource.RecordRetry(activity, attemptNumber, exception);

                    if (options.OnRetryAsync is not null)
                        await options.OnRetryAsync(index, attemptNumber, exception).ConfigureAwait(false);
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

                    try { activity.Stop(); }
                    finally { asyncLocalActivity.Value = null; }

                    return options.OnErrorAsync is null || await options.OnErrorAsync(index, exception).ConfigureAwait(false);
                }
                finally
                {
                    Activity.Current = previousActivity;
                }
            },
            CircuitBreaker = CreateCircuitBreakerOptions(options.CircuitBreaker, itemActivities, asyncLocalActivity),
            AdaptiveConcurrency = CreateAdaptiveConcurrencyOptions(options.AdaptiveConcurrency, itemActivities, asyncLocalActivity)
        };
    }

    private static CircuitBreakerOptions? CreateCircuitBreakerOptions(
        CircuitBreakerOptions? sourceOptions,
        System.Collections.Concurrent.ConcurrentDictionary<int, Activity> itemActivities,
        AsyncLocal<Activity?> asyncLocalActivity)
    {
        if (sourceOptions is null) return null;

        return new CircuitBreakerOptions
        {
            FailureThreshold = sourceOptions.FailureThreshold,
            SuccessThreshold = sourceOptions.SuccessThreshold,
            OpenTimeout = sourceOptions.OpenTimeout,
            SamplingDuration = sourceOptions.SamplingDuration,
            OnStateChange = async (oldState, newState) =>
            {
                var currentActivity = asyncLocalActivity.Value ?? Activity.Current
                    ?? itemActivities.Values.FirstOrDefault();
                RivuletActivitySource.RecordCircuitBreakerStateChange(currentActivity, newState.ToString());

                if (sourceOptions.OnStateChange is not null)
                    await sourceOptions.OnStateChange(oldState, newState).ConfigureAwait(false);
            }
        };
    }

    private static AdaptiveConcurrencyOptions? CreateAdaptiveConcurrencyOptions(
        AdaptiveConcurrencyOptions? sourceOptions,
        System.Collections.Concurrent.ConcurrentDictionary<int, Activity> itemActivities,
        AsyncLocal<Activity?> asyncLocalActivity)
    {
        if (sourceOptions is null) return null;

        return new AdaptiveConcurrencyOptions
        {
            MinConcurrency = sourceOptions.MinConcurrency,
            MaxConcurrency = sourceOptions.MaxConcurrency,
            InitialConcurrency = sourceOptions.InitialConcurrency,
            SampleInterval = sourceOptions.SampleInterval,
            TargetLatency = sourceOptions.TargetLatency,
            MinSuccessRate = sourceOptions.MinSuccessRate,
            IncreaseStrategy = sourceOptions.IncreaseStrategy,
            DecreaseStrategy = sourceOptions.DecreaseStrategy,
            OnConcurrencyChange = async (oldValue, newValue) =>
            {
                var currentActivity = asyncLocalActivity.Value ?? Activity.Current
                    ?? itemActivities.Values.FirstOrDefault();
                RivuletActivitySource.RecordConcurrencyChange(currentActivity, oldValue, newValue);

                if (sourceOptions.OnConcurrencyChange is not null)
                    await sourceOptions.OnConcurrencyChange(oldValue, newValue).ConfigureAwait(false);
            }
        };
    }
}
