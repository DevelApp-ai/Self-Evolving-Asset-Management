namespace SelfEvolving.AssetManagement.Web.Configuration;

public sealed class SystemArchitectureOptions
{
    public const string SectionName = "SystemArchitecture";

    public string EvolutionFrameworkPackage { get; set; } = "DevelApp.SelfEvolvingFramework";

    public string EvolutionFrameworkVersion { get; set; } = "1.3.0";

    public string DatabaseProvider { get; set; } = "PostgreSQL";

    public string DatabaseConnectionString { get; set; } = string.Empty;

    public bool BlazorWebAssemblyEnabled { get; set; } = true;

    public bool BlazorServerEnabled { get; set; } = true;

    public int EvolutionExecutionBudgetMilliseconds { get; set; } = 30000;

    public double EvolutionMinimumFitnessScore { get; set; } = 0.8;

    public bool MultiAgentEnabled { get; set; }

    public string MultiAgentSystemMode { get; set; } = "Cloud";

    public int MultiAgentMaxParallelAgents { get; set; } = 6;

    public int MultiAgentRunTimeoutMs { get; set; } = 5000;

    public double MultiAgentSafetyBlockThreshold { get; set; } = 0.6;

    public bool MultiAgentRequireHumanApproval { get; set; } = true;

    public string OpaPolicyBundlePath { get; set; } = "policies/asset-create-policy.json";

    public string OpaPolicyBundleVersion { get; set; } = "v1.0.0";
}
