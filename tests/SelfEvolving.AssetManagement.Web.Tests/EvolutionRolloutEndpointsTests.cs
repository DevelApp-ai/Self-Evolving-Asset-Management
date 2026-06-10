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
        var candidate = candidates!.Single(x => x.Id == candidateId);
        Assert.Equal("Active", candidate.Status);
        Assert.Equal("Internal", candidate.RolloutStage);
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

    [Fact]
    public async Task RollbackCandidate_WhenActive_ReturnsOkAndStatusBecomesRolledBack()
    {
        using var client = _factory.CreateClient();
        var candidateId = await CreateActiveCandidateAsync(client);

        var rollbackResponse = await client.PostAsync($"/api/evolution/candidates/{candidateId}/rollback", content: null);
        Assert.Equal(HttpStatusCode.OK, rollbackResponse.StatusCode);

        var listResponse = await client.GetAsync("/api/evolution/candidates");
        listResponse.EnsureSuccessStatusCode();

        var candidates = await listResponse.Content.ReadFromJsonAsync<List<CandidateResponse>>();
        Assert.NotNull(candidates);
        Assert.Equal("RolledBack", candidates!.Single(x => x.Id == candidateId).Status);
    }

    [Fact]
    public async Task RollbackCandidate_WhenNotActive_ReturnsConflict()
    {
        using var client = _factory.CreateClient();
        var candidateId = await CreateApprovedCandidateAsync(client);

        var response = await client.PostAsync($"/api/evolution/candidates/{candidateId}/rollback", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RollbackCandidate_WhenMissing_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/evolution/candidates/9999/rollback", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PromoteRollout_WhenActive_ReturnsOkAndStageAdvances()
    {
        using var client = _factory.CreateClient();
        var candidateId = await CreateActiveCandidateAsync(client);

        var promoteResponse = await client.PostAsync($"/api/evolution/candidates/{candidateId}/rollout/promote", content: null);
        Assert.Equal(HttpStatusCode.OK, promoteResponse.StatusCode);

        var listResponse = await client.GetAsync("/api/evolution/candidates");
        listResponse.EnsureSuccessStatusCode();

        var candidates = await listResponse.Content.ReadFromJsonAsync<List<CandidateResponse>>();
        Assert.NotNull(candidates);
        Assert.Equal("Pilot", candidates!.Single(x => x.Id == candidateId).RolloutStage);
    }

    [Fact]
    public async Task PromoteRollout_WhenNotActive_ReturnsConflict()
    {
        using var client = _factory.CreateClient();
        var candidateId = await CreateApprovedCandidateAsync(client);

        var response = await client.PostAsync($"/api/evolution/candidates/{candidateId}/rollout/promote", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PromoteRollout_WhenAlreadyFull_ReturnsConflict()
    {
        using var client = _factory.CreateClient();
        var candidateId = await CreateActiveCandidateAsync(client);

        var firstPromotion = await client.PostAsync($"/api/evolution/candidates/{candidateId}/rollout/promote", content: null);
        Assert.Equal(HttpStatusCode.OK, firstPromotion.StatusCode);
        var secondPromotion = await client.PostAsync($"/api/evolution/candidates/{candidateId}/rollout/promote", content: null);
        Assert.Equal(HttpStatusCode.OK, secondPromotion.StatusCode);

        var response = await client.PostAsync($"/api/evolution/candidates/{candidateId}/rollout/promote", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ReleaseCandidate_WhenActiveAndFull_ReturnsOkAndStatusBecomesReleased()
    {
        using var client = _factory.CreateClient();
        var candidateId = await CreateFullyPromotedActiveCandidateAsync(client);

        var releaseResponse = await client.PostAsync($"/api/evolution/candidates/{candidateId}/release", content: null);
        Assert.Equal(HttpStatusCode.OK, releaseResponse.StatusCode);

        var listResponse = await client.GetAsync("/api/evolution/candidates");
        listResponse.EnsureSuccessStatusCode();

        var candidates = await listResponse.Content.ReadFromJsonAsync<List<CandidateResponse>>();
        Assert.NotNull(candidates);
        var candidate = candidates!.Single(x => x.Id == candidateId);
        Assert.Equal("Released", candidate.Status);
        Assert.Equal("Full", candidate.RolloutStage);
    }

    [Fact]
    public async Task ReleaseCandidate_WhenNotFull_ReturnsConflict()
    {
        using var client = _factory.CreateClient();
        var candidateId = await CreateActiveCandidateAsync(client);

        var response = await client.PostAsync($"/api/evolution/candidates/{candidateId}/release", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ReleaseCandidate_WhenMissing_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/evolution/candidates/9999/release", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RegressionSignal_WhenActive_ReturnsOkAndStatusBecomesRolledBack()
    {
        using var client = _factory.CreateClient();
        var candidateId = await CreateActiveCandidateAsync(client);

        var response = await client.PostAsync($"/api/evolution/candidates/{candidateId}/regression-signal?reason=error-rate-spike", content: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var listResponse = await client.GetAsync("/api/evolution/candidates");
        listResponse.EnsureSuccessStatusCode();
        var candidates = await listResponse.Content.ReadFromJsonAsync<List<CandidateResponse>>();
        Assert.NotNull(candidates);
        Assert.Equal("RolledBack", candidates!.Single(x => x.Id == candidateId).Status);

        var eventsResponse = await client.GetAsync($"/api/evolution/candidates/{candidateId}/events");
        eventsResponse.EnsureSuccessStatusCode();
        var events = await eventsResponse.Content.ReadFromJsonAsync<List<LifecycleEventResponse>>();
        Assert.NotNull(events);
        Assert.Contains(events!, x => x.EventType == "AutoRolledBack" && x.Details == "error-rate-spike");
    }

    [Fact]
    public async Task RegressionSignal_WhenNotActive_ReturnsConflict()
    {
        using var client = _factory.CreateClient();
        var candidateId = await CreateApprovedCandidateAsync(client);

        var response = await client.PostAsync($"/api/evolution/candidates/{candidateId}/regression-signal", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RegressionSignal_WhenMissing_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/evolution/candidates/9999/regression-signal", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RetireCandidate_WhenReleased_ReturnsOkAndStatusBecomesRetired()
    {
        using var client = _factory.CreateClient();
        var candidateId = await CreateFullyPromotedActiveCandidateAsync(client);
        var releaseResponse = await client.PostAsync($"/api/evolution/candidates/{candidateId}/release", content: null);
        releaseResponse.EnsureSuccessStatusCode();

        var retireResponse = await client.PostAsync($"/api/evolution/candidates/{candidateId}/retire", content: null);
        Assert.Equal(HttpStatusCode.OK, retireResponse.StatusCode);

        var listResponse = await client.GetAsync("/api/evolution/candidates");
        listResponse.EnsureSuccessStatusCode();
        var candidates = await listResponse.Content.ReadFromJsonAsync<List<CandidateResponse>>();
        Assert.NotNull(candidates);
        Assert.Equal("Retired", candidates!.Single(x => x.Id == candidateId).Status);
    }

    [Fact]
    public async Task RetireCandidate_WhenRolledBack_ReturnsOkAndRecordsEvent()
    {
        using var client = _factory.CreateClient();
        var candidateId = await CreateActiveCandidateAsync(client);
        var rollbackResponse = await client.PostAsync($"/api/evolution/candidates/{candidateId}/rollback", content: null);
        rollbackResponse.EnsureSuccessStatusCode();

        var retireResponse = await client.PostAsync($"/api/evolution/candidates/{candidateId}/retire", content: null);
        Assert.Equal(HttpStatusCode.OK, retireResponse.StatusCode);

        var eventsResponse = await client.GetAsync($"/api/evolution/candidates/{candidateId}/events");
        eventsResponse.EnsureSuccessStatusCode();
        var events = await eventsResponse.Content.ReadFromJsonAsync<List<LifecycleEventResponse>>();
        Assert.NotNull(events);
        Assert.Contains(events!, x => x.EventType == "Retired" && x.Actor == "system");
    }

    [Fact]
    public async Task RetireCandidate_WhenStateIsInvalid_ReturnsConflict()
    {
        using var client = _factory.CreateClient();
        var candidateId = await CreateApprovedCandidateAsync(client);

        var response = await client.PostAsync($"/api/evolution/candidates/{candidateId}/retire", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RetireCandidate_WhenMissing_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/evolution/candidates/9999/retire", content: null);

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

    private static async Task<int> CreateActiveCandidateAsync(HttpClient client)
    {
        var candidateId = await CreateApprovedCandidateAsync(client);
        var activateResponse = await client.PostAsync($"/api/evolution/candidates/{candidateId}/activate", content: null);
        activateResponse.EnsureSuccessStatusCode();
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

    private static async Task<int> CreateFullyPromotedActiveCandidateAsync(HttpClient client)
    {
        var candidateId = await CreateActiveCandidateAsync(client);
        var firstPromotion = await client.PostAsync($"/api/evolution/candidates/{candidateId}/rollout/promote", content: null);
        firstPromotion.EnsureSuccessStatusCode();
        var secondPromotion = await client.PostAsync($"/api/evolution/candidates/{candidateId}/rollout/promote", content: null);
        secondPromotion.EnsureSuccessStatusCode();
        return candidateId;
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
        string? RolloutStage,
        DateTime CreatedUtc);

    private sealed record LifecycleEventResponse(
        int Id,
        int CandidateId,
        string EventType,
        string Actor,
        string? Details,
        DateTime OccurredUtc);
}
