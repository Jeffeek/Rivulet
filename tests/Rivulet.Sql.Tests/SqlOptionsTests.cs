using System.Data;
using System.Reflection;
using System.Reflection.Emit;
using Rivulet.Core;
using Rivulet.Core.Observability;
using Rivulet.Core.Resilience;

namespace Rivulet.Sql.Tests;

public sealed class SqlOptionsTests
{
    [Fact]
    public void SqlOptions_DefaultValues_ShouldBeCorrect()
    {
        var options = new SqlOptions();

        options.CommandTimeout.ShouldBe(SqlOptions.DefaultCommandTimeout);
        options.AutoManageConnection.ShouldBeTrue();
        options.IsolationLevel.ShouldBe(IsolationLevel.ReadCommitted);
        options.ParallelOptions.ShouldBeNull();
        options.OnSqlErrorAsync.ShouldBeNull();
    }

    [Fact]
    public async Task SqlOptions_WithInitProperties_ShouldSetCorrectly()
    {
        var errorCallbackInvoked = false;
        var options = new SqlOptions
        {
            CommandTimeout = 60,
            AutoManageConnection = false,
            IsolationLevel = IsolationLevel.Serializable,
            ParallelOptions = new() { MaxDegreeOfParallelism = 10 },
            OnSqlErrorAsync = (_, _, _) =>
            {
                errorCallbackInvoked = true;
                return ValueTask.CompletedTask;
            }
        };

        options.CommandTimeout.ShouldBe(60);
        options.AutoManageConnection.ShouldBeFalse();
        options.IsolationLevel.ShouldBe(IsolationLevel.Serializable);
        options.ParallelOptions.ShouldNotBeNull();
        options.ParallelOptions!.MaxDegreeOfParallelism.ShouldBe(10);
        options.OnSqlErrorAsync.ShouldNotBeNull();

        await options.OnSqlErrorAsync!.Invoke(null, new(), 0);
        errorCallbackInvoked.ShouldBeTrue();
    }

    [Fact]
    public void GetMergedParallelOptions_WithNullParallelOptions_ShouldUseDefaults()
    {
        var options = new SqlOptions();
        var mergedOptions = options.GetMergedParallelOptions();

        mergedOptions.MaxRetries.ShouldBe(SqlOptions.DefaultRetryCount);
        mergedOptions.PerItemTimeout.ShouldBe(TimeSpan.FromSeconds(SqlOptions.DefaultCommandTimeout + 5));
        mergedOptions.IsTransient.ShouldNotBeNull();
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

        var options = new SqlOptions { ParallelOptions = customParallelOptions, CommandTimeout = 45 };

        var mergedOptions = options.GetMergedParallelOptions();

        mergedOptions.MaxDegreeOfParallelism.ShouldBe(10);
        mergedOptions.MaxRetries.ShouldBe(5);
        mergedOptions.BaseDelay.ShouldBe(TimeSpan.FromMilliseconds(200));
        mergedOptions.ErrorMode.ShouldBe(ErrorMode.CollectAndContinue);
        mergedOptions.PerItemTimeout.ShouldBe(TimeSpan.FromSeconds(50));
    }

    [Fact]
    public void GetMergedParallelOptions_IsTransient_ShouldHandleTimeoutException()
    {
        var options = new SqlOptions();
        var mergedOptions = options.GetMergedParallelOptions();

        var timeoutException = new TimeoutException();

        mergedOptions.IsTransient!.Invoke(timeoutException).ShouldBeTrue();
    }

    [Fact]
    public void GetMergedParallelOptions_IsTransient_ShouldHandleInvalidOperationWithTimeout()
    {
        var options = new SqlOptions();
        var mergedOptions = options.GetMergedParallelOptions();

        var invalidOpException = new InvalidOperationException("Connection timeout occurred");

        mergedOptions.IsTransient!.Invoke(invalidOpException).ShouldBeTrue();
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

        var options = new SqlOptions { ParallelOptions = customParallelOptions };

        var mergedOptions = options.GetMergedParallelOptions();
        var customException = new InvalidOperationException();
        var result = mergedOptions.IsTransient!.Invoke(customException);

        result.ShouldBeTrue();
        userProvidedCalled.ShouldBeTrue();
    }

    [Fact]
    public void GetMergedParallelOptions_WithZeroMaxRetries_ShouldDefaultToThree()
    {
        var options = new SqlOptions { ParallelOptions = new() { MaxRetries = 0 } };

        var mergedOptions = options.GetMergedParallelOptions();

        mergedOptions.MaxRetries.ShouldBe(SqlOptions.DefaultRetryCount);
    }

