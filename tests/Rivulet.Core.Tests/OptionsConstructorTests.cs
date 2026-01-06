using Rivulet.Core.Observability;
using Rivulet.Core.Resilience;

namespace Rivulet.Core.Tests;

/// <summary>
///     Tests for Options classes copy constructors to verify they properly copy all properties
///     and are part of the public API.
/// </summary>
public sealed class OptionsConstructorTests
{
    #region CircuitBreakerOptions Tests

    [Fact]
    public void CircuitBreakerOptions_CopyConstructor_WithNull_ShouldUseDefaults()
    {
        // Act
        var options = new CircuitBreakerOptions(null);

        // Assert
        options.FailureThreshold.ShouldBe(5);
        options.SuccessThreshold.ShouldBe(2);
        options.OpenTimeout.ShouldBe(TimeSpan.FromSeconds(30));
        options.SamplingDuration.ShouldBeNull();
        options.OnStateChange.ShouldBeNull();
    }

    [Fact]
    public void CircuitBreakerOptions_CopyConstructor_ShouldCopyAllProperties()
    {
        // Arrange
        var stateChangeCallback = static (CircuitBreakerState from, CircuitBreakerState to) => ValueTask.CompletedTask;
        var original = new CircuitBreakerOptions
        {
            FailureThreshold = 10,
            SuccessThreshold = 3,
            OpenTimeout = TimeSpan.FromMinutes(1),
            SamplingDuration = TimeSpan.FromMinutes(5),
            OnStateChange = stateChangeCallback
        };

        // Act
        var copy = new CircuitBreakerOptions(original);

        // Assert
        copy.FailureThreshold.ShouldBe(10);
        copy.SuccessThreshold.ShouldBe(3);
        copy.OpenTimeout.ShouldBe(TimeSpan.FromMinutes(1));
        copy.SamplingDuration.ShouldBe(TimeSpan.FromMinutes(5));
        copy.OnStateChange.ShouldBe(stateChangeCallback);
    }

    [Fact]
    public void CircuitBreakerOptions_CopyConstructor_WithInvalidOptions_ShouldThrow()
    {
        // Arrange
        var invalid = new CircuitBreakerOptions
        {
            FailureThreshold = -1
        };

        // Act & Assert
        Should.Throw<ArgumentException>(() => new CircuitBreakerOptions(invalid));
    }

    [Fact]
    public async Task CircuitBreakerOptions_CopyConstructor_PreservesCallback()
    {
        // Arrange
        var callbackInvoked = false;
        var original = new CircuitBreakerOptions
        {
            OnStateChange = (_, _) =>
            {
                callbackInvoked = true;
                return ValueTask.CompletedTask;
            }
        };

        // Act
        var copy = new CircuitBreakerOptions(original);

        // Assert - invoke callback to verify it was copied correctly
        copy.OnStateChange.ShouldNotBeNull();
        await copy.OnStateChange!.Invoke(CircuitBreakerState.Closed, CircuitBreakerState.Open);
        callbackInvoked.ShouldBeTrue();
    }

    #endregion

    #region MetricsOptions Tests

    [Fact]
    public void MetricsOptions_CopyConstructor_WithNull_ShouldUseDefaults()
    {
        // Act
        var options = new MetricsOptions(null);

        // Assert
        options.SampleInterval.ShouldBe(TimeSpan.FromSeconds(10));
        options.OnMetricsSample.ShouldBeNull();
    }

    [Fact]
    public void MetricsOptions_CopyConstructor_ShouldCopyAllProperties()
    {
        // Arrange
        var metricsCallback = static (MetricsSnapshot snapshot) => ValueTask.CompletedTask;
        var original = new MetricsOptions
        {
            SampleInterval = TimeSpan.FromSeconds(5),
            OnMetricsSample = metricsCallback
        };

        // Act
        var copy = new MetricsOptions(original);

        // Assert
        copy.SampleInterval.ShouldBe(TimeSpan.FromSeconds(5));
        copy.OnMetricsSample.ShouldBe(metricsCallback);
    }

