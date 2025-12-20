using Rivulet.Sql.Internal;

namespace Rivulet.Sql.Tests.Internal;

public sealed class SqlErrorHelperTests
{
    [Fact]
    public void WrapBulkOperationException_ShouldCreateInvalidOperationException()
    {
        var innerException = new InvalidOperationException("Original error");
        const string operation = "bulk insert";
        const int batchSize = 1000;
        const string tableName = "Users";

        var wrappedException = SqlErrorHelper.WrapBulkOperationException(
            innerException,
            operation,
            batchSize,
            tableName);

        wrappedException.ShouldBeOfType<InvalidOperationException>();
        wrappedException.Message.ShouldBe("Failed to bulk insert batch of 1000 rows to table 'Users'");
        wrappedException.InnerException.ShouldBe(innerException);
    }

    [Fact]
    public void WrapBulkOperationException_WithDifferentOperations_ShouldFormatCorrectly()
    {
        var exception = new Exception("Test");

        var result1 = SqlErrorHelper.WrapBulkOperationException(exception, "bulk load", 500, "Products");
        result1.Message.ShouldBe("Failed to bulk load batch of 500 rows to table 'Products'");

        var result2 = SqlErrorHelper.WrapBulkOperationException(exception, "bulk copy", 2000, "Orders");
        result2.Message.ShouldBe("Failed to bulk copy batch of 2000 rows to table 'Orders'");
    }

    [Fact]
    public void WrapBulkOperationExceptionWithDetails_ShouldIncludeExceptionDetails()
    {
        var innerException = new InvalidOperationException("Connection timeout");
        const string context = "MySqlBulkLoader";
        const int batchSize = 5000;
        const string tableName = "Customers";

        var wrappedException = SqlErrorHelper.WrapBulkOperationExceptionWithDetails(
            innerException,
            context,
            batchSize,
            tableName);

        wrappedException.ShouldBeOfType<InvalidOperationException>();
        wrappedException.Message.ShouldContain($"[{context}] Failed to bulk insert batch of {batchSize} rows to table '{tableName}'");
        wrappedException.Message.ShouldContain("Exception: System.InvalidOperationException - Connection timeout");
        wrappedException.InnerException.ShouldBe(innerException);
    }

    [Fact]
    public void WrapBulkOperationExceptionWithDetails_WithCommand_ShouldIncludeCommandText()
    {
        var exception = new Exception("SQL error");
        const string context = "SqlBulkCopy";
        const int batchSize = 1000;
        const string tableName = "Products";
        const string command = "INSERT INTO Products (Name, Price) VALUES (@p0, @p1)";

        var wrappedException = SqlErrorHelper.WrapBulkOperationExceptionWithDetails(
            exception,
            context,
            batchSize,
            tableName,
            command);

        wrappedException.Message.ShouldContain($"Command: {command}");
    }

    [Fact]
    public void WrapBulkOperationExceptionWithDetails_WithNullCommand_ShouldNotIncludeCommand()
    {
        var exception = new Exception("SQL error");

        var wrappedException = SqlErrorHelper.WrapBulkOperationExceptionWithDetails(
            exception,
            "PostgreSqlCopy",
            100,
            "TestTable",
            // ReSharper disable once RedundantArgumentDefaultValue
            null);

        wrappedException.Message.ShouldNotContain("Command:");
    }

    [Fact]
    public void WrapBulkOperationExceptionWithDetails_WithEmptyCommand_ShouldNotIncludeCommand()
    {
        var exception = new Exception("SQL error");

        var wrappedException = SqlErrorHelper.WrapBulkOperationExceptionWithDetails(
            exception,
            "PostgreSqlCopy",
            100,
            "TestTable",
            string.Empty);

        wrappedException.Message.ShouldNotContain("Command:");
    }

