using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SelfEvolving.AssetManagement.Web.Tests;

public class EvolutionLifecycleEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public EvolutionLifecycleEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetCandidateEvents_WhenLifecycleChangesOccur_ReturnsRecordedEvents()
    {
        using var client = _factory.CreateClient();
        var candidate = await CreateCandidateAsync(client);

        var approvalResponse = await client.PostAsJsonAsync($"/api/evolution/candidates/{candidate.Id}/approvals", new
        {
            decision = "Approve",
            reviewerId = "reviewer-events",
            notes = "Ready"
        });
        approvalResponse.EnsureSuccessStatusCode();

        var activateResponse = await client.PostAsync($"/api/evolution/candidates/{candidate.Id}/activate", content: null);
        activateResponse.EnsureSuccessStatusCode();

        var listResponse = await client.GetAsync($"/api/evolution/candidates/{candidate.Id}/events");
        listResponse.EnsureSuccessStatusCode();

        var events = await listResponse.Content.ReadFromJsonAsync<List<LifecycleEventResponse>>();
        Assert.NotNull(events);
        Assert.Contains(events!, x => x.EventType == "Approved" && x.Actor == "reviewer-events");
        Assert.Contains(events!, x => x.EventType == "Activated" && x.Actor == "system");
    }

    [Fact]
    public async Task GetCandidateEvents_WhenCandidateMissing_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/evolution/candidates/9999/events");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<CandidateResponse> CreateCandidateAsync(HttpClient client)
    {
        var feedbackResponse = await client.PostAsJsonAsync("/api/feedback", new
        {
            source = "Ops",
            subject = "Events",
            message = "Track lifecycle transitions"
        });
        feedbackResponse.EnsureSuccessStatusCode();
        var feedback = await feedbackResponse.Content.ReadFromJsonAsync<FeedbackResponse>();
        Assert.NotNull(feedback);

        var candidateResponse = await client.PostAsync($"/api/evolution/candidates/from-feedback/{feedback!.Id}", content: null);
        candidateResponse.EnsureSuccessStatusCode();
        var candidate = await candidateResponse.Content.ReadFromJsonAsync<CandidateResponse>();
        Assert.NotNull(candidate);
        return candidate!;
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

    private sealed record LifecycleEventResponse(
        int Id,
        int CandidateId,
        string EventType,
        string Actor,
        string? Details,
        DateTime OccurredUtc);
}
