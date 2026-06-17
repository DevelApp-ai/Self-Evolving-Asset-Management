using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace SelfEvolving.AssetManagement.Web.Tests;

public class EvolutionApprovalEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public EvolutionApprovalEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ApproveCandidate_WhenCandidateExists_ReturnsCreatedAndUpdatesCandidateStatus()
    {
        using var client = _factory.CreateClient();
        var candidate = await CreateCandidateAsync(client);
        var fitnessResponse = await client.PostAsJsonAsync($"/api/evolution/candidates/{candidate.Id}/fitness", new
        {
            score = 0.95,
            evaluatorId = "fitness-gate",
            notes = "meets approval gate"
        });
        fitnessResponse.EnsureSuccessStatusCode();

        var approvalResponse = await client.PostAsJsonAsync($"/api/evolution/candidates/{candidate.Id}/approvals", new
        {
            decision = "Approve",
            reviewerId = "approver-1",
            notes = "Safe to rollout"
        });

        Assert.Equal(HttpStatusCode.Created, approvalResponse.StatusCode);

        var approvalsListResponse = await client.GetAsync($"/api/evolution/candidates/{candidate.Id}/approvals");
        approvalsListResponse.EnsureSuccessStatusCode();

        var approvals = await approvalsListResponse.Content.ReadFromJsonAsync<List<ApprovalResponse>>();
        Assert.NotNull(approvals);
        Assert.Contains(approvals!, x => x.CandidateId == candidate.Id && x.Decision == "Approve");

        var candidatesResponse = await client.GetAsync("/api/evolution/candidates");
        candidatesResponse.EnsureSuccessStatusCode();

        var candidates = await candidatesResponse.Content.ReadFromJsonAsync<List<CandidateResponse>>();
        Assert.NotNull(candidates);
        Assert.Equal("Approved", candidates!.Single(x => x.Id == candidate.Id).Status);
    }

    [Fact]
    public async Task ApproveCandidate_WhenCandidateMissing_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/evolution/candidates/9999/approvals", new
        {
            decision = "Approve",
            reviewerId = "approver-1"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ApproveCandidate_WhenAlreadyReviewed_ReturnsConflict()
    {
        using var client = _factory.CreateClient();
        var candidate = await CreateCandidateAsync(client);

        var first = await client.PostAsJsonAsync($"/api/evolution/candidates/{candidate.Id}/approvals", new
        {
            decision = "Reject",
            reviewerId = "approver-1"
        });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync($"/api/evolution/candidates/{candidate.Id}/approvals", new
        {
            decision = "Approve",
            reviewerId = "approver-2"
        });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task ApproveCandidate_WhenDecisionInvalid_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();
        var candidate = await CreateCandidateAsync(client);

        var response = await client.PostAsJsonAsync($"/api/evolution/candidates/{candidate.Id}/approvals", new
        {
            decision = "Hold",
            reviewerId = "approver-1"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ApproveCandidate_WhenFitnessMissing_ReturnsConflict()
    {
        using var client = CreateMultiAgentDisabledClient();
        var candidate = await CreateCandidateAsync(client);

        var response = await client.PostAsJsonAsync($"/api/evolution/candidates/{candidate.Id}/approvals", new
        {
            decision = "Approve",
            reviewerId = "approver-1"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ApproveCandidate_WhenFitnessBelowThreshold_ReturnsConflict()
    {
        using var client = _factory.CreateClient();
        var candidate = await CreateCandidateAsync(client);
        var fitnessResponse = await client.PostAsJsonAsync($"/api/evolution/candidates/{candidate.Id}/fitness", new
        {
            score = 0.4,
            evaluatorId = "fitness-gate",
            notes = "below threshold"
        });
        fitnessResponse.EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync($"/api/evolution/candidates/{candidate.Id}/approvals", new
        {
            decision = "Approve",
            reviewerId = "approver-1"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RejectCandidate_WhenFitnessMissing_ReturnsCreatedAndUpdatesCandidateStatus()
    {
        using var client = _factory.CreateClient();
        var candidate = await CreateCandidateAsync(client);

        var response = await client.PostAsJsonAsync($"/api/evolution/candidates/{candidate.Id}/approvals", new
        {
            decision = "Reject",
            reviewerId = "approver-1",
            notes = "not fit for rollout"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var candidatesResponse = await client.GetAsync("/api/evolution/candidates");
        candidatesResponse.EnsureSuccessStatusCode();
        var candidates = await candidatesResponse.Content.ReadFromJsonAsync<List<CandidateResponse>>();
        Assert.NotNull(candidates);
        Assert.Equal("Rejected", candidates!.Single(x => x.Id == candidate.Id).Status);
    }

    private static async Task<CandidateResponse> CreateCandidateAsync(HttpClient client)
    {
        var feedbackResponse = await client.PostAsJsonAsync("/api/feedback", new
        {
            source = "Ops",
            subject = "Rollout review",
            message = "Need explicit approval before release"
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

    private HttpClient CreateMultiAgentDisabledClient()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SystemArchitecture:MultiAgentEnabled"] = "false",
                    ["SystemArchitecture:EvolutionFrameworkVersion"] = "1.3.0",
                    ["SystemArchitecture:MultiAgentSystemMode"] = "Cloud"
                });
            });
        });

        return factory.CreateClient();
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

    private sealed record ApprovalResponse(
        int Id,
        int CandidateId,
        string Decision,
        string ReviewerId,
        string? Notes,
        DateTime ReviewedUtc);
}
