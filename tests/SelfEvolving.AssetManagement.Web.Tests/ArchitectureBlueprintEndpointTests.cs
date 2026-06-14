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
        Assert.Equal("1.0.1", blueprint.EvolutionFrameworkVersion);
        Assert.Equal("PostgreSQL", blueprint.DatabaseProvider);
        Assert.True(blueprint.BlazorWebAssemblyEnabled);
        Assert.True(blueprint.BlazorServerEnabled);
        Assert.True(blueprint.HasDatabaseConnectionString);
    }

    private sealed record ArchitectureBlueprintResponse(
        string EvolutionFrameworkPackage,
        string EvolutionFrameworkVersion,
        string DatabaseProvider,
        bool BlazorWebAssemblyEnabled,
        bool BlazorServerEnabled,
        bool HasDatabaseConnectionString);
}