    [
        Theory,
        InlineData(-2, true),
        InlineData(-1, true),
        InlineData(2, true),
        InlineData(53, true),
        InlineData(64, true),
        InlineData(233, true),
        InlineData(10053, true),
        InlineData(10054, true),
        InlineData(10060, true),
        InlineData(40197, true),
        InlineData(40501, true),
        InlineData(40613, true),
        InlineData(49918, true),
        InlineData(49919, true),
        InlineData(49920, true),
        InlineData(9999, false)
    ]
    // Timeout
    // Connection broken
    // Connection timeout
    // Connection does not exist
    // Error on server
    // Connection initialization failed
    // Transport-level error
    // Connection reset by peer
    // Network timeout
    // Service unavailable
    // Service busy
    // Database unavailable
    // Cannot process request
    // Cannot process create or update
    // Cannot process more than requests
    // Non-transient error code
    public void GetMergedParallelOptions_IsTransient_SqlServerErrors(int errorNumber, bool expectedTransient)
    {
        var options = new SqlOptions();
        var mergedOptions = options.GetMergedParallelOptions();

        // Create a mock SqlException using reflection
        var sqlException = CreateMockSqlException(errorNumber);

        mergedOptions.IsTransient!.Invoke(sqlException).ShouldBe(expectedTransient);
    }

    [
        Theory,
        InlineData("08000", true),
        InlineData("08003", true),
        InlineData("08006", true),
        InlineData("08001", true),
        InlineData("08004", true),
        InlineData("53300", true),
        InlineData("57P03", true),
        InlineData("58000", true),
        InlineData("58030", true),
        InlineData("23505", false)
    ]
    // Connection exception
    // Connection does not exist
    // Connection failure
    // Unable to establish connection
    // Server rejected connection
    // Too many connections
    // Cannot connect now
    // System error
    // IO error
    // Non-transient error (unique violation)
    public void GetMergedParallelOptions_IsTransient_PostgreSqlErrors(string sqlState, bool expectedTransient)
    {
        var options = new SqlOptions();
        var mergedOptions = options.GetMergedParallelOptions();

        // Create a mock NpgsqlException using reflection
        var npgsqlException = CreateMockNpgsqlException(sqlState);

        mergedOptions.IsTransient!.Invoke(npgsqlException).ShouldBe(expectedTransient);
    }

    [
        Theory,
        InlineData(1040, true),
        InlineData(1205, true),
        InlineData(1213, true),
        InlineData(1226, true),
        InlineData(2002, true),
        InlineData(2003, true),
        InlineData(2006, true),
        InlineData(2013, true),
        InlineData(1062, false)
    ]
    // Too many connections
    // Lock wait timeout
    // Deadlock found
    // User has exceeded resource limit
    // Can't connect to server
    // Can't connect to server
    // Server has gone away
    // Lost connection during query
    // Non-transient error (duplicate entry)
    public void GetMergedParallelOptions_IsTransient_MySqlErrors(int errorNumber, bool expectedTransient)
    {
        var options = new SqlOptions();
        var mergedOptions = options.GetMergedParallelOptions();

        // Create a mock MySqlException using reflection
        var mySqlException = CreateMockMySqlException(errorNumber);

        mergedOptions.IsTransient!.Invoke(mySqlException).ShouldBe(expectedTransient);
    }

    [Fact]
    public void GetMergedParallelOptions_IsTransient_NonSqlException_ShouldReturnFalse()
    {
        var options = new SqlOptions();
        var mergedOptions = options.GetMergedParallelOptions();

        var nonSqlException = new ArgumentException("Not a SQL exception");

        mergedOptions.IsTransient!.Invoke(nonSqlException).ShouldBeFalse();
    }

    [Fact]
    public void GetMergedParallelOptions_IsTransient_SqlExceptionWithoutNumber_ShouldReturnFalse()
    {
        var options = new SqlOptions();
        var mergedOptions = options.GetMergedParallelOptions();

        // Create exception with type name containing "SqlException" but no Number property
        var mockException = CreateMockExceptionWithTypeName("MockSqlException", null);

        mergedOptions.IsTransient!.Invoke(mockException).ShouldBeFalse();
    }

