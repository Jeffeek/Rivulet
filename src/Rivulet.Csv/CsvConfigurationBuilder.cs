using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace Rivulet.Csv;

/// <summary>
///     Builder for creating and configuring CsvHelper CsvConfiguration with ClassMap support.
///     Provides a fluent API for CSV configuration that works with both reading and writing operations.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
public sealed class CsvConfigurationBuilder
{
    private readonly List<Action<CsvContext>> _classMapActions = [];

    /// <summary>
    ///     Initializes a new instance of the <see cref="CsvConfigurationBuilder"/> class.
    /// </summary>
    /// <param name="culture">The culture to use for parsing/writing. If null, uses InvariantCulture.</param>
    public CsvConfigurationBuilder(CultureInfo? culture = null) => Configuration = new CsvConfiguration(culture ?? CultureInfo.InvariantCulture);

    /// <summary>
    ///     Gets the underlying CsvConfiguration for direct access to all CsvHelper settings.
    /// </summary>
    public CsvConfiguration Configuration { get; }

    /// <summary>
    ///     Registers a ClassMap type for strongly-typed CSV column mapping.
    /// </summary>
    /// <typeparam name="TMap">The ClassMap type to register.</typeparam>
    /// <returns>This builder instance for method chaining.</returns>
    public CsvConfigurationBuilder WithClassMap<TMap>()
        where TMap : ClassMap
    {
        _classMapActions.Add(static ctx => ctx.RegisterClassMap<TMap>());
        return this;
    }

    /// <summary>
    ///     Registers a ClassMap instance for strongly-typed CSV column mapping.
    /// </summary>
    /// <param name="classMap">The ClassMap instance to register.</param>
    /// <returns>This builder instance for method chaining.</returns>
    public CsvConfigurationBuilder WithClassMap(ClassMap classMap)
    {
        ArgumentNullException.ThrowIfNull(classMap);
        _classMapActions.Add(ctx => ctx.RegisterClassMap(classMap));
        return this;
    }

    /// <summary>
    ///     Configures additional CsvHelper settings using an action.
    /// </summary>
    /// <param name="configure">Action to configure the CsvConfiguration.</param>
    /// <returns>This builder instance for method chaining.</returns>
    public CsvConfigurationBuilder Configure(Action<CsvConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(Configuration);
        return this;
    }

    /// <summary>
    ///     Applies all registered ClassMaps to a CsvHelper context.
    /// </summary>
    /// <param name="csvContext">The CsvHelper context to apply ClassMaps to.</param>
    internal void ApplyClassMaps(CsvContext csvContext)
    {
        foreach (var action in _classMapActions)
            action(csvContext);
    }

    /// <summary>
    ///     Gets the built CsvConfiguration.
    /// </summary>
    public CsvConfiguration Build() => Configuration;

    /// <summary>
    ///     Creates a new builder from an existing CsvConfiguration.
    /// </summary>
    /// <param name="configuration">The existing configuration to wrap.</param>
    public static CsvConfigurationBuilder FromConfiguration(CsvConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var builder = new CsvConfigurationBuilder(configuration.CultureInfo)
        {
            Configuration =
            {
                // Copy common settings
                Delimiter = configuration.Delimiter,
                HasHeaderRecord = configuration.HasHeaderRecord,
                TrimOptions = configuration.TrimOptions
            }
        };
        return builder;
    }
}
