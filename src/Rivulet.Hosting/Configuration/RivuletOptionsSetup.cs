using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Rivulet.Core;

namespace Rivulet.Hosting.Configuration;

/// <summary>
/// Configures ParallelOptionsRivulet from IConfiguration with proper type conversions.
/// Note: This class is provided for future customization of configuration binding.
/// Currently, the standard IConfiguration.Bind method handles all properties including nested objects.
/// </summary>
internal sealed class RivuletOptionsSetup(IConfiguration configuration) : IConfigureOptions<ParallelOptionsRivulet>
{
    private readonly IConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

    public void Configure(ParallelOptionsRivulet options)
    {
        var section = _configuration.GetSection("Rivulet");
        if (!section.Exists()) return;

        // The standard Bind method handles all properties including nested init-only properties
        // when used with the Options pattern during configuration binding
        section.Bind(options);
    }
}
