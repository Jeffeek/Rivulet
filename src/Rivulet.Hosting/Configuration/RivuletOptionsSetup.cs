using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Rivulet.Core;
using Rivulet.Core.Resilience;

namespace Rivulet.Hosting.Configuration;

/// <summary>
///     Configures ParallelOptionsRivulet from IConfiguration using reflection to set init-only properties.
///     This is necessary because IConfigureOptions receives an already-constructed instance,
///     but ParallelOptionsRivulet uses init-only properties that can't be set normally after construction.
/// </summary>
/// <remarks>
///     Note: This class uses reflection to work around the init-only property limitation.
///     Consider using services.Configure&lt;ParallelOptionsRivulet&gt;(config.GetSection("Rivulet"))
///     directly in ServiceCollectionExtensions, which handles init-only properties properly.
/// </remarks>
internal sealed class RivuletOptionsSetup(IConfiguration configuration) : IConfigureOptions<ParallelOptionsRivulet>
{
    private readonly IConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

    public void Configure(ParallelOptionsRivulet options)
    {
        var section = _configuration.GetSection(RivuletHostingConstants.ConfigurationSectionName);
        if (!section.Exists()) return;

        // Use reflection to set init-only properties
        // This is necessary because IConfigureOptions<T> provides an already-constructed instance
        var type = typeof(ParallelOptionsRivulet);

        SetPropertyIfExists(options, type, section, nameof(ParallelOptionsRivulet.MaxDegreeOfParallelism), int.Parse);
        SetPropertyIfExists(options, type, section, nameof(ParallelOptionsRivulet.ChannelCapacity), int.Parse);
        SetPropertyIfExists(options, type, section, nameof(ParallelOptionsRivulet.MaxRetries), int.Parse);
        SetPropertyIfExists(options, type, section, nameof(ParallelOptionsRivulet.OrderedOutput), bool.Parse);
        SetPropertyIfExists(options, type, section, nameof(ParallelOptionsRivulet.PerItemTimeout), TimeSpan.Parse);
        SetPropertyIfExists(options, type, section, nameof(ParallelOptionsRivulet.BaseDelay), TimeSpan.Parse);
        SetPropertyIfExists(options, type, section, nameof(ParallelOptionsRivulet.ErrorMode), Enum.Parse<ErrorMode>);
        SetPropertyIfExists(options, type, section, nameof(ParallelOptionsRivulet.BackoffStrategy), Enum.Parse<BackoffStrategy>);
    }

    private static void SetPropertyIfExists<T>(
        ParallelOptionsRivulet options,
        Type type,
        IConfiguration section,
        string propertyName,
        Func<string, T> parser)
    {
        var configValue = section[propertyName];
        if (string.IsNullOrEmpty(configValue)) return;

        try
        {
            var value = parser(configValue);
            var property = type.GetProperty(propertyName);
            if (property == null) return;

            // Get the backing field for the init-only property
            // Init-only properties have a backing field named "<PropertyName>k__BackingField"
            var backingField = type.GetField($"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (backingField != null) backingField.SetValue(options, value);
        }
        catch
        {
            // Ignore parsing errors - keep default value
        }
    }
}