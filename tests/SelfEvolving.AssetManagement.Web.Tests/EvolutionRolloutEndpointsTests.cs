using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SelfEvolving.AssetManagement.Web.Tests;

public class EvolutionRolloutEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public EvolutionRolloutEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ActivateCandidate_WhenApproved_ReturnsOkAndStatusBecomesActive()
    {
        using var client = _factory.CreateClient();
        var candidateId = await CreateApprovedCandidateAsync(client);

        var activateResponse = await client.PostAsync($"/api/evolution/candidates/{candidateId}/activate", content: null);
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);

        var listResponse = await client.GetAsync("/api/evolution/candidates");
        listResponse.EnsureSuccessStatusCode();

        var candidates = await listResponse.Content.ReadFromJsonAsync<List<CandidateResponse>>();
        Assert.NotNull(candidates);
        Assert.Equal("Active", candidates!.Single(x => x.Id == candidateId).Status);
    }

    [Fact]
    public async Task ActivateCandidate_WhenNotApproved_ReturnsConflict()
    {
        using var client = _factory.CreateClient();
        var candidateId = await CreateCandidateAsync(client);

        var response = await client.PostAsync($"/api/evolution/candidates/{candidateId}/activate", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ActivateCandidate_WhenMissing_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/evolution/candidates/9999/activate", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<int> CreateApprovedCandidateAsync(HttpClient client)
    {
        var candidateId = await CreateCandidateAsync(client);
        var approvalResponse = await client.PostAsJsonAsync($"/api/evolution/candidates/{candidateId}/approvals", new
        {
            decision = "Approve",
            reviewerId = "reviewer-rollout"
        });

        approvalResponse.EnsureSuccessStatusCode();
        return candidateId;
    }

    private static async Task<int> CreateCandidateAsync(HttpClient client)
    {
        var feedbackResponse = await client.PostAsJsonAsync("/api/feedback", new
        {
            source = "Ops",
            subject = "Rollout stage",
            message = "Progress approved candidates to active rollout"
        });

        feedbackResponse.EnsureSuccessStatusCode();
        var feedback = await feedbackResponse.Content.ReadFromJsonAsync<FeedbackResponse>();
        Assert.NotNull(feedback);

        var candidateResponse = await client.PostAsync($"/api/evolution/candidates/from-feedback/{feedback!.Id}", content: null);
        candidateResponse.EnsureSuccessStatusCode();
        var candidate = await candidateResponse.Content.ReadFromJsonAsync<CandidateResponse>();
        Assert.NotNull(candidate);
        return candidate!.Id;
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
