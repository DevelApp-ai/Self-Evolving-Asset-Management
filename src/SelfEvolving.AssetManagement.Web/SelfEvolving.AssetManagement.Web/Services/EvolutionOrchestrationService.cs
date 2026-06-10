using System.Collections.Concurrent;
using System.Threading;
using SelfEvolving.AssetManagement.Web.Models;

namespace SelfEvolving.AssetManagement.Web.Services;

public sealed class EvolutionOrchestrationService
{
    private readonly ConcurrentDictionary<int, EvolutionCandidateRecord> _candidatesById = new();
    private readonly ConcurrentDictionary<int, int> _candidateIdByFeedbackId = new();
    private int _nextId;

    public IReadOnlyList<EvolutionCandidateRecord> GetAll() =>
        _candidatesById.Values
            .OrderByDescending(x => x.Id)
            .ToArray();

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
            DateTime.UtcNow);

        _candidatesById[id] = created;
        return created;
    }
}
