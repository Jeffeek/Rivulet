using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Rivulet.Core;
using Rivulet.Hosting.Configuration;

namespace Rivulet.Hosting.Tests;

public class RivuletOptionsSetupTests
{
    [Fact]
    public void Constructor_WithNullConfiguration_ShouldThrow()
    {
        var act = () => new RivuletOptionsSetup(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("configuration");
    }

    [Fact]
    public void Configure_WithNonExistentSection_ShouldNotModifyOptions()
    {
        var configuration = new ConfigurationBuilder().Build();
        var setup = new RivuletOptionsSetup(configuration);
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 42,
            ErrorMode = ErrorMode.BestEffort
        };

        // Act
        setup.Configure(options);

        // Assert - options should remain unchanged
        options.MaxDegreeOfParallelism.Should().Be(42);
        options.ErrorMode.Should().Be(ErrorMode.BestEffort);
    }

    [Fact]
    public void Configure_WithValidSection_ShouldBindConfiguration()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Rivulet:MaxDegreeOfParallelism"] = "8",
            ["Rivulet:ErrorMode"] = "CollectAndContinue",
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
        options.MaxDegreeOfParallelism.Should().Be(8);
        options.ErrorMode.Should().Be(ErrorMode.CollectAndContinue);
        options.OrderedOutput.Should().BeTrue();
    }

    [Fact]
    public void Configure_WithRetryOptions_ShouldBindConfiguration()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Rivulet:MaxRetries"] = "5",
            ["Rivulet:BaseDelay"] = "00:00:02"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var setup = new RivuletOptionsSetup(configuration);
        var options = new ParallelOptionsRivulet();

        // Act
        setup.Configure(options);

        // Assert
        options.MaxRetries.Should().Be(5);
        options.BaseDelay.Should().Be(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Configure_WithPartialConfiguration_ShouldBindOnlySpecifiedValues()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Rivulet:MaxDegreeOfParallelism"] = "16"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var setup = new RivuletOptionsSetup(configuration);
        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.FailFast,
            MaxRetries = 3
        };

        // Act
        setup.Configure(options);

        // Assert - only MaxDegreeOfParallelism should change
        options.MaxDegreeOfParallelism.Should().Be(16);
        options.ErrorMode.Should().Be(ErrorMode.FailFast);
        options.MaxRetries.Should().Be(3);
    }

    [Fact]
    public void Configure_WithEmptySection_ShouldNotModifyOptions()
    {
        var configData = new Dictionary<string, string?>
        {
            ["OtherSection:SomeValue"] = "test"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var setup = new RivuletOptionsSetup(configuration);
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 10
        };

        // Act
        setup.Configure(options);

        // Assert
        options.MaxDegreeOfParallelism.Should().Be(10);
    }

    [Fact]
    public void Configure_WithChannelCapacity_ShouldBindValue()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Rivulet:ChannelCapacity"] = "2048"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var setup = new RivuletOptionsSetup(configuration);
        var options = new ParallelOptionsRivulet();

        // Act
        setup.Configure(options);

        // Assert
        options.ChannelCapacity.Should().Be(2048);
    }

    [Fact]
    public void Configure_WithPerItemTimeout_ShouldBindTimeSpan()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Rivulet:PerItemTimeout"] = "00:01:30"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var setup = new RivuletOptionsSetup(configuration);
        var options = new ParallelOptionsRivulet();

        // Act
        setup.Configure(options);

        // Assert
        options.PerItemTimeout.Should().Be(TimeSpan.FromSeconds(90));
    }

    [Fact]
    public void Configure_ImplementsIConfigureOptions_ShouldBeUsableWithOptionsPattern()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Rivulet:MaxDegreeOfParallelism"] = "4"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var setup = new RivuletOptionsSetup(configuration);

        // Assert - verify it implements the interface
        setup.Should().BeAssignableTo<IConfigureOptions<ParallelOptionsRivulet>>();
    }

    [Fact]
    public void Configure_WithMultipleCallsToSameOptions_ShouldOverwriteValues()
    {
        var configData1 = new Dictionary<string, string?>
        {
            ["Rivulet:MaxDegreeOfParallelism"] = "5"
        };

        var configData2 = new Dictionary<string, string?>
        {
            ["Rivulet:MaxDegreeOfParallelism"] = "10"
        };

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
        options.MaxDegreeOfParallelism.Should().Be(10);
    }

    [Fact]
    public void Configure_WithInvalidIntValue_ShouldIgnoreAndKeepDefault()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Rivulet:MaxDegreeOfParallelism"] = "not-a-number",
            ["Rivulet:ChannelCapacity"] = "500"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var setup = new RivuletOptionsSetup(configuration);
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 42
        };

        // Act
        setup.Configure(options);

        // Assert - invalid value should be ignored, default retained
        options.MaxDegreeOfParallelism.Should().Be(42);
        // Valid value should still be set
        options.ChannelCapacity.Should().Be(500);
    }

    [Fact]
    public void Configure_WithInvalidTimeSpanValue_ShouldIgnoreAndKeepDefault()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Rivulet:BaseDelay"] = "invalid-timespan",
            ["Rivulet:MaxRetries"] = "3"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var setup = new RivuletOptionsSetup(configuration);
        var options = new ParallelOptionsRivulet
        {
            BaseDelay = TimeSpan.FromSeconds(5)
        };

        // Act
        setup.Configure(options);

        // Assert - invalid value should be ignored, default retained
        options.BaseDelay.Should().Be(TimeSpan.FromSeconds(5));
        // Valid value should still be set
        options.MaxRetries.Should().Be(3);
    }

    [Fact]
    public void Configure_WithInvalidEnumValue_ShouldIgnoreAndKeepDefault()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Rivulet:ErrorMode"] = "InvalidMode",
            ["Rivulet:MaxDegreeOfParallelism"] = "8"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var setup = new RivuletOptionsSetup(configuration);
        var options = new ParallelOptionsRivulet
        {
            ErrorMode = ErrorMode.FailFast
        };

        // Act
        setup.Configure(options);

        // Assert - invalid enum should be ignored, default retained
        options.ErrorMode.Should().Be(ErrorMode.FailFast);
        // Valid value should still be set
        options.MaxDegreeOfParallelism.Should().Be(8);
    }

    [Fact]
    public void Configure_WithBackoffStrategy_ShouldBindValue()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Rivulet:BackoffStrategy"] = "Linear"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var setup = new RivuletOptionsSetup(configuration);
        var options = new ParallelOptionsRivulet();

        // Act
        setup.Configure(options);

        // Assert
        options.BackoffStrategy.Should().Be(Core.Resilience.BackoffStrategy.Linear);
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
        options.MaxDegreeOfParallelism.Should().Be(16);
        options.ChannelCapacity.Should().Be(1024);
        options.MaxRetries.Should().Be(5);
        options.OrderedOutput.Should().BeTrue();
        options.PerItemTimeout.Should().Be(TimeSpan.FromMinutes(2));
        options.BaseDelay.Should().Be(TimeSpan.FromSeconds(1));
        options.ErrorMode.Should().Be(ErrorMode.BestEffort);
        options.BackoffStrategy.Should().Be(Core.Resilience.BackoffStrategy.Exponential);
    }

    [Fact]
    public void Configure_WithEmptyStringValues_ShouldNotModifyOptions()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Rivulet:MaxDegreeOfParallelism"] = "",
            ["Rivulet:ChannelCapacity"] = "  "
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var setup = new RivuletOptionsSetup(configuration);
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 10,
            ChannelCapacity = 200
        };

        // Act
        setup.Configure(options);

        // Assert - empty strings should be ignored
        options.MaxDegreeOfParallelism.Should().Be(10);
        options.ChannelCapacity.Should().Be(200);
    }

    [Fact]
    public void Configure_WithMixedValidAndInvalidValues_ShouldOnlySetValidOnes()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Rivulet:MaxDegreeOfParallelism"] = "abc", // Invalid
            ["Rivulet:ChannelCapacity"] = "512", // Valid
            ["Rivulet:OrderedOutput"] = "invalid-bool", // Invalid
            ["Rivulet:MaxRetries"] = "3", // Valid
            ["Rivulet:BaseDelay"] = "not-a-timespan", // Invalid
            ["Rivulet:ErrorMode"] = "FailFast" // Valid
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var setup = new RivuletOptionsSetup(configuration);
        var options = new ParallelOptionsRivulet
        {
            MaxDegreeOfParallelism = 8,
            OrderedOutput = false,
            BaseDelay = TimeSpan.FromSeconds(2)
        };

        // Act
        setup.Configure(options);

        // Assert - only valid values should be set, invalid ones ignored
        options.MaxDegreeOfParallelism.Should().Be(8); // Invalid, kept default
        options.ChannelCapacity.Should().Be(512); // Valid, set
        options.OrderedOutput.Should().BeFalse(); // Invalid, kept default
        options.MaxRetries.Should().Be(3); // Valid, set
        options.BaseDelay.Should().Be(TimeSpan.FromSeconds(2)); // Invalid, kept default
        options.ErrorMode.Should().Be(ErrorMode.FailFast); // Valid, set
    }
}