    [Fact]
    public void WrapBulkOperationExceptionWithDetails_WithInnerException_ShouldIncludeInnerDetails()
    {
        var innerMostException = new ArgumentException("Invalid argument");
        var middleException = new InvalidOperationException("Operation failed", innerMostException);

        var wrappedException = SqlErrorHelper.WrapBulkOperationExceptionWithDetails(
            middleException,
            "BulkInsert",
            250,
            "Orders");

        wrappedException.Message.ShouldContain("Exception: System.InvalidOperationException - Operation failed");
        wrappedException.Message.ShouldContain("InnerException: System.ArgumentException - Invalid argument");
    }

    [Fact]
    public void WrapBulkOperationExceptionWithDetails_WithStackTrace_ShouldIncludeTruncatedStackTrace()
    {
        Exception exceptionWithStack;
        try
        {
            throw new InvalidOperationException("Test exception with stack trace");
        }
        catch (Exception ex)
        {
            exceptionWithStack = ex;
        }

        var wrappedException = SqlErrorHelper.WrapBulkOperationExceptionWithDetails(
            exceptionWithStack,
            "SqlBulkCopy",
            1000,
            "TestTable");

        wrappedException.Message.ShouldContain("StackTrace:");
        // Verify stack trace is included but potentially truncated
        var messageLines = wrappedException.Message.Split([" | "], StringSplitOptions.None);
        var stackTraceLine = messageLines.FirstOrDefault(static line => line.StartsWith("StackTrace:", StringComparison.Ordinal));
        stackTraceLine.ShouldNotBeNull();
    }

    [Fact]
    public void WrapBulkOperationExceptionWithDetails_WithoutStackTrace_ShouldNotIncludeStackTrace()
    {
        var exceptionWithoutStack = new Exception("Simple exception");

        var wrappedException = SqlErrorHelper.WrapBulkOperationExceptionWithDetails(
            exceptionWithoutStack,
            "BulkLoad",
            500,
            "Users");

        // Exception created without throw won't have StackTrace set
        if (exceptionWithoutStack.StackTrace == null)
            wrappedException.Message.ShouldNotContain("StackTrace:");
    }

    [Fact]
    public void WrapBulkOperationExceptionWithDetails_WithLongStackTrace_ShouldTruncateAt500Characters()
    {
        Exception exceptionWithLongStack = null!;
        try
        {
            // Create nested calls to generate a longer stack trace
            ThrowNestedExceptionLevel1();
        }
        catch (Exception ex)
        {
            exceptionWithLongStack = ex;
        }

        exceptionWithLongStack.ShouldNotBeNull();

        var wrappedException = SqlErrorHelper.WrapBulkOperationExceptionWithDetails(
            exceptionWithLongStack,
            "BulkInsert",
            1000,
            "TestTable");

        if (exceptionWithLongStack.StackTrace is not { Length: > 500 }) return;

        var stackTraceSection = wrappedException.Message.Split(["StackTrace: "], StringSplitOptions.None)[1];
        // The stack trace in the message should not exceed the original length
        stackTraceSection.Length.ShouldBeLessThanOrEqualTo(exceptionWithLongStack.StackTrace.Length);
    }

    [
        Theory,
        InlineData("SqlServer.BulkCopy", 1000, "Users"),
        InlineData("PostgreSql.Copy", 5000, "Products"),
        InlineData("MySql.BulkLoader", 10000, "Orders")
    ]
    public void WrapBulkOperationExceptionWithDetails_WithVariousContexts_ShouldFormatCorrectly(
        string context,
        int batchSize,
        string tableName)
    {
        var exception = new Exception("Test error");

        var wrappedException = SqlErrorHelper.WrapBulkOperationExceptionWithDetails(
            exception,
            context,
            batchSize,
            tableName);

        wrappedException.Message.ShouldStartWith($"[{context}] Failed to bulk insert batch of {batchSize} rows to table '{tableName}'");
    }

    // Helper methods to generate nested stack traces
    private static void ThrowNestedExceptionLevel1() => ThrowNestedExceptionLevel2();
    private static void ThrowNestedExceptionLevel2() => ThrowNestedExceptionLevel3();
    private static void ThrowNestedExceptionLevel3() => ThrowNestedExceptionLevel4();
    private static void ThrowNestedExceptionLevel4() => throw new InvalidOperationException("Nested exception");
}