    [Fact]
    public void GetMergedParallelOptions_IsTransient_UserProvidedTakesPrecedence()
    {
        var userTransientCalled = false;
        var customParallelOptions = new ParallelOptionsRivulet
        {
            IsTransient = _ =>
            {
                userTransientCalled = true;
                return true; // User says everything is transient
            }
        };

        var options = new SqlOptions { ParallelOptions = customParallelOptions };

        var mergedOptions = options.GetMergedParallelOptions();

        // Even a non-SQL exception should be transient if user says so
        var nonSqlException = new ArgumentException("Test");
        var result = mergedOptions.IsTransient!.Invoke(nonSqlException);

        result.ShouldBeTrue();
        userTransientCalled.ShouldBeTrue();
    }

    [Fact]
    public void GetMergedParallelOptions_IsTransient_UserAndBuiltInBothApply()
    {
        var customParallelOptions = new ParallelOptionsRivulet
        {
            IsTransient = static ex => ex is ArgumentException // User defines ArgumentException as transient
        };

        var options = new SqlOptions { ParallelOptions = customParallelOptions };

        var mergedOptions = options.GetMergedParallelOptions();

        // User-defined transient exception
        mergedOptions.IsTransient!.Invoke(new ArgumentException()).ShouldBeTrue();

        // Built-in transient exception (TimeoutException)
        mergedOptions.IsTransient!.Invoke(new TimeoutException()).ShouldBeTrue();

        // Neither user-defined nor built-in
        mergedOptions.IsTransient!.Invoke(new DivideByZeroException()).ShouldBeFalse();
    }

    [Fact]
    public void GetMergedParallelOptions_ShouldPreserveAllBaseOptions()
    {
        var progress = new ProgressOptions();
        var metrics = new MetricsOptions();
        var circuitBreaker = new CircuitBreakerOptions();
        var rateLimit = new RateLimitOptions();
        var adaptiveConcurrency = new AdaptiveConcurrencyOptions();

        var baseOptions = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 42,
            BaseDelay = TimeSpan.FromSeconds(2),
            BackoffStrategy = BackoffStrategy.LinearJitter,
            ErrorMode = ErrorMode.BestEffort,
            OnStartItemAsync = static _ => ValueTask.CompletedTask,
            OnCompleteItemAsync = static _ => ValueTask.CompletedTask,
            OnErrorAsync = static (_, _) => ValueTask.FromResult(true),
            CircuitBreaker = circuitBreaker,
            RateLimit = rateLimit,
            Progress = progress,
            OrderedOutput = true,
            Metrics = metrics,
            AdaptiveConcurrency = adaptiveConcurrency
        };

        var options = new SqlOptions { ParallelOptions = baseOptions };
        var merged = options.GetMergedParallelOptions();

