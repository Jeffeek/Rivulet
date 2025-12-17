using System.Data;

namespace Rivulet.Sql.Tests;

public sealed class BulkOperationOptionsTests
{
    [Fact]
    public void BulkOperationOptions_DefaultValues_ShouldBeCorrect()
    {
        var options = new BulkOperationOptions();

        options.BatchSize.ShouldBe(1000);
        options.UseTransaction.ShouldBeTrue();
        options.SqlOptions.ShouldBeNull();
        options.OnBatchStartAsync.ShouldBeNull();
        options.OnBatchCompleteAsync.ShouldBeNull();
        options.OnBatchErrorAsync.ShouldBeNull();
    }

    [Fact]
    public async Task BulkOperationOptions_WithInitProperties_ShouldSetCorrectly()
    {
        var batchStartCalled = false;
        var batchCompleteCalled = false;
        var batchErrorCalled = false;

        var options = new BulkOperationOptions
        {
            BatchSize = 500,
            UseTransaction = false,
            SqlOptions = new() { CommandTimeout = 60 },
            OnBatchStartAsync = (_, _) =>
            {
                batchStartCalled = true;
                return ValueTask.CompletedTask;
            },
            OnBatchCompleteAsync = (_, _, _) =>
            {
                batchCompleteCalled = true;
                return ValueTask.CompletedTask;
            },
            OnBatchErrorAsync = (_, _, _) =>
            {
                batchErrorCalled = true;
                return ValueTask.CompletedTask;
            }
        };

        options.BatchSize.ShouldBe(500);
        options.UseTransaction.ShouldBeFalse();
        options.SqlOptions.ShouldNotBeNull();
        options.SqlOptions!.CommandTimeout.ShouldBe(60);
        options.OnBatchStartAsync.ShouldNotBeNull();
        options.OnBatchCompleteAsync.ShouldNotBeNull();
        options.OnBatchErrorAsync.ShouldNotBeNull();

        await options.OnBatchStartAsync!.Invoke(new List<object>(), 0);
        await options.OnBatchCompleteAsync!.Invoke(new List<object>(), 0, 100);
        await options.OnBatchErrorAsync!.Invoke(new List<object>(), 0, new());

        batchStartCalled.ShouldBeTrue();
        batchCompleteCalled.ShouldBeTrue();
        batchErrorCalled.ShouldBeTrue();
    }

    [Fact]
    public void BulkOperationOptions_WithNestedSqlOptions_ShouldPreserveConfiguration()
    {
        var options = new BulkOperationOptions
        {
            SqlOptions = new() { CommandTimeout = 90, IsolationLevel = IsolationLevel.Serializable, AutoManageConnection = false }
        };

        options.SqlOptions.ShouldNotBeNull();
        options.SqlOptions!.CommandTimeout.ShouldBe(90);
        options.SqlOptions.IsolationLevel.ShouldBe(IsolationLevel.Serializable);
        options.SqlOptions.AutoManageConnection.ShouldBeFalse();
    }
}