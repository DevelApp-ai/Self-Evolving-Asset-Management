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

    [Fact]
    public async Task CreateAsset_WhenPolicyDenied_ReturnsForbidden()
    {
        using var client = _factory.CreateClient();

        var deniedResponse = await client.PostAsJsonAsync("/api/assets", new
        {
            assetTag = "X-999",
            name = "Unmanaged",
            category = "Hardware"
        });

        Assert.Equal(HttpStatusCode.Forbidden, deniedResponse.StatusCode);
    }

    [Fact]
    public async Task AssignOwner_WhenAssetExists_ReturnsCreatedAndListIncludesActiveOwner()
    {
        using var client = _factory.CreateClient();
        var createAssetResponse = await client.PostAsJsonAsync("/api/assets", new
        {
            assetTag = "A-901",
            name = "Scanner",
            category = "Hardware"
        });

        var asset = await createAssetResponse.Content.ReadFromJsonAsync<AssetResponse>();
        Assert.NotNull(asset);

        var assignResponse = await client.PostAsJsonAsync($"/api/assets/{asset!.Id}/assignments", new
        {
            ownerId = "u-901",
            ownerName = "Ops Team"
        });

        Assert.Equal(HttpStatusCode.Created, assignResponse.StatusCode);

        var listResponse = await client.GetAsync($"/api/assets/{asset.Id}/assignments");
        listResponse.EnsureSuccessStatusCode();

        var assignments = await listResponse.Content.ReadFromJsonAsync<List<AssetAssignmentResponse>>();
        Assert.NotNull(assignments);
        Assert.Single(assignments!);
        Assert.True(assignments[0].IsActive);
        Assert.Equal("u-901", assignments[0].OwnerId);
    }

    [Fact]
    public async Task AssignOwner_WhenAssetDoesNotExist_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();

        var assignResponse = await client.PostAsJsonAsync("/api/assets/99999/assignments", new
        {
            ownerId = "u-x",
            ownerName = "Ghost Owner"
        });

        Assert.Equal(HttpStatusCode.NotFound, assignResponse.StatusCode);
    }

    private sealed record AssetResponse(
        int Id,
        string AssetTag,
        string Name,
        string Category,
        DateTime CreatedUtc);

    private sealed record AssetAssignmentResponse(
        int Id,
        int AssetId,
        string OwnerId,
        string OwnerName,
        DateTime AssignedUtc,
        bool IsActive);
}
