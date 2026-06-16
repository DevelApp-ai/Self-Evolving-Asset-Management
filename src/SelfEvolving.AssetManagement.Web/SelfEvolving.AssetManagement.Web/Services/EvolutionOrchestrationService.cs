using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
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
    private readonly ConcurrentDictionary<int, EvolutionAgentRunRecord> _agentRunsById = new();
    private readonly ConcurrentDictionary<int, int> _runIdByFeedbackId = new();
    private readonly ConcurrentDictionary<int, List<EvolutionAgentStepRecord>> _agentStepsByRunId = new();
    private readonly ConcurrentDictionary<int, List<EvolutionAgentDecisionRecord>> _agentDecisionsByRunId = new();
    private readonly ConcurrentDictionary<int, List<EvolutionRunAuditLinkRecord>> _auditLinksByCandidateId = new();
    private readonly int _executionBudgetMilliseconds;
    private readonly double _minimumFitnessScore;
    private readonly string _frameworkVersion;
    private readonly bool _multiAgentEnabled;
    private readonly string _multiAgentSystemMode;
    private readonly int _multiAgentMaxParallelAgents;
    private readonly int _multiAgentRunTimeoutMs;
    private readonly double _multiAgentSafetyBlockThreshold;
    private readonly bool _multiAgentRequireHumanApproval;
    private int _nextId;
    private int _nextRunId;
    private int _nextStepId;
    private int _nextDecisionId;
    private int _nextAuditLinkId;

    public EvolutionOrchestrationService(IOptions<SystemArchitectureOptions>? options = null, AssetManagementDbContext? dbContext = null)
    {
        _dbContext = dbContext;
        var config = options?.Value ?? new SystemArchitectureOptions();
        _executionBudgetMilliseconds = config.EvolutionExecutionBudgetMilliseconds;
        _minimumFitnessScore = config.EvolutionMinimumFitnessScore;
        _frameworkVersion = config.EvolutionFrameworkVersion;
        _multiAgentEnabled = config.MultiAgentEnabled;
        _multiAgentSystemMode = config.MultiAgentSystemMode?.Trim() ?? "Cloud";
        _multiAgentMaxParallelAgents = config.MultiAgentMaxParallelAgents;
        _multiAgentRunTimeoutMs = config.MultiAgentRunTimeoutMs;
        _multiAgentSafetyBlockThreshold = config.MultiAgentSafetyBlockThreshold;
        _multiAgentRequireHumanApproval = config.MultiAgentRequireHumanApproval;

        if (_executionBudgetMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Evolution execution budget must be greater than zero.");
        }

        if (_minimumFitnessScore is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Evolution minimum fitness score must be between 0 and 1.");
        }

        if (_multiAgentMaxParallelAgents <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Multi-agent max parallel agents must be greater than zero.");
        }

        if (_multiAgentRunTimeoutMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Multi-agent run timeout must be greater than zero.");
        }

        if (_multiAgentSafetyBlockThreshold is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Multi-agent safety block threshold must be between 0 and 1.");
        }

        if (!string.Equals(_multiAgentSystemMode, "Cloud", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(_multiAgentSystemMode, "Local", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Multi-agent system mode must be Cloud or Local.", nameof(options));
        }
    }

    public IReadOnlyList<EvolutionCandidateRecord> GetAll() =>
        _dbContext is null
            ? _candidatesById.Values.OrderByDescending(x => x.Id).ToArray()
            : _dbContext.EvolutionCandidates.AsNoTracking()
                .OrderByDescending(x => x.Id)
                .Select(MapCandidate)
                .ToArray();

    public EvolutionCandidateRecord? GetById(int id) =>
        _dbContext is null
            ? (_candidatesById.TryGetValue(id, out var candidate) ? candidate : null)
            : _dbContext.EvolutionCandidates.AsNoTracking()
                .Where(x => x.Id == id)
                .Select(MapCandidate)
                .SingleOrDefault();

    public EvolutionRunTelemetryRecord? GetTelemetry(int candidateId) =>
        _dbContext is null
            ? (_telemetryByCandidateId.TryGetValue(candidateId, out var telemetry) ? telemetry : null)
            : _dbContext.EvolutionTelemetry.AsNoTracking()
                .Where(x => x.CandidateId == candidateId)
                .Select(MapTelemetry)
                .SingleOrDefault();

    public EvolutionFitnessEvaluationRecord? GetFitnessEvaluation(int candidateId) =>
        _dbContext is null
            ? (_fitnessByCandidateId.TryGetValue(candidateId, out var evaluation) ? evaluation : null)
            : _dbContext.EvolutionFitness.AsNoTracking()
                .Where(x => x.CandidateId == candidateId)
                .Select(MapFitness)
                .SingleOrDefault();

    public IReadOnlyList<EvolutionAgentRunRecord> GetAgentRunsByCandidateId(int candidateId) =>
        _dbContext is null
            ? _agentRunsById.Values.Where(x => x.CandidateId == candidateId).OrderByDescending(x => x.Id).ToArray()
            : _dbContext.EvolutionAgentRuns.AsNoTracking()
                .Where(x => x.CandidateId == candidateId)
                .OrderByDescending(x => x.Id)
                .Select(MapAgentRun)
                .ToArray();

    public EvolutionAgentRunRecord? GetAgentRunById(int runId) =>
        _dbContext is null
            ? (_agentRunsById.TryGetValue(runId, out var run) ? run : null)
            : _dbContext.EvolutionAgentRuns.AsNoTracking()
                .Where(x => x.Id == runId)
                .Select(MapAgentRun)
                .SingleOrDefault();

    public IReadOnlyList<EvolutionAgentStepRecord> GetAgentRunSteps(int runId) =>
        _dbContext is null
            ? (_agentStepsByRunId.TryGetValue(runId, out var steps) ? steps.OrderBy(x => x.Id).ToArray() : [])
            : _dbContext.EvolutionAgentSteps.AsNoTracking()
                .Where(x => x.RunId == runId)
                .OrderBy(x => x.Id)
                .Select(MapAgentStep)
                .ToArray();

    public IReadOnlyList<EvolutionAgentDecisionRecord> GetAgentRunDecisions(int runId) =>
        _dbContext is null
            ? (_agentDecisionsByRunId.TryGetValue(runId, out var decisions) ? decisions.OrderBy(x => x.Id).ToArray() : [])
            : _dbContext.EvolutionAgentDecisions.AsNoTracking()
                .Where(x => x.RunId == runId)
                .OrderBy(x => x.Id)
                .Select(MapAgentDecision)
                .ToArray();

    public IReadOnlyList<EvolutionRunAuditLinkRecord> GetAuditLinksByCandidateId(int candidateId) =>
        _dbContext is null
            ? (_auditLinksByCandidateId.TryGetValue(candidateId, out var links) ? links.OrderByDescending(x => x.Id).ToArray() : [])
            : _dbContext.EvolutionRunAuditLinks.AsNoTracking()
                .Where(x => x.CandidateId == candidateId)
                .OrderByDescending(x => x.Id)
                .Select(MapAuditLink)
                .ToArray();

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
        if (!_multiAgentEnabled)
        {
            return CreateFromFeedbackSinglePath(feedback);
        }

        if (TryGetCandidateByFeedbackId(feedback.Id, out var existingCandidate))
        {
            return existingCandidate;
        }

        try
        {
            return CreateFromFeedbackMultiAgentCore(feedback);
        }
        catch (TimeoutException)
        {
            return CreateFromFeedbackSinglePath(feedback);
        }
    }

    public EvolutionCandidateRecord CreateFromFeedbackMultiAgent(FeedbackRecord feedback)
    {
        if (!_multiAgentEnabled)
        {
            throw new InvalidOperationException("Multi-agent orchestration is disabled.");
        }

        if (TryGetCandidateByFeedbackId(feedback.Id, out var existingCandidate))
        {
            return existingCandidate;
        }

        return CreateFromFeedbackMultiAgentCore(feedback);
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

    public EvolutionRunAuditLinkRecord? LinkLatestRunToLifecycleEvent(int candidateId, int lifecycleEventId, string relationType)
    {
        var run = GetAgentRunsByCandidateId(candidateId).FirstOrDefault();
        if (run is null)
        {
            return null;
        }

        return AddAuditLink(run.Id, candidateId, lifecycleEventId, relationType);
    }

    private EvolutionCandidateRecord CreateFromFeedbackSinglePath(FeedbackRecord feedback)
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

    private EvolutionCandidateRecord CreateFromFeedbackMultiAgentCore(FeedbackRecord feedback)
    {
        if (TryGetRunIdByFeedbackId(feedback.Id, out var runId))
        {
            var existingRun = GetAgentRunById(runId);
            if (existingRun is not null && existingRun.CandidateId > 0)
            {
                var existingCandidate = GetById(existingRun.CandidateId);
                if (existingCandidate is not null)
                {
                    return existingCandidate;
                }
            }
        }

        var startedUtc = DateTime.UtcNow;
        var inputHash = ComputeHash($"{feedback.Source}|{feedback.Subject}|{feedback.Message}");
        var estimatedLatency = 0;

        var stepRequests = new List<(string AgentType, string OutputSummary, int LatencyMilliseconds, int TokenCost, int DiagnosticCount)>
        {
            ("CoordinatorAgent", $"Routed to {Math.Min(_multiAgentMaxParallelAgents, 5)} specialist agents on {GetMultiAgentSystemLabel()} runtime", 15, 32, 0),
            ("CandidateSynthesisAgent", "Generated candidate title and summary strategy", 42, 120, 0),
            ("PolicySafetyAgent", "Validated policy and risk envelope", 35, 95, 0),
            ("FitnessEvaluationAgent", "Estimated candidate fitness and confidence", 27, 80, 0),
            ("RolloutStrategyAgent", "Recommended staged rollout progression", 21, 76, 0),
            ("HumanGateAdapter", _multiAgentRequireHumanApproval ? "Prepared human approval packet" : "Prepared automated gate packet", 14, 40, 0)
        };

        estimatedLatency = stepRequests.Sum(x => x.LatencyMilliseconds);

        var safetyDecision = EvaluateSafetyDecision(feedback);
        var fitnessDecision = BuildFitnessDecision(feedback);
        var rolloutDecision = BuildRolloutDecision();

        var runRecord = CreateAgentRun(feedback.Id, 0, "Running", startedUtc, null);
        RegisterRunIdByFeedback(feedback.Id, runRecord.Id);

        foreach (var request in stepRequests)
        {
            AddAgentStep(runRecord.Id, request.AgentType, inputHash, request.OutputSummary, request.LatencyMilliseconds, request.TokenCost, request.DiagnosticCount);
        }

        AddAgentDecision(runRecord.Id, safetyDecision.Recommendation, safetyDecision.Confidence, safetyDecision.Rationale, safetyDecision.IsBlocking);
        AddAgentDecision(runRecord.Id, fitnessDecision.Recommendation, fitnessDecision.Confidence, fitnessDecision.Rationale, fitnessDecision.IsBlocking);
        AddAgentDecision(runRecord.Id, rolloutDecision.Recommendation, rolloutDecision.Confidence, rolloutDecision.Rationale, rolloutDecision.IsBlocking);

        if (estimatedLatency > _multiAgentRunTimeoutMs)
        {
            UpdateRunStatus(runRecord.Id, "TimedOut", DateTime.UtcNow);
            throw new TimeoutException("Multi-agent orchestration timed out.");
        }

        if (safetyDecision.IsBlocking)
        {
            UpdateRunStatus(runRecord.Id, "Blocked", DateTime.UtcNow);
            throw new InvalidOperationException("Multi-agent policy/safety agent denied candidate generation.");
        }

        var synthesis = SynthesizeCandidateMultiAgent(feedback, fitnessDecision.Confidence, _multiAgentSystemMode);
        var candidate = CreateFromFeedbackSinglePathCore(feedback, synthesis.Title, synthesis.Summary, startedUtc, estimatedLatency);

        var minFitness = Math.Max(_minimumFitnessScore, Math.Min(1, fitnessDecision.Confidence));
        SetFitnessEvaluation(candidate.Id, new CreateEvolutionFitnessEvaluationRequest(minFitness, "multi-agent-fitness", "Pre-approval multi-agent fitness estimate"));

        UpdateRunCandidate(runRecord.Id, candidate.Id);
        UpdateRunStatus(runRecord.Id, "Completed", DateTime.UtcNow);
        AddAuditLink(runRecord.Id, candidate.Id, null, "CandidateCreated");

        return candidate;
    }

    private EvolutionCandidateRecord CreateFromFeedbackSinglePathCore(FeedbackRecord feedback, string title, string summary, DateTime startedUtc, int syntheticLatency)
    {
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
                Title = title,
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
                MutationDurationMilliseconds = Math.Max(1, syntheticLatency),
                SecurityEvaluationDurationMilliseconds = Math.Max(1, syntheticLatency / 3d),
                CompilationDurationMilliseconds = Math.Max(1, syntheticLatency / 4d),
                FitnessEvaluationDurationMilliseconds = Math.Max(1, syntheticLatency / 5d),
                DiagnosticCount = 0,
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
            title,
            summary,
            "Proposed",
            null,
            DateTime.UtcNow);

        _candidatesById[id] = created;
        _telemetryByCandidateId[id] = new EvolutionRunTelemetryRecord(
            CandidateId: id,
            TotalDurationMilliseconds: Math.Max(1, (DateTime.UtcNow - startedUtc).TotalMilliseconds),
            MutationDurationMilliseconds: Math.Max(1, syntheticLatency),
            SecurityEvaluationDurationMilliseconds: Math.Max(1, syntheticLatency / 3d),
            CompilationDurationMilliseconds: Math.Max(1, syntheticLatency / 4d),
            FitnessEvaluationDurationMilliseconds: Math.Max(1, syntheticLatency / 5d),
            DiagnosticCount: 0,
            CanceledByCaller: false,
            TimedOut: false,
            ExecutionBudgetMilliseconds: _executionBudgetMilliseconds,
            RecordedUtc: DateTime.UtcNow);
        return created;
    }

    private EvolutionAgentRunRecord CreateAgentRun(int feedbackId, int candidateId, string status, DateTime startedUtc, DateTime? completedUtc)
    {
        if (_dbContext is not null)
        {
            var entity = new EvolutionAgentRunEntity
            {
                CandidateId = candidateId,
                SourceFeedbackId = feedbackId,
                Status = status,
                FrameworkVersion = _frameworkVersion,
                StartedUtc = startedUtc,
                CompletedUtc = completedUtc
            };
            _dbContext.EvolutionAgentRuns.Add(entity);
            _dbContext.SaveChanges();
            return MapAgentRun(entity);
        }

        var id = Interlocked.Increment(ref _nextRunId);
        var run = new EvolutionAgentRunRecord(id, candidateId, feedbackId, status, _frameworkVersion, startedUtc, completedUtc);
        _agentRunsById[id] = run;
        return run;
    }

    private EvolutionAgentStepRecord AddAgentStep(int runId, string agentType, string inputHash, string outputSummary, int latencyMilliseconds, int tokenCost, int diagnostics)
    {
        if (_dbContext is not null)
        {
            var entity = new EvolutionAgentStepEntity
            {
                RunId = runId,
                AgentType = agentType,
                InputHash = inputHash,
                OutputSummary = outputSummary,
                LatencyMilliseconds = latencyMilliseconds,
                TokenCost = tokenCost,
                DiagnosticCount = diagnostics,
                RecordedUtc = DateTime.UtcNow
            };
            _dbContext.EvolutionAgentSteps.Add(entity);
            _dbContext.SaveChanges();
            return MapAgentStep(entity);
        }

        var id = Interlocked.Increment(ref _nextStepId);
        var record = new EvolutionAgentStepRecord(id, runId, agentType, inputHash, outputSummary, latencyMilliseconds, tokenCost, diagnostics, DateTime.UtcNow);
        var steps = _agentStepsByRunId.GetOrAdd(runId, _ => []);
        lock (steps)
        {
            steps.Add(record);
        }

        return record;
    }

    private EvolutionAgentDecisionRecord AddAgentDecision(int runId, string recommendation, double confidence, string rationale, bool isBlocking)
    {
        if (_dbContext is not null)
        {
            var entity = new EvolutionAgentDecisionEntity
            {
                RunId = runId,
                Recommendation = recommendation,
                Confidence = confidence,
                Rationale = rationale,
                IsBlocking = isBlocking,
                RecordedUtc = DateTime.UtcNow
            };
            _dbContext.EvolutionAgentDecisions.Add(entity);
            _dbContext.SaveChanges();
            return MapAgentDecision(entity);
        }

        var id = Interlocked.Increment(ref _nextDecisionId);
        var record = new EvolutionAgentDecisionRecord(id, runId, recommendation, confidence, rationale, isBlocking, DateTime.UtcNow);
        var decisions = _agentDecisionsByRunId.GetOrAdd(runId, _ => []);
        lock (decisions)
        {
            decisions.Add(record);
        }

        return record;
    }

    private EvolutionRunAuditLinkRecord AddAuditLink(int runId, int candidateId, int? lifecycleEventId, string relationType)
    {
        if (_dbContext is not null)
        {
            var entity = new EvolutionRunAuditLinkEntity
            {
                RunId = runId,
                CandidateId = candidateId,
                LifecycleEventId = lifecycleEventId,
                RelationType = relationType,
                LinkedUtc = DateTime.UtcNow
            };
            _dbContext.EvolutionRunAuditLinks.Add(entity);
            _dbContext.SaveChanges();
            return MapAuditLink(entity);
        }

        var id = Interlocked.Increment(ref _nextAuditLinkId);
        var link = new EvolutionRunAuditLinkRecord(id, runId, candidateId, lifecycleEventId, relationType, DateTime.UtcNow);
        var links = _auditLinksByCandidateId.GetOrAdd(candidateId, _ => []);
        lock (links)
        {
            links.Add(link);
        }

        return link;
    }

    private void UpdateRunStatus(int runId, string status, DateTime? completedUtc)
    {
        if (_dbContext is not null)
        {
            var run = _dbContext.EvolutionAgentRuns.Single(x => x.Id == runId);
            run.Status = status;
            run.CompletedUtc = completedUtc;
            _dbContext.SaveChanges();
            return;
        }

        if (_agentRunsById.TryGetValue(runId, out var runRecord))
        {
            _agentRunsById[runId] = runRecord with { Status = status, CompletedUtc = completedUtc };
        }
    }

    private void UpdateRunCandidate(int runId, int candidateId)
    {
        if (_dbContext is not null)
        {
            var run = _dbContext.EvolutionAgentRuns.Single(x => x.Id == runId);
            run.CandidateId = candidateId;
            _dbContext.SaveChanges();
            return;
        }

        if (_agentRunsById.TryGetValue(runId, out var runRecord))
        {
            _agentRunsById[runId] = runRecord with { CandidateId = candidateId };
        }
    }

    private bool TryGetRunIdByFeedbackId(int feedbackId, out int runId)
    {
        if (_dbContext is not null)
        {
            runId = _dbContext.EvolutionAgentRuns.AsNoTracking()
                .Where(x => x.SourceFeedbackId == feedbackId)
                .Select(x => x.Id)
                .SingleOrDefault();
            return runId > 0;
        }

        return _runIdByFeedbackId.TryGetValue(feedbackId, out runId);
    }

    private void RegisterRunIdByFeedback(int feedbackId, int runId)
    {
        if (_dbContext is null)
        {
            _runIdByFeedbackId[feedbackId] = runId;
        }
    }

    private bool TryGetCandidateByFeedbackId(int feedbackId, out EvolutionCandidateRecord candidate)
    {
        if (_dbContext is not null)
        {
            var entity = _dbContext.EvolutionCandidates.AsNoTracking().SingleOrDefault(x => x.SourceFeedbackId == feedbackId);
            if (entity is null)
            {
                candidate = default!;
                return false;
            }

            candidate = MapCandidate(entity);
            return true;
        }

        if (_candidateIdByFeedbackId.TryGetValue(feedbackId, out var candidateId) && _candidatesById.TryGetValue(candidateId, out var inMemoryCandidate))
        {
            candidate = inMemoryCandidate;
            return true;
        }

        candidate = default!;
        return false;
    }

    private (string Recommendation, double Confidence, string Rationale, bool IsBlocking) EvaluateSafetyDecision(FeedbackRecord feedback)
    {
        var message = feedback.Message?.Trim() ?? string.Empty;
        var lowered = message.ToLowerInvariant();
        var hasDangerousIntent = lowered.Contains("bypass") || lowered.Contains("disable auth") || lowered.Contains("drop audit");
        var confidence = hasDangerousIntent ? 0.05 : 0.95;
        var blocked = confidence < _multiAgentSafetyBlockThreshold;
        var rationale = blocked
            ? "Detected unsafe intent in feedback and denied evolution candidate generation."
            : "Safety policy checks passed for candidate generation.";

        return (blocked ? "Deny" : "Allow", confidence, rationale, blocked);
    }

    private static (string Recommendation, double Confidence, string Rationale, bool IsBlocking) BuildFitnessDecision(FeedbackRecord feedback)
    {
        var confidence = string.IsNullOrWhiteSpace(feedback.Message) ? 0.7 : 0.9;
        return ("PromoteIfApproved", confidence, "Fitness agent predicts positive quality impact.", false);
    }

    private static (string Recommendation, double Confidence, string Rationale, bool IsBlocking) BuildRolloutDecision() =>
        ("InternalThenPilotThenFull", 0.88, "Rollout strategy agent recommends staged promotion with rollback on regression.", false);

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

    private (string Title, string Summary) SynthesizeCandidateMultiAgent(FeedbackRecord feedback, double confidence, string multiAgentSystemMode)
    {
        var previousTitle = GetAll().FirstOrDefault()?.Title;
        var baseTitle = $"Improve: {feedback.Subject}";
        var coordinatorTitle = string.IsNullOrWhiteSpace(previousTitle)
            ? baseTitle
            : $"{baseTitle} + {ExtractSuffix(previousTitle)}";
        var summary = $"{feedback.Message.Trim()} [multi-agent:v1.3 mode:{multiAgentSystemMode.ToLowerInvariant()} confidence:{confidence:F2}]";
        return (coordinatorTitle, summary);
    }

    private string GetMultiAgentSystemLabel() =>
        string.Equals(_multiAgentSystemMode, "Local", StringComparison.OrdinalIgnoreCase) ? "local" : "cloud";

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

    private bool MeetsFitnessGate(int candidateId) =>
        GetFitnessEvaluation(candidateId) is { } evaluation &&
        evaluation.Score >= _minimumFitnessScore;

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
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

    private static EvolutionAgentRunRecord MapAgentRun(EvolutionAgentRunEntity entity) =>
        new(
            entity.Id,
            entity.CandidateId,
            entity.SourceFeedbackId,
            entity.Status,
            entity.FrameworkVersion,
            entity.StartedUtc,
            entity.CompletedUtc);

    private static EvolutionAgentStepRecord MapAgentStep(EvolutionAgentStepEntity entity) =>
        new(
            entity.Id,
            entity.RunId,
            entity.AgentType,
            entity.InputHash,
            entity.OutputSummary,
            entity.LatencyMilliseconds,
            entity.TokenCost,
            entity.DiagnosticCount,
            entity.RecordedUtc);

    private static EvolutionAgentDecisionRecord MapAgentDecision(EvolutionAgentDecisionEntity entity) =>
        new(
            entity.Id,
            entity.RunId,
            entity.Recommendation,
            entity.Confidence,
            entity.Rationale,
            entity.IsBlocking,
            entity.RecordedUtc);

    private static EvolutionRunAuditLinkRecord MapAuditLink(EvolutionRunAuditLinkEntity entity) =>
        new(
            entity.Id,
            entity.RunId,
            entity.CandidateId,
            entity.LifecycleEventId,
            entity.RelationType,
            entity.LinkedUtc);
}
