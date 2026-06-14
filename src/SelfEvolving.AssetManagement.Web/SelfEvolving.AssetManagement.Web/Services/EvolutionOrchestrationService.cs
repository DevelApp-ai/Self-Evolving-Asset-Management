using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SelfEvolving.AssetManagement.Web.Configuration;
using SelfEvolving.AssetManagement.Web.Data;
using SelfEvolving.AssetManagement.Web.Models;

namespace SelfEvolving.AssetManagement.Web.Services;

public sealed class EvolutionOrchestrationService
{
    private static readonly string[] RolloutStages = ["Internal", "Pilot", "Full"];
    private readonly AssetManagementDbContext? _dbContext;
    private readonly ConcurrentDictionary<int, EvolutionCandidateRecord> _candidatesById = new();
    private readonly ConcurrentDictionary<int, int> _candidateIdByFeedbackId = new();
    private readonly ConcurrentDictionary<int, EvolutionRunTelemetryRecord> _telemetryByCandidateId = new();
    private readonly ConcurrentDictionary<int, EvolutionFitnessEvaluationRecord> _fitnessByCandidateId = new();
    private readonly int _executionBudgetMilliseconds;
    private readonly double _minimumFitnessScore;
    private int _nextId;

    public EvolutionOrchestrationService(IOptions<SystemArchitectureOptions>? options = null, AssetManagementDbContext? dbContext = null)
    {
        _dbContext = dbContext;
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
        _dbContext is null
            ? _candidatesById.Values.OrderByDescending(x => x.Id).ToArray()
            : _dbContext.EvolutionCandidates.AsNoTracking()
                .OrderByDescending(x => x.Id)
                .Select(x => MapCandidate(x))
                .ToArray();

    public EvolutionCandidateRecord? GetById(int id) =>
        _dbContext is null
            ? (_candidatesById.TryGetValue(id, out var candidate) ? candidate : null)
            : _dbContext.EvolutionCandidates.AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => MapCandidate(x))
                .SingleOrDefault();

    public EvolutionRunTelemetryRecord? GetTelemetry(int candidateId) =>
        _dbContext is null
            ? (_telemetryByCandidateId.TryGetValue(candidateId, out var telemetry) ? telemetry : null)
            : _dbContext.EvolutionTelemetry.AsNoTracking()
                .Where(x => x.CandidateId == candidateId)
                .Select(x => MapTelemetry(x))
                .SingleOrDefault();

    public EvolutionFitnessEvaluationRecord? GetFitnessEvaluation(int candidateId) =>
        _dbContext is null
            ? (_fitnessByCandidateId.TryGetValue(candidateId, out var evaluation) ? evaluation : null)
            : _dbContext.EvolutionFitness.AsNoTracking()
                .Where(x => x.CandidateId == candidateId)
                .Select(x => MapFitness(x))
                .SingleOrDefault();

    public bool MeetsMinimumFitnessGate(int candidateId) => MeetsFitnessGate(candidateId);

    public EvolutionFitnessEvaluationRecord SetFitnessEvaluation(int candidateId, CreateEvolutionFitnessEvaluationRequest request)
    {
        if (GetById(candidateId) is null)
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

        if (_dbContext is null)
        {
            _fitnessByCandidateId[candidateId] = evaluation;
            return evaluation;
        }

        var existing = _dbContext.EvolutionFitness.SingleOrDefault(x => x.CandidateId == candidateId);
        if (existing is null)
        {
            existing = new EvolutionFitnessEntity { CandidateId = candidateId };
            _dbContext.EvolutionFitness.Add(existing);
        }

        existing.Score = evaluation.Score;
        existing.EvaluatorId = evaluation.EvaluatorId;
        existing.Notes = evaluation.Notes;
        existing.EvaluatedUtc = evaluation.EvaluatedUtc;
        _dbContext.SaveChanges();

        return evaluation;
    }

