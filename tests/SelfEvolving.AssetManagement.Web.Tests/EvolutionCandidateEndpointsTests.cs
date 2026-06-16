using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

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

    [Fact]
    public async Task GetCandidateById_WhenExists_ReturnsOk()
    {
        using var client = _factory.CreateClient();
        var feedbackResponse = await client.PostAsJsonAsync("/api/feedback", new
        {
            source = "UX",
            subject = "Single candidate lookup",
            message = "Need direct candidate endpoint"
        });

        var feedback = await feedbackResponse.Content.ReadFromJsonAsync<FeedbackResponse>();
        Assert.NotNull(feedback);

        var createResponse = await client.PostAsync($"/api/evolution/candidates/from-feedback/{feedback!.Id}", content: null);
        var created = await createResponse.Content.ReadFromJsonAsync<CandidateResponse>();
        Assert.NotNull(created);

        var getResponse = await client.GetAsync($"/api/evolution/candidates/{created!.Id}");
        getResponse.EnsureSuccessStatusCode();

        var fetched = await getResponse.Content.ReadFromJsonAsync<CandidateResponse>();
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched!.Id);
        Assert.Equal("Proposed", fetched.Status);
    }

    [Fact]
    public async Task GetCandidateById_WhenMissing_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/evolution/candidates/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCandidateTelemetry_WhenCandidateExists_ReturnsTelemetry()
    {
        using var client = _factory.CreateClient();
        var feedbackResponse = await client.PostAsJsonAsync("/api/feedback", new
        {
            source = "Ops",
            subject = "Telemetry request",
            message = "Capture orchestration telemetry"
        });

        var feedback = await feedbackResponse.Content.ReadFromJsonAsync<FeedbackResponse>();
        Assert.NotNull(feedback);

        var createResponse = await client.PostAsync($"/api/evolution/candidates/from-feedback/{feedback!.Id}", content: null);
        var created = await createResponse.Content.ReadFromJsonAsync<CandidateResponse>();
        Assert.NotNull(created);

        var telemetryResponse = await client.GetAsync($"/api/evolution/candidates/{created!.Id}/telemetry");
        telemetryResponse.EnsureSuccessStatusCode();

        var telemetry = await telemetryResponse.Content.ReadFromJsonAsync<EvolutionTelemetryResponse>();
        Assert.NotNull(telemetry);
        Assert.Equal(created.Id, telemetry!.CandidateId);
        Assert.Equal(30000, telemetry.ExecutionBudgetMilliseconds);
        Assert.False(telemetry.TimedOut);
    }

    [Fact]
    public async Task GetCandidateTelemetry_WhenCandidateMissing_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/evolution/candidates/9999/telemetry");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RecordAndGetCandidateFitness_WhenCandidateExists_ReturnsCreatedThenOk()
    {
        using var client = _factory.CreateClient();
        var feedbackResponse = await client.PostAsJsonAsync("/api/feedback", new
        {
            source = "Ops",
            subject = "Fitness request",
            message = "Track candidate quality gates"
        });

        var feedback = await feedbackResponse.Content.ReadFromJsonAsync<FeedbackResponse>();
        Assert.NotNull(feedback);

        var createResponse = await client.PostAsync($"/api/evolution/candidates/from-feedback/{feedback!.Id}", content: null);
        var created = await createResponse.Content.ReadFromJsonAsync<CandidateResponse>();
        Assert.NotNull(created);

        var recordResponse = await client.PostAsJsonAsync($"/api/evolution/candidates/{created!.Id}/fitness", new
        {
            score = 0.92,
            evaluatorId = "fitness-bot",
            notes = "strong execution flow"
        });
        Assert.Equal(HttpStatusCode.Created, recordResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/evolution/candidates/{created.Id}/fitness");
        getResponse.EnsureSuccessStatusCode();

        var fitness = await getResponse.Content.ReadFromJsonAsync<EvolutionFitnessResponse>();
        Assert.NotNull(fitness);
        Assert.Equal(created.Id, fitness!.CandidateId);
        Assert.Equal(0.92, fitness.Score);
        Assert.Equal("fitness-bot", fitness.EvaluatorId);
    }

    [Fact]
    public async Task GetCandidateFitness_WhenMissing_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/evolution/candidates/9999/fitness");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GenerateFromFeedbackMultiAgent_WhenDisabled_ReturnsConflict()
    {
        using var client = _factory.CreateClient();
        var feedbackResponse = await client.PostAsJsonAsync("/api/feedback", new
        {
            source = "Ops",
            subject = "Multi-agent",
            message = "Enable coordinated multi-agent flow"
        });
        feedbackResponse.EnsureSuccessStatusCode();
        var feedback = await feedbackResponse.Content.ReadFromJsonAsync<FeedbackResponse>();
        Assert.NotNull(feedback);

        var response = await client.PostAsync($"/api/evolution/candidates/from-feedback/{feedback!.Id}/multi-agent", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GenerateFromFeedbackMultiAgent_WhenEnabled_ReturnsCreatedAndRunDetails()
    {
        using var client = CreateMultiAgentEnabledClient();
        var feedbackResponse = await client.PostAsJsonAsync("/api/feedback", new
        {
            source = "UX",
            subject = "Multi-agent candidate",
            message = "Improve ranking and filtering relevance"
        });
        feedbackResponse.EnsureSuccessStatusCode();
        var feedback = await feedbackResponse.Content.ReadFromJsonAsync<FeedbackResponse>();
        Assert.NotNull(feedback);

        var createResponse = await client.PostAsync($"/api/evolution/candidates/from-feedback/{feedback!.Id}/multi-agent", content: null);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var candidate = await createResponse.Content.ReadFromJsonAsync<CandidateResponse>();
        Assert.NotNull(candidate);
        Assert.Contains("mode:local", candidate!.Summary);

        var runsResponse = await client.GetAsync($"/api/evolution/candidates/{candidate!.Id}/agent-runs");
        runsResponse.EnsureSuccessStatusCode();
        var runs = await runsResponse.Content.ReadFromJsonAsync<List<AgentRunResponse>>();
        Assert.NotNull(runs);
        Assert.Single(runs!);
        Assert.Equal(candidate.Id, runs[0].CandidateId);
        Assert.Equal("Completed", runs[0].Status);

        var runResponse = await client.GetAsync($"/api/evolution/agent-runs/{runs[0].Id}");
        runResponse.EnsureSuccessStatusCode();
        var run = await runResponse.Content.ReadFromJsonAsync<AgentRunResponse>();
        Assert.NotNull(run);
        Assert.Equal(runs[0].Id, run!.Id);

        var stepsResponse = await client.GetAsync($"/api/evolution/agent-runs/{runs[0].Id}/steps");
        stepsResponse.EnsureSuccessStatusCode();
        var steps = await stepsResponse.Content.ReadFromJsonAsync<List<AgentStepResponse>>();
        Assert.NotNull(steps);
        Assert.True(steps!.Count >= 5);
    }

    private HttpClient CreateMultiAgentEnabledClient()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SystemArchitecture:MultiAgentEnabled"] = "true",
                    ["SystemArchitecture:EvolutionFrameworkVersion"] = "1.3.0",
                    ["SystemArchitecture:MultiAgentSystemMode"] = "Local"
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

    private sealed record EvolutionTelemetryResponse(
        int CandidateId,
        double TotalDurationMilliseconds,
        int DiagnosticCount,
        bool CanceledByCaller,
        bool TimedOut,
        int ExecutionBudgetMilliseconds,
        DateTime RecordedUtc);

    private sealed record EvolutionFitnessResponse(
        int CandidateId,
        double Score,
        string EvaluatorId,
        string? Notes,
        DateTime EvaluatedUtc);

    private sealed record AgentRunResponse(
        int Id,
        int CandidateId,
        int SourceFeedbackId,
        string Status,
        string FrameworkVersion,
        DateTime StartedUtc,
        DateTime? CompletedUtc);

    private sealed record AgentStepResponse(
        int Id,
        int RunId,
        string AgentType,
        string InputHash,
        string OutputSummary,
        int LatencyMilliseconds,
        int TokenCost,
        int DiagnosticCount,
        DateTime RecordedUtc);
}
