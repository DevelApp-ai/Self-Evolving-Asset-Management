using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SelfEvolving.AssetManagement.Web.Tests;

public class AssetEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AssetEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateAndFetchAsset_ReturnsCreatedAndThenAsset()
    {
        using var client = _factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/assets", new
        {
            assetTag = "A-200",
            name = "Workstation",
            category = "Hardware"
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createResponse.Headers.Location);

        var created = await createResponse.Content.ReadFromJsonAsync<AssetResponse>();
        Assert.NotNull(created);

        var getResponse = await client.GetAsync($"/api/assets/{created!.Id}");

        getResponse.EnsureSuccessStatusCode();
        var fetched = await getResponse.Content.ReadFromJsonAsync<AssetResponse>();

        Assert.NotNull(fetched);
        Assert.Equal("A-200", fetched!.AssetTag);
        Assert.Equal("Workstation", fetched.Name);
        Assert.Equal("Hardware", fetched.Category);
    }

    [Fact]
    public async Task CreateAsset_WhenDuplicateTag_ReturnsConflict()
    {
        using var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/assets", new
        {
            assetTag = "A-300",
            name = "Phone",
            category = "Devices"
        });

        var duplicateResponse = await client.PostAsJsonAsync("/api/assets", new
        {
            assetTag = "a-300",
            name = "Tablet",
            category = "Devices"
        });

        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
    }

    private sealed record AssetResponse(
        int Id,
        string AssetTag,
        string Name,
        string Category,
        DateTime CreatedUtc);
}