    [Fact]
    public async Task MetricsOptions_CopyConstructor_PreservesCallback()
    {
        // Arrange
        MetricsSnapshot? capturedSnapshot = null;
        var original = new MetricsOptions
        {
            OnMetricsSample = snapshot =>
            {
                capturedSnapshot = snapshot;
                return ValueTask.CompletedTask;
            }
        };

        // Act
        var copy = new MetricsOptions(original);

        // Assert - invoke callback to verify it was copied correctly
        copy.OnMetricsSample.ShouldNotBeNull();
        var testSnapshot = new MetricsSnapshot
        {
            ActiveWorkers = 5,
            ItemsCompleted = 100
        };
        await copy.OnMetricsSample!.Invoke(testSnapshot);
        capturedSnapshot.ShouldNotBeNull();
        capturedSnapshot!.ActiveWorkers.ShouldBe(5);
        capturedSnapshot.ItemsCompleted.ShouldBe(100);
    }

    [Fact]
    public void MetricsOptions_CopyConstructor_WithCustomInterval_ShouldCopyCorrectly()
    {
        // Arrange
        var original = new MetricsOptions
        {
            SampleInterval = TimeSpan.FromMilliseconds(500)
        };

        // Act
        var copy = new MetricsOptions(original);

        // Assert
        copy.SampleInterval.ShouldBe(TimeSpan.FromMilliseconds(500));
    }

    #endregion

    #region ProgressOptions Tests

    [Fact]
    public void ProgressOptions_CopyConstructor_WithNull_ShouldUseDefaults()
    {
        // Act
        var options = new ProgressOptions(null);

        // Assert
        options.ReportInterval.ShouldBe(TimeSpan.FromSeconds(5));
        options.OnProgress.ShouldBeNull();
    }

    [Fact]
    public void ProgressOptions_CopyConstructor_ShouldCopyAllProperties()
    {
        // Arrange
        var progressCallback = static (ProgressSnapshot snapshot) => ValueTask.CompletedTask;
        var original = new ProgressOptions
        {
            ReportInterval = TimeSpan.FromSeconds(3),
            OnProgress = progressCallback
        };

        // Act
        var copy = new ProgressOptions(original);

        // Assert
        copy.ReportInterval.ShouldBe(TimeSpan.FromSeconds(3));
        copy.OnProgress.ShouldBe(progressCallback);
    }

    [Fact]
    public async Task ProgressOptions_CopyConstructor_PreservesCallback()
    {
        // Arrange
        ProgressSnapshot? capturedSnapshot = null;
        var original = new ProgressOptions
        {
            OnProgress = snapshot =>
            {
                capturedSnapshot = snapshot;
                return ValueTask.CompletedTask;
            }
        };

        // Act
        var copy = new ProgressOptions(original);

        // Assert - invoke callback to verify it was copied correctly
        copy.OnProgress.ShouldNotBeNull();
        var testSnapshot = new ProgressSnapshot
        {
            ItemsCompleted = 50,
            ItemsStarted = 75,
            TotalItems = 100
        };
        await copy.OnProgress!.Invoke(testSnapshot);
        capturedSnapshot.ShouldNotBeNull();
        capturedSnapshot!.ItemsCompleted.ShouldBe(50);
        capturedSnapshot.ItemsStarted.ShouldBe(75);
        capturedSnapshot.TotalItems.ShouldBe(100);
    }

    [Fact]
    public void ProgressOptions_CopyConstructor_WithCustomInterval_ShouldCopyCorrectly()
    {
        // Arrange
        var original = new ProgressOptions
        {
            ReportInterval = TimeSpan.FromMilliseconds(250)
        };

        // Act
        var copy = new ProgressOptions(original);

        // Assert
        copy.ReportInterval.ShouldBe(TimeSpan.FromMilliseconds(250));
    }

    #endregion

    #region RateLimitOptions Tests

