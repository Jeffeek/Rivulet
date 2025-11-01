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
    /// <param name="options">The options to enhance with tracing.</param>
    /// <param name="operationName">The name of the operation for the root activity.</param>
    /// <returns>The enhanced options with tracing enabled.</returns>
    /// <remarks>
    /// This method wraps the existing lifecycle hooks to automatically create activities and record traces.
    /// Configure OpenTelemetry at startup to export traces:
    /// <code>
    /// using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    ///     .AddSource(RivuletActivitySource.SourceName)
    ///     .AddJaegerExporter()
    ///     .Build();
    /// </code>
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = new ParallelOptionsRivulet
    /// {
    ///     MaxDegreeOfParallelism = 32,
    ///     MaxRetries = 3
    /// }.WithOpenTelemetryTracing("ProcessUrls");
    ///
    /// var results = await urls.SelectParallelAsync(processAsync, options);
    /// </code>
    /// </example>
    public static ParallelOptionsRivulet WithOpenTelemetryTracing(
        this ParallelOptionsRivulet options,
        string operationName)
    {
        var itemActivities = new System.Collections.Concurrent.ConcurrentDictionary<int, Activity>();
        var asyncLocalActivity = new AsyncLocal<Activity?>();

        return new ParallelOptionsRivulet
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
            OnRetryAsync = options.OnRetryAsync,
            OnStartItemAsync = async index =>
            {
                // Only create a new activity if one doesn't already exist (for retries)
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
                    // Reuse existing activity for retry attempts
                    asyncLocalActivity.Value = itemActivities.GetValueOrDefault(index);
                }

                if (options.OnStartItemAsync is not null)
                {
                    await options.OnStartItemAsync(index).ConfigureAwait(false);
                }
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
                {
                    await options.OnCompleteItemAsync(index).ConfigureAwait(false);
                }
            },
            OnErrorAsync = async (index, exception) =>
            {
                var activity = asyncLocalActivity.Value ?? itemActivities.GetValueOrDefault(index);

                if (activity is not null)
                {
                    // Temporarily set as current so nested callbacks can access it
                    var previousActivity = Activity.Current;

                    try
                    {
                        Activity.Current = activity;

                        var isTransient = options.IsTransient?.Invoke(exception) ?? false;
                        RivuletActivitySource.RecordError(activity, exception, isTransient);

                        // Only dispose if this is a final error (not going to retry)
                        var willRetry = isTransient && options.MaxRetries > 0;
                        if (!willRetry && itemActivities.TryRemove(index, out _))
                        {
                            try
                            {
                                activity.Stop();
                            }
                            finally
                            {
                                activity.Dispose();
                                asyncLocalActivity.Value = null;
                            }
                        }

                        if (options.OnErrorAsync is not null)
                        {
                            return await options.OnErrorAsync(index, exception).ConfigureAwait(false);
                        }
                        return true;
                    }
                    finally
                    {
                        // Restore previous activity
                        Activity.Current = previousActivity;
                    }
                }

                if (options.OnErrorAsync is not null)
                {
                    return await options.OnErrorAsync(index, exception).ConfigureAwait(false);
                }
                return true;
            },

            CircuitBreaker = options.CircuitBreaker != null
            ? new CircuitBreakerOptions
            {
                FailureThreshold = options.CircuitBreaker.FailureThreshold,
                SuccessThreshold = options.CircuitBreaker.SuccessThreshold,
                OpenTimeout = options.CircuitBreaker.OpenTimeout,
                SamplingDuration = options.CircuitBreaker.SamplingDuration,
                OnStateChange = async (oldState, newState) =>
                {
                    // Try to get current activity, or any active item's activity
                    var currentActivity = asyncLocalActivity.Value ?? Activity.Current
                        ?? itemActivities.Values.FirstOrDefault();
                    RivuletActivitySource.RecordCircuitBreakerStateChange(currentActivity, newState.ToString());

                    if (options.CircuitBreaker.OnStateChange is not null)
                    {
                        await options.CircuitBreaker.OnStateChange(oldState, newState).ConfigureAwait(false);
                    }
                }
            }
            : null,
            AdaptiveConcurrency = options.AdaptiveConcurrency != null
            ? new AdaptiveConcurrencyOptions
            {
                MinConcurrency = options.AdaptiveConcurrency.MinConcurrency,
                MaxConcurrency = options.AdaptiveConcurrency.MaxConcurrency,
                InitialConcurrency = options.AdaptiveConcurrency.InitialConcurrency,
                SampleInterval = options.AdaptiveConcurrency.SampleInterval,
                TargetLatency = options.AdaptiveConcurrency.TargetLatency,
                MinSuccessRate = options.AdaptiveConcurrency.MinSuccessRate,
                IncreaseStrategy = options.AdaptiveConcurrency.IncreaseStrategy,
                DecreaseStrategy = options.AdaptiveConcurrency.DecreaseStrategy,
                OnConcurrencyChange = async (oldValue, newValue) =>
                {
                    // Try to get current activity, or any active item's activity
                    var currentActivity = asyncLocalActivity.Value ?? Activity.Current
                        ?? itemActivities.Values.FirstOrDefault();
                    RivuletActivitySource.RecordConcurrencyChange(currentActivity, oldValue, newValue);

                    if (options.AdaptiveConcurrency.OnConcurrencyChange is not null)
                    {
                        await options.AdaptiveConcurrency.OnConcurrencyChange(oldValue, newValue).ConfigureAwait(false);
                    }
                }
            }
            : null
        };
    }

    /// <summary>
    /// Adds OpenTelemetry distributed tracing with retry tracking to Rivulet parallel operations.
    /// </summary>
    /// <param name="options">The options to enhance with tracing.</param>
    /// <param name="operationName">The name of the operation for the root activity.</param>
    /// <param name="trackRetries">Whether to record retry attempts as activity events.</param>
    /// <returns>The enhanced options with tracing and retry tracking enabled.</returns>
    /// <remarks>
    /// This is an advanced version that also tracks retry attempts in the trace spans.
    /// </remarks>
    public static ParallelOptionsRivulet WithOpenTelemetryTracingAndRetries(
        this ParallelOptionsRivulet options,
        string operationName,
        bool trackRetries = true)
    {
        if (!trackRetries || options.MaxRetries <= 0)
        {
            return options.WithOpenTelemetryTracing(operationName);
        }

        var itemActivities = new System.Collections.Concurrent.ConcurrentDictionary<int, Activity>();
        var asyncLocalActivity = new AsyncLocal<Activity?>();

        return new ParallelOptionsRivulet
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
                // Only create a new activity if one doesn't already exist (for retries)
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
                    // Reuse existing activity for retry attempts
                    asyncLocalActivity.Value = itemActivities.GetValueOrDefault(index);
                }

                if (options.OnStartItemAsync is not null)
                {
                    await options.OnStartItemAsync(index).ConfigureAwait(false);
                }
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
                {
                    await options.OnCompleteItemAsync(index).ConfigureAwait(false);
                }
            },
            OnRetryAsync = async (index, attemptNumber, exception) =>
            {
                var activity = asyncLocalActivity.Value ?? itemActivities.GetValueOrDefault(index);
                if (activity is not null)
                {
                    RivuletActivitySource.RecordRetry(activity, attemptNumber, exception);
                }

                if (options.OnRetryAsync is not null)
                {
                    await options.OnRetryAsync(index, attemptNumber, exception).ConfigureAwait(false);
                }
            },
            OnErrorAsync = async (index, exception) =>
            {
                var activity = asyncLocalActivity.Value ?? itemActivities.GetValueOrDefault(index);

                if (activity is not null)
                {
                    // Temporarily set as current so nested callbacks can access it
                    var previousActivity = Activity.Current;

                    try
                    {
                        Activity.Current = activity;

                        var isTransient = options.IsTransient?.Invoke(exception) ?? false;
                        RivuletActivitySource.RecordError(activity, exception, isTransient);

                        // Only dispose if this is a final error (not going to retry)
                        var willRetry = isTransient && options.MaxRetries > 0;
                        if (!willRetry && itemActivities.TryRemove(index, out _))
                        {
                            activity.Stop();
                            activity.Dispose();
                            asyncLocalActivity.Value = null;
                        }

                        if (options.OnErrorAsync is not null)
                        {
                            return await options.OnErrorAsync(index, exception).ConfigureAwait(false);
                        }
                        return true;
                    }
                    finally
                    {
                        // Restore previous activity
                        Activity.Current = previousActivity;
                    }
                }

                if (options.OnErrorAsync is not null)
                {
                    return await options.OnErrorAsync(index, exception).ConfigureAwait(false);
                }
                return true;
            },

            CircuitBreaker = options.CircuitBreaker != null
            ? new CircuitBreakerOptions
            {
                FailureThreshold = options.CircuitBreaker.FailureThreshold,
                SuccessThreshold = options.CircuitBreaker.SuccessThreshold,
                OpenTimeout = options.CircuitBreaker.OpenTimeout,
                SamplingDuration = options.CircuitBreaker.SamplingDuration,
                OnStateChange = async (oldState, newState) =>
                {
                    // Try to get current activity, or any active item's activity
                    var currentActivity = asyncLocalActivity.Value ?? Activity.Current
                        ?? itemActivities.Values.FirstOrDefault();
                    RivuletActivitySource.RecordCircuitBreakerStateChange(currentActivity, newState.ToString());

                    if (options.CircuitBreaker.OnStateChange is not null)
                    {
                        await options.CircuitBreaker.OnStateChange(oldState, newState).ConfigureAwait(false);
                    }
                }
            }
            : null,
            AdaptiveConcurrency = options.AdaptiveConcurrency != null
            ? new AdaptiveConcurrencyOptions
            {
                MinConcurrency = options.AdaptiveConcurrency.MinConcurrency,
                MaxConcurrency = options.AdaptiveConcurrency.MaxConcurrency,
                InitialConcurrency = options.AdaptiveConcurrency.InitialConcurrency,
                SampleInterval = options.AdaptiveConcurrency.SampleInterval,
                TargetLatency = options.AdaptiveConcurrency.TargetLatency,
                MinSuccessRate = options.AdaptiveConcurrency.MinSuccessRate,
                IncreaseStrategy = options.AdaptiveConcurrency.IncreaseStrategy,
                DecreaseStrategy = options.AdaptiveConcurrency.DecreaseStrategy,
                OnConcurrencyChange = async (oldValue, newValue) =>
                {
                    // Try to get current activity, or any active item's activity
                    var currentActivity = asyncLocalActivity.Value ?? Activity.Current
                        ?? itemActivities.Values.FirstOrDefault();
                    RivuletActivitySource.RecordConcurrencyChange(currentActivity, oldValue, newValue);

                    if (options.AdaptiveConcurrency.OnConcurrencyChange is not null)
                    {
                        await options.AdaptiveConcurrency.OnConcurrencyChange(oldValue, newValue).ConfigureAwait(false);
                    }
                }
            }
            : null
        };
    }
}
