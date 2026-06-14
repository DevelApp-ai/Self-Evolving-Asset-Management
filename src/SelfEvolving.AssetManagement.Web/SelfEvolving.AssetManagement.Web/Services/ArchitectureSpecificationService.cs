using SelfEvolving.AssetManagement.Web.Configuration;
using Microsoft.Extensions.Options;

namespace SelfEvolving.AssetManagement.Web.Services;

public sealed class ArchitectureSpecificationService(IOptions<SystemArchitectureOptions> options)
{
    private readonly SystemArchitectureOptions _options = options.Value;

    public ArchitectureBlueprint GetBlueprint()
    {
        if (!string.Equals(_options.DatabaseProvider, "PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("SystemArchitecture:DatabaseProvider must be PostgreSQL.");
        }

        if (!string.Equals(_options.EvolutionFrameworkPackage, "DevelApp.SelfEvolvingFramework", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("SystemArchitecture:EvolutionFrameworkPackage must be DevelApp.SelfEvolvingFramework.");
        }

        if (!string.Equals(_options.EvolutionFrameworkVersion, "1.0.1", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("SystemArchitecture:EvolutionFrameworkVersion must be 1.0.1.");
        }

        return new ArchitectureBlueprint(
            _options.EvolutionFrameworkPackage,
            _options.EvolutionFrameworkVersion,
            _options.DatabaseProvider,
            _options.BlazorWebAssemblyEnabled,
            _options.BlazorServerEnabled,
            !string.IsNullOrWhiteSpace(_options.DatabaseConnectionString));
    }
}

public sealed record ArchitectureBlueprint(
    string EvolutionFrameworkPackage,
    string EvolutionFrameworkVersion,
    string DatabaseProvider,
    bool BlazorWebAssemblyEnabled,
    bool BlazorServerEnabled,
    bool HasDatabaseConnectionString);
