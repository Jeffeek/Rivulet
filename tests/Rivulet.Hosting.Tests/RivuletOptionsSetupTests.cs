using FluentAssertions;
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
}
