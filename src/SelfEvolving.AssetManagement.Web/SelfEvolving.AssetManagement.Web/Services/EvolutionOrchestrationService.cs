using System.Collections.Concurrent;
using System.Threading;
using SelfEvolving.AssetManagement.Web.Models;

namespace SelfEvolving.AssetManagement.Web.Services;

public sealed class EvolutionOrchestrationService
{
    private static readonly string[] RolloutStages = ["Internal", "Pilot", "Full"];
    private readonly ConcurrentDictionary<int, EvolutionCandidateRecord> _candidatesById = new();
    private readonly ConcurrentDictionary<int, int> _candidateIdByFeedbackId = new();
    private int _nextId;

    public IReadOnlyList<EvolutionCandidateRecord> GetAll() =>
        _candidatesById.Values
            .OrderByDescending(x => x.Id)
            .ToArray();

    public EvolutionCandidateRecord? GetById(int id) =>
        _candidatesById.TryGetValue(id, out var candidate) ? candidate : null;

    public EvolutionCandidateRecord CreateFromFeedback(FeedbackRecord feedback)
    {
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

    public EvolutionCandidateRecord PromoteRollout(int id)
    {
        var candidate = GetById(id) ?? throw new InvalidOperationException($"Candidate '{id}' was not found.");
        if (candidate.Status != "Active")
        {
            throw new InvalidOperationException($"Candidate '{id}' must be active before rollout promotion.");
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

        var updated = candidate with { Status = "Released" };
        _candidatesById[id] = updated;
        return updated;
    }
}
