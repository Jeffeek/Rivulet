using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Rivulet.Core;
using Rivulet.Core.Resilience;
using Rivulet.Hosting.Configuration;

namespace Rivulet.Hosting.Tests;

public sealed class RivuletOptionsSetupTests
{
    [Fact]
    public void Constructor_WithNullConfiguration_ShouldThrow()
    {
        var act = static () => new RivuletOptionsSetup(null!);

        act.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe("configuration");
    }

    [Fact]
    public void Configure_WithNonExistentSection_ShouldNotModifyOptions()
    {
        var configuration = new ConfigurationBuilder().Build();
        var setup = new RivuletOptionsSetup(configuration);
        var options = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 42, ErrorMode = ErrorMode.BestEffort };

        // Act
        setup.Configure(options);

        // Assert - options should remain unchanged
        options.MaxDegreeOfParallelism.ShouldBe(42);
        options.ErrorMode.ShouldBe(ErrorMode.BestEffort);
    }

    [Fact]
    public void Configure_WithValidSection_ShouldBindConfiguration()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Rivulet:MaxDegreeOfParallelism"] = "8", ["Rivulet:ErrorMode"] = "CollectAndContinue",
            ["Rivulet:OrderedOutput"] = "true"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var setup = new RivuletOptionsSetup(configuration);
        var options = new ParallelOptionsRivulet();

        // Act
        setup.Configure(options);

        // Assert
        options.MaxDegreeOfParallelism.ShouldBe(8);
        options.ErrorMode.ShouldBe(ErrorMode.CollectAndContinue);
        options.OrderedOutput.ShouldBeTrue();
    }

    [Fact]
    public void Configure_WithRetryOptions_ShouldBindConfiguration()
    {
        var configData = new Dictionary<string, string?>
            { ["Rivulet:MaxRetries"] = "5", ["Rivulet:BaseDelay"] = "00:00:02" };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var setup = new RivuletOptionsSetup(configuration);
        var options = new ParallelOptionsRivulet();

        // Act
        setup.Configure(options);

        // Assert
        options.MaxRetries.ShouldBe(5);
        options.BaseDelay.ShouldBe(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Configure_WithPartialConfiguration_ShouldBindOnlySpecifiedValues()
    {
        var configData = new Dictionary<string, string?> { ["Rivulet:MaxDegreeOfParallelism"] = "16" };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var setup = new RivuletOptionsSetup(configuration);
        var options = new ParallelOptionsRivulet { ErrorMode = ErrorMode.FailFast, MaxRetries = 3 };

        // Act
        setup.Configure(options);

        // Assert - only MaxDegreeOfParallelism should change
        options.MaxDegreeOfParallelism.ShouldBe(16);
        options.ErrorMode.ShouldBe(ErrorMode.FailFast);
        options.MaxRetries.ShouldBe(3);
    }

    [Fact]
    public void Configure_WithEmptySection_ShouldNotModifyOptions()
    {
        var configData = new Dictionary<string, string?> { ["OtherSection:SomeValue"] = "test" };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var setup = new RivuletOptionsSetup(configuration);
        var options = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 10 };

        // Act
        setup.Configure(options);

        // Assert
        options.MaxDegreeOfParallelism.ShouldBe(10);
    }

    [Fact]
    public void Configure_WithChannelCapacity_ShouldBindValue()
    {
        var configData = new Dictionary<string, string?> { ["Rivulet:ChannelCapacity"] = "2048" };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var setup = new RivuletOptionsSetup(configuration);
        var options = new ParallelOptionsRivulet();

        // Act
        setup.Configure(options);

        // Assert
        options.ChannelCapacity.ShouldBe(2048);
    }

    [Fact]
    public void Configure_WithPerItemTimeout_ShouldBindTimeSpan()
    {
        var configData = new Dictionary<string, string?> { ["Rivulet:PerItemTimeout"] = "00:01:30" };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var setup = new RivuletOptionsSetup(configuration);
        var options = new ParallelOptionsRivulet();

        // Act
        setup.Configure(options);

        // Assert
        options.PerItemTimeout.ShouldBe(TimeSpan.FromSeconds(90));
    }

    [Fact]
    public void Configure_ImplementsIConfigureOptions_ShouldBeUsableWithOptionsPattern()
    {
        var configData = new Dictionary<string, string?> { ["Rivulet:MaxDegreeOfParallelism"] = "4" };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var setup = new RivuletOptionsSetup(configuration);

        // Assert - verify it implements the interface
        setup.ShouldBeAssignableTo<IConfigureOptions<ParallelOptionsRivulet>>();
    }

    [Fact]
    public void Configure_WithMultipleCallsToSameOptions_ShouldOverwriteValues()
    {
        var configData1 = new Dictionary<string, string?> { ["Rivulet:MaxDegreeOfParallelism"] = "5" };

        var configData2 = new Dictionary<string, string?> { ["Rivulet:MaxDegreeOfParallelism"] = "10" };

        var configuration1 = new ConfigurationBuilder()
            .AddInMemoryCollection(configData1)
            .Build();

        var configuration2 = new ConfigurationBuilder()
            .AddInMemoryCollection(configData2)
            .Build();

        var setup1 = new RivuletOptionsSetup(configuration1);
        var setup2 = new RivuletOptionsSetup(configuration2);
        var options = new ParallelOptionsRivulet();

        // Act
        setup1.Configure(options);
        setup2.Configure(options);

        // Assert
        options.MaxDegreeOfParallelism.ShouldBe(10);
    }

    [Fact]
    public void Configure_WithInvalidIntValue_ShouldIgnoreAndKeepDefault()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Rivulet:MaxDegreeOfParallelism"] = "not-a-number", ["Rivulet:ChannelCapacity"] = "500"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var setup = new RivuletOptionsSetup(configuration);
        var options = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 42 };

        // Act
        setup.Configure(options);

        // Assert - invalid value should be ignored, default retained
        options.MaxDegreeOfParallelism.ShouldBe(42);
        // Valid value should still be set
        options.ChannelCapacity.ShouldBe(500);
    }

    [Fact]
    public void Configure_WithInvalidTimeSpanValue_ShouldIgnoreAndKeepDefault()
    {
        var configData = new Dictionary<string, string?>
            { ["Rivulet:BaseDelay"] = "invalid-timespan", ["Rivulet:MaxRetries"] = "3" };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var setup = new RivuletOptionsSetup(configuration);
        var options = new ParallelOptionsRivulet { BaseDelay = TimeSpan.FromSeconds(5) };

        // Act
        setup.Configure(options);

        // Assert - invalid value should be ignored, default retained
        options.BaseDelay.ShouldBe(TimeSpan.FromSeconds(5));
        // Valid value should still be set
        options.MaxRetries.ShouldBe(3);
    }

    [Fact]
    public void Configure_WithInvalidEnumValue_ShouldIgnoreAndKeepDefault()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Rivulet:ErrorMode"] = "InvalidMode", ["Rivulet:MaxDegreeOfParallelism"] = "8"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var setup = new RivuletOptionsSetup(configuration);
        var options = new ParallelOptionsRivulet { ErrorMode = ErrorMode.FailFast };

        // Act
        setup.Configure(options);

        // Assert - invalid enum should be ignored, default retained
        options.ErrorMode.ShouldBe(ErrorMode.FailFast);
        // Valid value should still be set
        options.MaxDegreeOfParallelism.ShouldBe(8);
    }

    [Fact]
    public void Configure_WithBackoffStrategy_ShouldBindValue()
    {
        var configData = new Dictionary<string, string?> { ["Rivulet:BackoffStrategy"] = "Linear" };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var setup = new RivuletOptionsSetup(configuration);
        var options = new ParallelOptionsRivulet();

        // Act
        setup.Configure(options);

        // Assert
        options.BackoffStrategy.ShouldBe(BackoffStrategy.Linear);
    }

    [Fact]
    public void Configure_WithAllProperties_ShouldBindAllValues()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Rivulet:MaxDegreeOfParallelism"] = "16",
            ["Rivulet:ChannelCapacity"] = "1024",
            ["Rivulet:MaxRetries"] = "5",
            ["Rivulet:OrderedOutput"] = "true",
            ["Rivulet:PerItemTimeout"] = "00:02:00",
            ["Rivulet:BaseDelay"] = "00:00:01",
            ["Rivulet:ErrorMode"] = "BestEffort",
            ["Rivulet:BackoffStrategy"] = "Exponential"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var setup = new RivuletOptionsSetup(configuration);
        var options = new ParallelOptionsRivulet();

        // Act
        setup.Configure(options);

        // Assert - all properties should be set
        options.MaxDegreeOfParallelism.ShouldBe(16);
        options.ChannelCapacity.ShouldBe(1024);
        options.MaxRetries.ShouldBe(5);
        options.OrderedOutput.ShouldBeTrue();
        options.PerItemTimeout.ShouldBe(TimeSpan.FromMinutes(2));
        options.BaseDelay.ShouldBe(TimeSpan.FromSeconds(1));
        options.ErrorMode.ShouldBe(ErrorMode.BestEffort);
        options.BackoffStrategy.ShouldBe(BackoffStrategy.Exponential);
    }

    [Fact]
    public void Configure_WithEmptyStringValues_ShouldNotModifyOptions()
    {
        var configData = new Dictionary<string, string?>
            { ["Rivulet:MaxDegreeOfParallelism"] = "", ["Rivulet:ChannelCapacity"] = "  " };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var setup = new RivuletOptionsSetup(configuration);
        var options = new ParallelOptionsRivulet { MaxDegreeOfParallelism = 10, ChannelCapacity = 200 };

        // Act
        setup.Configure(options);

        // Assert - empty strings should be ignored
        options.MaxDegreeOfParallelism.ShouldBe(10);
        options.ChannelCapacity.ShouldBe(200);
    }

    [Fact]
    public void Configure_WithMixedValidAndInvalidValues_ShouldOnlySetValidOnes()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Rivulet:MaxDegreeOfParallelism"] = "abc", // Invalid
            ["Rivulet:ChannelCapacity"] = "512",        // Valid
            ["Rivulet:OrderedOutput"] = "invalid-bool", // Invalid
            ["Rivulet:MaxRetries"] = "3",               // Valid
            ["Rivulet:BaseDelay"] = "not-a-timespan",   // Invalid
            ["Rivulet:ErrorMode"] = "FailFast"          // Valid
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var setup = new RivuletOptionsSetup(configuration);
        var options = new ParallelOptionsRivulet
            { MaxDegreeOfParallelism = 8, OrderedOutput = false, BaseDelay = TimeSpan.FromSeconds(2) };

        // Act
        setup.Configure(options);

        // Assert - only valid values should be set, invalid ones ignored
        options.MaxDegreeOfParallelism.ShouldBe(8);          // Invalid, kept default
        options.ChannelCapacity.ShouldBe(512);               // Valid, set
        options.OrderedOutput.ShouldBeFalse();               // Invalid, kept default
        options.MaxRetries.ShouldBe(3);                      // Valid, set
        options.BaseDelay.ShouldBe(TimeSpan.FromSeconds(2)); // Invalid, kept default
        options.ErrorMode.ShouldBe(ErrorMode.FailFast);      // Valid, set
    }
}