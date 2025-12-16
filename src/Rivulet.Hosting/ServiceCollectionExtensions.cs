using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rivulet.Core;

namespace Rivulet.Hosting;

/// <summary>
///     Extension methods for registering Rivulet services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Registers default ParallelOptionsRivulet configuration from IConfiguration.
    /// </summary>
    public static IServiceCollection AddRivulet(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ParallelOptionsRivulet>(configuration.GetSection(RivuletHostingConstants.ConfigurationSectionName));
        return services;
    }

    /// <summary>
    ///     Registers default ParallelOptionsRivulet configuration with a custom setup action.
    /// </summary>
    public static IServiceCollection AddRivulet(this IServiceCollection services, Action<ParallelOptionsRivulet> configure)
    {
        services.Configure(configure);
        return services;
    }

    /// <summary>
    ///     Registers a named ParallelOptionsRivulet configuration.
    /// </summary>
    public static IServiceCollection AddRivulet(this IServiceCollection services, string name, IConfiguration configuration)
    {
        services.Configure<ParallelOptionsRivulet>(name,
            configuration.GetSection($"{RivuletHostingConstants.ConfigurationSectionName}:{name}"));
        return services;
    }
}