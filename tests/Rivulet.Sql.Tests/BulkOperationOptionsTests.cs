using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace Rivulet.Sql.Tests;

public class BulkOperationOptionsTests
{
    [Fact]
    public void BulkOperationOptions_DefaultValues_ShouldBeCorrect()
    {
        var options = new BulkOperationOptions();

        options.BatchSize.Should().Be(1000);
        options.UseTransaction.Should().BeTrue();
        options.SqlOptions.Should().BeNull();
        options.OnBatchStartAsync.Should().BeNull();
        options.OnBatchCompleteAsync.Should().BeNull();
        options.OnBatchErrorAsync.Should().BeNull();
    }

    [Fact]
    public void BulkOperationOptions_WithInitProperties_ShouldSetCorrectly()
    {
        var batchStartCalled = false;
        var batchCompleteCalled = false;
        var batchErrorCalled = false;

        var options = new BulkOperationOptions
        {
            BatchSize = 500,
            UseTransaction = false,
            SqlOptions = new()
            {
                CommandTimeout = 60
            },
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

        options.BatchSize.Should().Be(500);
        options.UseTransaction.Should().BeFalse();
        options.SqlOptions.Should().NotBeNull();
        options.SqlOptions!.CommandTimeout.Should().Be(60);
        options.OnBatchStartAsync.Should().NotBeNull();
        options.OnBatchCompleteAsync.Should().NotBeNull();
        options.OnBatchErrorAsync.Should().NotBeNull();

        options.OnBatchStartAsync!.Invoke(new List<object>(), 0);
        options.OnBatchCompleteAsync!.Invoke(new List<object>(), 0, 100);
        options.OnBatchErrorAsync!.Invoke(new List<object>(), 0, new());

        batchStartCalled.Should().BeTrue();
        batchCompleteCalled.Should().BeTrue();
        batchErrorCalled.Should().BeTrue();
    }

    [Fact]
    public void BulkOperationOptions_WithNestedSqlOptions_ShouldPreserveConfiguration()
    {
        var options = new BulkOperationOptions
        {
            SqlOptions = new()
            {
                CommandTimeout = 90,
                IsolationLevel = IsolationLevel.Serializable,
                AutoManageConnection = false
            }
        };

        options.SqlOptions.Should().NotBeNull();
        options.SqlOptions!.CommandTimeout.Should().Be(90);
        options.SqlOptions.IsolationLevel.Should().Be(IsolationLevel.Serializable);
        options.SqlOptions.AutoManageConnection.Should().BeFalse();
    }
}
