using System;
using System.Data;
using System.Threading.Tasks;
using Rivulet.Core;

namespace Rivulet.Sql.Tests;

public class SqlOptionsTests
{
    [Fact]
    public void SqlOptions_DefaultValues_ShouldBeCorrect()
    {
        var options = new SqlOptions();

        options.CommandTimeout.Should().Be(SqlOptions.DefaultCommandTimeout);
        options.AutoManageConnection.Should().BeTrue();
        options.IsolationLevel.Should().Be(IsolationLevel.ReadCommitted);
        options.ParallelOptions.Should().BeNull();
        options.OnSqlErrorAsync.Should().BeNull();
    }

    [Fact]
    public void SqlOptions_WithInitProperties_ShouldSetCorrectly()
    {
        var errorCallbackInvoked = false;
        var options = new SqlOptions
        {
            CommandTimeout = 60,
            AutoManageConnection = false,
            IsolationLevel = IsolationLevel.Serializable,
            ParallelOptions = new()
            {
                MaxDegreeOfParallelism = 10
            },
            OnSqlErrorAsync = (_, _, _) =>
            {
                errorCallbackInvoked = true;
                return ValueTask.CompletedTask;
            }
        };

        options.CommandTimeout.Should().Be(60);
        options.AutoManageConnection.Should().BeFalse();
        options.IsolationLevel.Should().Be(IsolationLevel.Serializable);
        options.ParallelOptions.Should().NotBeNull();
        options.ParallelOptions!.MaxDegreeOfParallelism.Should().Be(10);
        options.OnSqlErrorAsync.Should().NotBeNull();

        options.OnSqlErrorAsync!.Invoke(null, new(), 0);
        errorCallbackInvoked.Should().BeTrue();
    }

    [Fact]
    public void GetMergedParallelOptions_WithNullParallelOptions_ShouldUseDefaults()
    {
        var options = new SqlOptions();
        var mergedOptions = options.GetMergedParallelOptions();

        mergedOptions.MaxRetries.Should().Be(SqlOptions.DefaultRetryCount);
        mergedOptions.PerItemTimeout.Should().Be(TimeSpan.FromSeconds(SqlOptions.DefaultCommandTimeout + 5));
        mergedOptions.IsTransient.Should().NotBeNull();
    }

    [Fact]
    public void GetMergedParallelOptions_WithCustomParallelOptions_ShouldMergeCorrectly()
    {
        var customParallelOptions = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 10,
            MaxRetries = 5,
            BaseDelay = TimeSpan.FromMilliseconds(200),
            ErrorMode = ErrorMode.CollectAndContinue
        };

        var options = new SqlOptions
        {
            ParallelOptions = customParallelOptions,
            CommandTimeout = 45
        };

        var mergedOptions = options.GetMergedParallelOptions();

        mergedOptions.MaxDegreeOfParallelism.Should().Be(10);
        mergedOptions.MaxRetries.Should().Be(5);
        mergedOptions.BaseDelay.Should().Be(TimeSpan.FromMilliseconds(200));
        mergedOptions.ErrorMode.Should().Be(ErrorMode.CollectAndContinue);
        mergedOptions.PerItemTimeout.Should().Be(TimeSpan.FromSeconds(50));
    }

    [Fact]
    public void GetMergedParallelOptions_IsTransient_ShouldHandleTimeoutException()
    {
        var options = new SqlOptions();
        var mergedOptions = options.GetMergedParallelOptions();

        var timeoutException = new TimeoutException();

        mergedOptions.IsTransient!.Invoke(timeoutException).Should().BeTrue();
    }

    [Fact]
    public void GetMergedParallelOptions_IsTransient_ShouldHandleInvalidOperationWithTimeout()
    {
        var options = new SqlOptions();
        var mergedOptions = options.GetMergedParallelOptions();

        var invalidOpException = new InvalidOperationException("Connection timeout occurred");

        mergedOptions.IsTransient!.Invoke(invalidOpException).Should().BeTrue();
    }

    [Fact]
    public void GetMergedParallelOptions_IsTransient_ShouldRespectUserProvidedPredicate()
    {
        var userProvidedCalled = false;
        var customParallelOptions = new ParallelOptionsRivulet
        {
            IsTransient = ex =>
            {
                userProvidedCalled = true;
                return ex is InvalidOperationException;
            }
        };

        var options = new SqlOptions
        {
            ParallelOptions = customParallelOptions
        };

        var mergedOptions = options.GetMergedParallelOptions();
        var customException = new InvalidOperationException();
        var result = mergedOptions.IsTransient!.Invoke(customException);

        result.Should().BeTrue();
        userProvidedCalled.Should().BeTrue();
    }

    [Fact]
    public void GetMergedParallelOptions_WithZeroMaxRetries_ShouldDefaultToThree()
    {
        var options = new SqlOptions
        {
            ParallelOptions = new()
            {
                MaxRetries = 0
            }
        };

        var mergedOptions = options.GetMergedParallelOptions();

        mergedOptions.MaxRetries.Should().Be(SqlOptions.DefaultRetryCount);
    }
}
