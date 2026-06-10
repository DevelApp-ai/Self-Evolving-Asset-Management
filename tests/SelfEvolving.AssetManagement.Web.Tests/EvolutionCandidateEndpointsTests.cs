using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SelfEvolving.AssetManagement.Web.Tests;

public class EvolutionCandidateEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public EvolutionCandidateEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GenerateFromFeedback_WhenFeedbackExists_ReturnsCreatedAndListIncludesCandidate()
    {
        using var client = _factory.CreateClient();
        var feedbackResponse = await client.PostAsJsonAsync("/api/feedback", new
        {
            source = "Ops",
            subject = "Asset ownership",
            message = "Auto-assign known device owners"
        });

        var feedback = await feedbackResponse.Content.ReadFromJsonAsync<FeedbackResponse>();
        Assert.NotNull(feedback);

        var generateResponse = await client.PostAsync($"/api/evolution/candidates/from-feedback/{feedback!.Id}", content: null);
        Assert.Equal(HttpStatusCode.Created, generateResponse.StatusCode);

        var listResponse = await client.GetAsync("/api/evolution/candidates");
        listResponse.EnsureSuccessStatusCode();

        var candidates = await listResponse.Content.ReadFromJsonAsync<List<CandidateResponse>>();
        Assert.NotNull(candidates);
        Assert.Contains(candidates!, x => x.SourceFeedbackId == feedback.Id && x.Status == "Proposed");
    }

    [Fact]
    public async Task GenerateFromFeedback_WhenFeedbackMissing_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/evolution/candidates/from-feedback/9999", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GenerateFromFeedback_WhenAlreadyGenerated_ReturnsConflict()
    {
        using var client = _factory.CreateClient();
        var feedbackResponse = await client.PostAsJsonAsync("/api/feedback", new
        {
            source = "UI",
            subject = "Search ranking",
            message = "Prefer recent assets in results"
        });

        var feedback = await feedbackResponse.Content.ReadFromJsonAsync<FeedbackResponse>();
        Assert.NotNull(feedback);

        var first = await client.PostAsync($"/api/evolution/candidates/from-feedback/{feedback!.Id}", content: null);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsync($"/api/evolution/candidates/from-feedback/{feedback.Id}", content: null);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    private sealed record FeedbackResponse(
        int Id,
        string Source,
        string Subject,
        string Message,
        DateTime SubmittedUtc);

    private sealed record CandidateResponse(
        int Id,
        int SourceFeedbackId,
        string Title,
        string Summary,
        string Status,
        DateTime CreatedUtc);
}