    public EvolutionCandidateRecord CreateFromFeedback(FeedbackRecord feedback)
    {
        var startedUtc = DateTime.UtcNow;
        var mutationTimer = Stopwatch.StartNew();
        var synthesis = SynthesizeCandidate(feedback);
        mutationTimer.Stop();

        var securityTimer = Stopwatch.StartNew();
        var diagnostics = string.IsNullOrWhiteSpace(feedback.Message) ? 1 : 0;
        securityTimer.Stop();

        var compilationTimer = Stopwatch.StartNew();
        var summary = synthesis.Summary.Trim();
        compilationTimer.Stop();

        if (_dbContext is not null)
        {
            var duplicate = _dbContext.EvolutionCandidates.Any(x => x.SourceFeedbackId == feedback.Id);
            if (duplicate)
            {
                throw new InvalidOperationException($"Feedback '{feedback.Id}' already has a generated candidate.");
            }

            var entity = new EvolutionCandidateEntity
            {
                SourceFeedbackId = feedback.Id,
                Title = synthesis.Title,
                Summary = summary,
                Status = "Proposed",
                RolloutStage = null,
                CreatedUtc = DateTime.UtcNow
            };

            _dbContext.EvolutionCandidates.Add(entity);
            _dbContext.SaveChanges();

            var telemetryEntity = new EvolutionTelemetryEntity
            {
                CandidateId = entity.Id,
                TotalDurationMilliseconds = Math.Max(1, (DateTime.UtcNow - startedUtc).TotalMilliseconds),
                MutationDurationMilliseconds = Math.Max(1, mutationTimer.Elapsed.TotalMilliseconds),
                SecurityEvaluationDurationMilliseconds = Math.Max(1, securityTimer.Elapsed.TotalMilliseconds),
                CompilationDurationMilliseconds = Math.Max(1, compilationTimer.Elapsed.TotalMilliseconds),
                FitnessEvaluationDurationMilliseconds = 0,
                DiagnosticCount = diagnostics,
                CanceledByCaller = false,
                TimedOut = false,
                ExecutionBudgetMilliseconds = _executionBudgetMilliseconds,
                RecordedUtc = DateTime.UtcNow
            };

            _dbContext.EvolutionTelemetry.Add(telemetryEntity);
            _dbContext.SaveChanges();
            return MapCandidate(entity);
        }

        var id = Interlocked.Increment(ref _nextId);
        if (!_candidateIdByFeedbackId.TryAdd(feedback.Id, id))
        {
            throw new InvalidOperationException($"Feedback '{feedback.Id}' already has a generated candidate.");
        }

        var created = new EvolutionCandidateRecord(
            id,
            feedback.Id,
            synthesis.Title,
            summary,
            Status: "Proposed",
            RolloutStage: null,
            DateTime.UtcNow);

        _candidatesById[id] = created;
        _telemetryByCandidateId[id] = new EvolutionRunTelemetryRecord(
            CandidateId: id,
            TotalDurationMilliseconds: Math.Max(1, (DateTime.UtcNow - startedUtc).TotalMilliseconds),
            MutationDurationMilliseconds: Math.Max(1, mutationTimer.Elapsed.TotalMilliseconds),
            SecurityEvaluationDurationMilliseconds: Math.Max(1, securityTimer.Elapsed.TotalMilliseconds),
            CompilationDurationMilliseconds: Math.Max(1, compilationTimer.Elapsed.TotalMilliseconds),
            FitnessEvaluationDurationMilliseconds: 0,
            DiagnosticCount: diagnostics,
            CanceledByCaller: false,
            TimedOut: false,
            ExecutionBudgetMilliseconds: _executionBudgetMilliseconds,
            RecordedUtc: DateTime.UtcNow);
        return created;
    }

    public EvolutionCandidateRecord UpdateStatus(int id, string status)
    {
        if (_dbContext is null)
        {
            if (!_candidatesById.TryGetValue(id, out var candidate))
            {
                throw new InvalidOperationException($"Candidate '{id}' was not found.");
            }

            var updated = candidate with { Status = status };
            _candidatesById[id] = updated;
            return updated;
        }

        var entity = _dbContext.EvolutionCandidates.SingleOrDefault(x => x.Id == id)
            ?? throw new InvalidOperationException($"Candidate '{id}' was not found.");
        entity.Status = status;
        _dbContext.SaveChanges();
        return MapCandidate(entity);
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
        PersistCandidate(updated);
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
        PersistCandidate(updated);
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
        PersistCandidate(updated);
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
        PersistCandidate(updated);
        return updated;
    }

    private bool MeetsFitnessGate(int candidateId) =>
        GetFitnessEvaluation(candidateId) is { } evaluation &&
        evaluation.Score >= _minimumFitnessScore;

    private (string Title, string Summary) SynthesizeCandidate(FeedbackRecord feedback)
    {
        var previousTitle = GetAll().FirstOrDefault()?.Title;
        var baseTitle = $"Improve: {feedback.Subject}";
        var crossoverTitle = string.IsNullOrWhiteSpace(previousTitle)
            ? baseTitle
            : $"{baseTitle} + {ExtractSuffix(previousTitle)}";
        var evolvedSummary = $"{feedback.Message.Trim()} [mutated:v1.1-crossover]";
        return (crossoverTitle, evolvedSummary);
    }

    private static string ExtractSuffix(string title)
    {
        var pieces = title.Split(':', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return pieces.Length == 2 ? pieces[1] : title;
    }

    private void PersistCandidate(EvolutionCandidateRecord updated)
    {
        if (_dbContext is null)
        {
            _candidatesById[updated.Id] = updated;
            return;
        }

        var entity = _dbContext.EvolutionCandidates.Single(x => x.Id == updated.Id);
        entity.Title = updated.Title;
        entity.Summary = updated.Summary;
        entity.Status = updated.Status;
        entity.RolloutStage = updated.RolloutStage;
        _dbContext.SaveChanges();
    }

    private static EvolutionCandidateRecord MapCandidate(EvolutionCandidateEntity entity) =>
        new(
            entity.Id,
            entity.SourceFeedbackId,
            entity.Title,
            entity.Summary,
            entity.Status,
            entity.RolloutStage,
            entity.CreatedUtc);

    private static EvolutionRunTelemetryRecord MapTelemetry(EvolutionTelemetryEntity entity) =>
        new(
            entity.CandidateId,
            entity.TotalDurationMilliseconds,
            entity.MutationDurationMilliseconds,
            entity.SecurityEvaluationDurationMilliseconds,
            entity.CompilationDurationMilliseconds,
            entity.FitnessEvaluationDurationMilliseconds,
            entity.DiagnosticCount,
            entity.CanceledByCaller,
            entity.TimedOut,
            entity.ExecutionBudgetMilliseconds,
            entity.RecordedUtc);

    private static EvolutionFitnessEvaluationRecord MapFitness(EvolutionFitnessEntity entity) =>
        new(
            entity.CandidateId,
            entity.Score,
            entity.EvaluatorId,
            entity.Notes,
            entity.EvaluatedUtc);
}
