using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Extensions.Options;
using SelfEvolving.AssetManagement.Web.Configuration;
using SelfEvolving.AssetManagement.Web.Models;

namespace SelfEvolving.AssetManagement.Web.Services;

public sealed class EvolutionOrchestrationService
{
    private static readonly string[] RolloutStages = ["Internal", "Pilot", "Full"];
    private readonly ConcurrentDictionary<int, EvolutionCandidateRecord> _candidatesById = new();
    private readonly ConcurrentDictionary<int, int> _candidateIdByFeedbackId = new();
    private readonly ConcurrentDictionary<int, EvolutionRunTelemetryRecord> _telemetryByCandidateId = new();
    private readonly ConcurrentDictionary<int, EvolutionFitnessEvaluationRecord> _fitnessByCandidateId = new();
    private readonly int _executionBudgetMilliseconds;
    private readonly double _minimumFitnessScore;
    private int _nextId;

    public EvolutionOrchestrationService(IOptions<SystemArchitectureOptions>? options = null)
    {
        _executionBudgetMilliseconds = options?.Value.EvolutionExecutionBudgetMilliseconds ?? 30000;
        _minimumFitnessScore = options?.Value.EvolutionMinimumFitnessScore ?? 0.8;
        if (_executionBudgetMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Evolution execution budget must be greater than zero.");
        }

        if (_minimumFitnessScore is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Evolution minimum fitness score must be between 0 and 1.");
        }
    }

    public IReadOnlyList<EvolutionCandidateRecord> GetAll() =>
        _candidatesById.Values
            .OrderByDescending(x => x.Id)
            .ToArray();

    public EvolutionCandidateRecord? GetById(int id) =>
        _candidatesById.TryGetValue(id, out var candidate) ? candidate : null;

    public EvolutionRunTelemetryRecord? GetTelemetry(int candidateId) =>
        _telemetryByCandidateId.TryGetValue(candidateId, out var telemetry) ? telemetry : null;

    public EvolutionFitnessEvaluationRecord? GetFitnessEvaluation(int candidateId) =>
        _fitnessByCandidateId.TryGetValue(candidateId, out var evaluation) ? evaluation : null;

    public EvolutionFitnessEvaluationRecord SetFitnessEvaluation(int candidateId, CreateEvolutionFitnessEvaluationRequest request)
    {
        if (!_candidatesById.ContainsKey(candidateId))
        {
            throw new InvalidOperationException($"Candidate '{candidateId}' was not found.");
        }

        var evaluatorId = request.EvaluatorId?.Trim();
        var notes = request.Notes?.Trim();

        if (string.IsNullOrWhiteSpace(evaluatorId))
        {
            throw new ArgumentException("EvaluatorId is required.");
        }

        if (request.Score is < 0 or > 1)
        {
            throw new ArgumentException("Score must be between 0 and 1.");
        }

        var evaluation = new EvolutionFitnessEvaluationRecord(
            CandidateId: candidateId,
            Score: request.Score,
            EvaluatorId: evaluatorId,
            Notes: string.IsNullOrWhiteSpace(notes) ? null : notes,
            EvaluatedUtc: DateTime.UtcNow);

        _fitnessByCandidateId[candidateId] = evaluation;
        return evaluation;
    }

    public EvolutionCandidateRecord CreateFromFeedback(FeedbackRecord feedback)
    {
        var startedUtc = DateTime.UtcNow;
        var id = Interlocked.Increment(ref _nextId);
        var title = $"Improve: {feedback.Subject}";
        var summary = feedback.Message;

        if (!_candidateIdByFeedbackId.TryAdd(feedback.Id, id))
        {
            throw new InvalidOperationException($"Feedback '{feedback.Id}' already has a generated candidate.");
        }

        var created = new EvolutionCandidateRecord(
            id,
            feedback.Id,
            title,
            summary,
            Status: "Proposed",
            RolloutStage: null,
            DateTime.UtcNow);

        _candidatesById[id] = created;
        _telemetryByCandidateId[id] = new EvolutionRunTelemetryRecord(
            CandidateId: id,
            TotalDurationMilliseconds: (DateTime.UtcNow - startedUtc).TotalMilliseconds,
            MutationDurationMilliseconds: 0,
            SecurityEvaluationDurationMilliseconds: 0,
            CompilationDurationMilliseconds: 0,
            FitnessEvaluationDurationMilliseconds: 0,
            DiagnosticCount: 0,
            CanceledByCaller: false,
            TimedOut: false,
            ExecutionBudgetMilliseconds: _executionBudgetMilliseconds,
            RecordedUtc: DateTime.UtcNow);
        return created;
    }

