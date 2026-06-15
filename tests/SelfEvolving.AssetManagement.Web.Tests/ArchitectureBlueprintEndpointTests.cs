using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SelfEvolving.AssetManagement.Web.Tests;

public class ArchitectureBlueprintEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ArchitectureBlueprintEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task BlueprintEndpoint_ReturnsConfiguredArchitecture()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/system/blueprint");

        response.EnsureSuccessStatusCode();

        var blueprint = await response.Content.ReadFromJsonAsync<ArchitectureBlueprintResponse>();

        Assert.NotNull(blueprint);
        Assert.Equal("DevelApp.SelfEvolvingFramework", blueprint!.EvolutionFrameworkPackage);
        Assert.Equal("1.2.0", blueprint.EvolutionFrameworkVersion);
        Assert.Equal("PostgreSQL", blueprint.DatabaseProvider);
        Assert.True(blueprint.BlazorWebAssemblyEnabled);
        Assert.True(blueprint.BlazorServerEnabled);
        Assert.True(blueprint.HasDatabaseConnectionString);
        Assert.False(blueprint.MultiAgentEnabled);
        Assert.Equal(6, blueprint.MultiAgentMaxParallelAgents);
        Assert.Equal(5000, blueprint.MultiAgentRunTimeoutMs);
        Assert.Equal(0.6, blueprint.MultiAgentSafetyBlockThreshold);
        Assert.True(blueprint.MultiAgentRequireHumanApproval);
    }

    private sealed record ArchitectureBlueprintResponse(
        string EvolutionFrameworkPackage,
        string EvolutionFrameworkVersion,
        string DatabaseProvider,
        bool BlazorWebAssemblyEnabled,
        bool BlazorServerEnabled,
        bool HasDatabaseConnectionString,
        bool MultiAgentEnabled,
        int MultiAgentMaxParallelAgents,
        int MultiAgentRunTimeoutMs,
        double MultiAgentSafetyBlockThreshold,
        bool MultiAgentRequireHumanApproval);
}