    [Fact]
    public void RateLimitOptions_CopyConstructor_WithNull_ShouldUseDefaults()
    {
        // Act
        var options = new RateLimitOptions(null);

        // Assert
        options.TokensPerSecond.ShouldBe(100);
        options.BurstCapacity.ShouldBe(100);
        options.TokensPerOperation.ShouldBe(1.0);
    }

    [Fact]
    public void RateLimitOptions_CopyConstructor_ShouldCopyAllProperties()
    {
        // Arrange
        var original = new RateLimitOptions
        {
            TokensPerSecond = 50,
            BurstCapacity = 200,
            TokensPerOperation = 2.5
        };

        // Act
        var copy = new RateLimitOptions(original);

        // Assert
        copy.TokensPerSecond.ShouldBe(50);
        copy.BurstCapacity.ShouldBe(200);
        copy.TokensPerOperation.ShouldBe(2.5);
    }

    [Fact]
    public void RateLimitOptions_CopyConstructor_WithInvalidOptions_ShouldThrow()
    {
        // Arrange
        var invalid = new RateLimitOptions
        {
            TokensPerSecond = -10
        };

        // Act & Assert
        Should.Throw<ArgumentException>(() => new RateLimitOptions(invalid));
    }

    [Fact]
    public void RateLimitOptions_CopyConstructor_WithBurstLessThanTokensPerOperation_ShouldThrow()
    {
        // Arrange
        var invalid = new RateLimitOptions
        {
            TokensPerSecond = 100,
            BurstCapacity = 5,
            TokensPerOperation = 10 // Greater than BurstCapacity
        };

        // Act & Assert
        Should.Throw<ArgumentException>(() => new RateLimitOptions(invalid));
    }

    [Fact]
    public void RateLimitOptions_CopyConstructor_WithFractionalValues_ShouldCopyCorrectly()
    {
        // Arrange
        var original = new RateLimitOptions
        {
            TokensPerSecond = 33.33,
            BurstCapacity = 100.5,
            TokensPerOperation = 0.5
        };

        // Act
        var copy = new RateLimitOptions(original);

        // Assert
        copy.TokensPerSecond.ShouldBe(33.33);
        copy.BurstCapacity.ShouldBe(100.5);
        copy.TokensPerOperation.ShouldBe(0.5);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void AllOptionsConstructors_ShouldBePubliclyAccessible()
    {
        // This test verifies that all copy constructors are public and can be called
        // This is important for the public API and for users who want to create
        // modified copies of options objects

        // Act - Create instances using copy constructors with null
        var circuitBreakerOptions = new CircuitBreakerOptions(null);
        var metricsOptions = new MetricsOptions(null);
        var progressOptions = new ProgressOptions(null);
        var rateLimitOptions = new RateLimitOptions(null);

        // Assert - All should use defaults
        circuitBreakerOptions.ShouldNotBeNull();
        metricsOptions.ShouldNotBeNull();
        progressOptions.ShouldNotBeNull();
        rateLimitOptions.ShouldNotBeNull();
    }

    [Fact]
    public void OptionsConstructors_CanBeUsedToCreateModifiedCopies()
    {
        // This test demonstrates a common use case: creating a modified copy of options
        // This pattern is useful when you want to reuse most settings but change a few

        // Arrange - Start with some base options
        var baseCircuitBreaker = new CircuitBreakerOptions
        {
            FailureThreshold = 10,
            SuccessThreshold = 3,
            OpenTimeout = TimeSpan.FromMinutes(1)
        };

        // Act - Create a copy and modify one property using init syntax
        var copy = new CircuitBreakerOptions(baseCircuitBreaker)
        {
            FailureThreshold = 15 // Override just this property
        };

        // Assert - New value for modified property, original values for others
        copy.FailureThreshold.ShouldBe(15);
        copy.SuccessThreshold.ShouldBe(3);
        copy.OpenTimeout.ShouldBe(TimeSpan.FromMinutes(1));
    }

    #endregion
}
