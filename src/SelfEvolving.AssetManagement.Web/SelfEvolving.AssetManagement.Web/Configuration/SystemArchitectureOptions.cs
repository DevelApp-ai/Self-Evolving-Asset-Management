namespace SelfEvolving.AssetManagement.Web.Configuration;

public sealed class SystemArchitectureOptions
{
    public const string SectionName = "SystemArchitecture";

    public string EvolutionFrameworkPackage { get; set; } = "DevelApp.SelfEvolvingFramework";

    public string DatabaseProvider { get; set; } = "PostgreSQL";

    public string DatabaseConnectionString { get; set; } = string.Empty;

    public bool BlazorWebAssemblyEnabled { get; set; } = true;

    public bool BlazorServerEnabled { get; set; } = true;
}