        merged.MaxDegreeOfParallelism.ShouldBe(42);
        merged.BaseDelay.ShouldBe(TimeSpan.FromSeconds(2));
        merged.BackoffStrategy.ShouldBe(BackoffStrategy.LinearJitter);
        merged.ErrorMode.ShouldBe(ErrorMode.BestEffort);
        merged.OnStartItemAsync.ShouldBeSameAs(baseOptions.OnStartItemAsync);
        merged.OnCompleteItemAsync.ShouldBeSameAs(baseOptions.OnCompleteItemAsync);
        merged.OnErrorAsync.ShouldBeSameAs(baseOptions.OnErrorAsync);
        merged.CircuitBreaker.ShouldBeEquivalentTo(circuitBreaker);
        merged.RateLimit.ShouldBeEquivalentTo(rateLimit);
        merged.Progress.ShouldBeEquivalentTo(progress);
        merged.OrderedOutput.ShouldBeTrue();
        merged.Metrics.ShouldBeEquivalentTo(metrics);
        merged.AdaptiveConcurrency.ShouldBeEquivalentTo(adaptiveConcurrency);
    }

    // Helper methods to create mock exceptions
    private static Exception CreateMockSqlException(int errorNumber)
    {
        var exceptionType = AssemblyBuilder.DefineDynamicAssembly(
                new("DynamicAssembly"),
                AssemblyBuilderAccess.Run)
            .DefineDynamicModule("DynamicModule")
            .DefineType("SqlException", TypeAttributes.Public, typeof(Exception));

        var numberProperty = exceptionType.DefineProperty("Number", PropertyAttributes.None, typeof(int), null);
        var numberField = exceptionType.DefineField("_number", typeof(int), FieldAttributes.Private);

        var numberGetter = exceptionType.DefineMethod("get_Number",
            MethodAttributes.Public | MethodAttributes.Virtual,
            typeof(int),
            Type.EmptyTypes);

        var getterIl = numberGetter.GetILGenerator();
        getterIl.Emit(OpCodes.Ldarg_0);
        getterIl.Emit(OpCodes.Ldfld, numberField);
        getterIl.Emit(OpCodes.Ret);

        numberProperty.SetGetMethod(numberGetter);

        var createdType = exceptionType.CreateType();
        var instance = Activator.CreateInstance(createdType);

        var field = createdType.GetField("_number", BindingFlags.NonPublic | BindingFlags.Instance);
        field!.SetValue(instance, errorNumber);

        return (Exception)instance!;
    }

    private static Exception CreateMockNpgsqlException(string sqlState)
    {
        var exceptionType = AssemblyBuilder.DefineDynamicAssembly(
                new("DynamicAssembly2"),
                AssemblyBuilderAccess.Run)
            .DefineDynamicModule("DynamicModule")
            .DefineType("NpgsqlException", TypeAttributes.Public, typeof(Exception));

        var sqlStateProperty = exceptionType.DefineProperty("SqlState", PropertyAttributes.None, typeof(string), null);
        var sqlStateField = exceptionType.DefineField("_sqlState", typeof(string), FieldAttributes.Private);

        var sqlStateGetter = exceptionType.DefineMethod("get_SqlState",
            MethodAttributes.Public | MethodAttributes.Virtual,
            typeof(string),
            Type.EmptyTypes);

        var getterIl = sqlStateGetter.GetILGenerator();
        getterIl.Emit(OpCodes.Ldarg_0);
        getterIl.Emit(OpCodes.Ldfld, sqlStateField);
        getterIl.Emit(OpCodes.Ret);

        sqlStateProperty.SetGetMethod(sqlStateGetter);

        var createdType = exceptionType.CreateType();
        var instance = Activator.CreateInstance(createdType);

        var field = createdType.GetField("_sqlState", BindingFlags.NonPublic | BindingFlags.Instance);
        field!.SetValue(instance, sqlState);

        return (Exception)instance!;
    }

    private static Exception CreateMockMySqlException(int errorNumber)
    {
        var exceptionType = AssemblyBuilder.DefineDynamicAssembly(
                new("DynamicAssembly3"),
                AssemblyBuilderAccess.Run)
            .DefineDynamicModule("DynamicModule")
            .DefineType("MySqlException", TypeAttributes.Public, typeof(Exception));

        var numberProperty = exceptionType.DefineProperty("Number", PropertyAttributes.None, typeof(int), null);
        var numberField = exceptionType.DefineField("_number", typeof(int), FieldAttributes.Private);

        var numberGetter = exceptionType.DefineMethod("get_Number",
            MethodAttributes.Public | MethodAttributes.Virtual,
            typeof(int),
            Type.EmptyTypes);

        var getterIl = numberGetter.GetILGenerator();
        getterIl.Emit(OpCodes.Ldarg_0);
        getterIl.Emit(OpCodes.Ldfld, numberField);
        getterIl.Emit(OpCodes.Ret);

        numberProperty.SetGetMethod(numberGetter);

        var createdType = exceptionType.CreateType();
        var instance = Activator.CreateInstance(createdType);

        var field = createdType.GetField("_number", BindingFlags.NonPublic | BindingFlags.Instance);
        field!.SetValue(instance, errorNumber);

        return (Exception)instance!;
    }

    private static Exception CreateMockExceptionWithTypeName(string typeName, int? number)
    {
        var assemblyName = $"DynamicAssembly_{Guid.NewGuid():N}";
        var exceptionType = AssemblyBuilder.DefineDynamicAssembly(
                new(assemblyName),
                AssemblyBuilderAccess.Run)
            .DefineDynamicModule("DynamicModule")
            .DefineType(typeName, TypeAttributes.Public, typeof(Exception));

        if (number.HasValue)
        {
            var numberProperty = exceptionType.DefineProperty("Number", PropertyAttributes.None, typeof(int), null);
            var numberField = exceptionType.DefineField("_number", typeof(int), FieldAttributes.Private);

            var numberGetter = exceptionType.DefineMethod("get_Number",
                MethodAttributes.Public | MethodAttributes.Virtual,
                typeof(int),
                Type.EmptyTypes);

            var getterIl = numberGetter.GetILGenerator();
            getterIl.Emit(OpCodes.Ldarg_0);
            getterIl.Emit(OpCodes.Ldfld, numberField);
            getterIl.Emit(OpCodes.Ret);

            numberProperty.SetGetMethod(numberGetter);
        }

        var createdType = exceptionType.CreateType();
        var instance = Activator.CreateInstance(createdType);

        if (!number.HasValue) return (Exception)instance!;

        var field = createdType.GetField("_number", BindingFlags.NonPublic | BindingFlags.Instance);
        field!.SetValue(instance, number.Value);

        return (Exception)instance!;
    }
}
