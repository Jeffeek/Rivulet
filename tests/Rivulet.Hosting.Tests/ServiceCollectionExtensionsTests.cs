using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Rivulet.Core;

namespace Rivulet.Hosting.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddRivulet_WithConfiguration_ShouldRegisterOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Rivulet:MaxDegreeOfParallelism"] = "4", ["Rivulet:ChannelCapacity"] = "100"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddRivulet(configuration);

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<ParallelOptionsRivulet>>();

        options.Value.MaxDegreeOfParallelism.ShouldBe(4);
        options.Value.ChannelCapacity.ShouldBe(100);
    }

    [Fact]
    public void AddRivulet_WithConfigureAction_ShouldRegisterOptions()
    {
        var services = new ServiceCollection();
        services.AddRivulet(static _ =>
        {
            // Configuration action is called - we can't test the values directly
            // because MaxDegreeOfParallelism is init-only
        });

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<ParallelOptionsRivulet>>();

        // Just verify the option is registered with defaults
        options.Value.MaxDegreeOfParallelism.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void AddRivulet_WithNamedConfiguration_ShouldRegisterNamedOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Rivulet:Worker1:MaxDegreeOfParallelism"] = "2", ["Rivulet:Worker1:ChannelCapacity"] = "50"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddRivulet("Worker1", configuration);

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptionsSnapshot<ParallelOptionsRivulet>>();

        var workerOptions = options.Get("Worker1");
        workerOptions.MaxDegreeOfParallelism.ShouldBe(2);
        workerOptions.ChannelCapacity.ShouldBe(50);
    }

    [Fact]
    public void AddRivulet_WithConfiguration_ShouldReturnServiceCollection()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        var result = services.AddRivulet(configuration);

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddRivulet_WithConfigureAction_ShouldReturnServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddRivulet(static _ => { });

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddRivulet_WithNamedConfiguration_ShouldReturnServiceCollection()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        var result = services.AddRivulet("TestWorker", configuration);

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddRivulet_WithEmptyConfiguration_ShouldUseDefaults()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddRivulet(configuration);

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<ParallelOptionsRivulet>>();

        options.Value.MaxDegreeOfParallelism.ShouldBe(Environment.ProcessorCount);
    }

    [Fact]
    public void AddRivulet_MultipleRegistrations_ShouldAllowChaining()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Rivulet:MaxDegreeOfParallelism"] = "4",
                ["Rivulet:Worker1:MaxDegreeOfParallelism"] = "2",
                ["Rivulet:Worker2:MaxDegreeOfParallelism"] = "6"
            })
            .Build();

        var services = new ServiceCollection();
        services
            .AddRivulet(configuration)
            .AddRivulet("Worker1", configuration)
            .AddRivulet("Worker2", configuration);

        var serviceProvider = services.BuildServiceProvider();
        var defaultOptions = serviceProvider.GetRequiredService<IOptions<ParallelOptionsRivulet>>();
        var namedOptions = serviceProvider.GetRequiredService<IOptionsSnapshot<ParallelOptionsRivulet>>();

        defaultOptions.Value.MaxDegreeOfParallelism.ShouldBe(4);
        namedOptions.Get("Worker1").MaxDegreeOfParallelism.ShouldBe(2);
        namedOptions.Get("Worker2").MaxDegreeOfParallelism.ShouldBe(6);
    }
}