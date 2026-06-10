using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SelfEvolving.AssetManagement.Web.Tests;

public class FeedbackEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public FeedbackEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateAndListFeedback_ReturnsCreatedAndIncludesSubmittedFeedback()
    {
        using var client = _factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/feedback", new
        {
            source = "Ops",
            subject = "Ownership automation",
            message = "Suggest auto assignment for known categories"
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var listResponse = await client.GetAsync("/api/feedback");
        listResponse.EnsureSuccessStatusCode();

        var feedback = await listResponse.Content.ReadFromJsonAsync<List<FeedbackResponse>>();
        Assert.NotNull(feedback);
        Assert.Contains(feedback!, x => x.Subject == "Ownership automation" && x.Source == "Ops");
    }

    [Fact]
    public async Task CreateFeedback_WhenSourceMissing_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/feedback", new
        {
            source = "",
            subject = "Asset API",
            message = "Need pagination"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed record FeedbackResponse(
        int Id,
        string Source,
        string Subject,
        string Message,
        DateTime SubmittedUtc);
}