    public EvolutionCandidateRecord UpdateStatus(int id, string status)
    {
        if (!_candidatesById.TryGetValue(id, out var candidate))
        {
            throw new InvalidOperationException($"Candidate '{id}' was not found.");
        }

        var updated = candidate with { Status = status };
        _candidatesById[id] = updated;
        return updated;
    }

    public EvolutionCandidateRecord Activate(int id)
    {
        var candidate = GetById(id) ?? throw new InvalidOperationException($"Candidate '{id}' was not found.");
        if (candidate.Status == "Active")
        {
            throw new InvalidOperationException($"Candidate '{id}' is already active.");
        }

        if (candidate.Status != "Approved")
        {
            throw new InvalidOperationException($"Candidate '{id}' must be approved before activation.");
        }

        if (!MeetsFitnessGate(id))
        {
            throw new InvalidOperationException($"Candidate '{id}' does not meet the minimum fitness score gate.");
        }

        var updated = candidate with { Status = "Active", RolloutStage = RolloutStages[0] };
        _candidatesById[id] = updated;
        return updated;
    }

    public EvolutionCandidateRecord Rollback(int id)
    {
        var candidate = GetById(id) ?? throw new InvalidOperationException($"Candidate '{id}' was not found.");
        if (candidate.Status != "Active")
        {
            throw new InvalidOperationException($"Candidate '{id}' must be active before rollback.");
        }

        return UpdateStatus(id, "RolledBack");
    }

    public EvolutionCandidateRecord AutoRollbackOnRegression(int id) => Rollback(id);

    public EvolutionCandidateRecord PromoteRollout(int id)
    {
        var candidate = GetById(id) ?? throw new InvalidOperationException($"Candidate '{id}' was not found.");
        if (candidate.Status != "Active")
        {
            throw new InvalidOperationException($"Candidate '{id}' must be active before rollout promotion.");
        }

        if (!MeetsFitnessGate(id))
        {
            throw new InvalidOperationException($"Candidate '{id}' does not meet the minimum fitness score gate.");
        }

        var currentStage = candidate.RolloutStage ?? RolloutStages[0];
        var currentIndex = Array.IndexOf(RolloutStages, currentStage);
        if (currentIndex < 0)
        {
            throw new InvalidOperationException($"Candidate '{id}' has an unknown rollout stage '{currentStage}'.");
        }

        if (currentIndex >= RolloutStages.Length - 1)
        {
            throw new InvalidOperationException($"Candidate '{id}' is already at the final rollout stage.");
        }

        var nextStage = RolloutStages[currentIndex + 1];
        var updated = candidate with { RolloutStage = nextStage };
        _candidatesById[id] = updated;
        return updated;
    }

    public EvolutionCandidateRecord Release(int id)
    {
        var candidate = GetById(id) ?? throw new InvalidOperationException($"Candidate '{id}' was not found.");
        if (candidate.Status != "Active")
        {
            throw new InvalidOperationException($"Candidate '{id}' must be active before release.");
        }

        if (candidate.RolloutStage != RolloutStages[^1])
        {
            throw new InvalidOperationException($"Candidate '{id}' must be promoted to '{RolloutStages[^1]}' before release.");
        }

        if (!MeetsFitnessGate(id))
        {
            throw new InvalidOperationException($"Candidate '{id}' does not meet the minimum fitness score gate.");
        }

        var updated = candidate with { Status = "Released" };
        _candidatesById[id] = updated;
        return updated;
    }

    public EvolutionCandidateRecord Retire(int id)
    {
        var candidate = GetById(id) ?? throw new InvalidOperationException($"Candidate '{id}' was not found.");
        if (candidate.Status is not ("Released" or "RolledBack"))
        {
            throw new InvalidOperationException($"Candidate '{id}' must be released or rolled back before retirement.");
        }

        var updated = candidate with { Status = "Retired" };
        _candidatesById[id] = updated;
        return updated;
    }

    private bool MeetsFitnessGate(int candidateId) =>
        _fitnessByCandidateId.TryGetValue(candidateId, out var evaluation) &&
        evaluation.Score >= _minimumFitnessScore;
}
