using System.Collections.Concurrent;
using System.Threading;
using SelfEvolving.AssetManagement.Web.Models;

namespace SelfEvolving.AssetManagement.Web.Services;

public sealed class EvolutionLifecycleService
{
    private readonly ConcurrentDictionary<int, List<EvolutionLifecycleEventRecord>> _eventsByCandidateId = new();
    private readonly object _sync = new();
    private int _nextId;

    public IReadOnlyList<EvolutionLifecycleEventRecord> GetByCandidateId(int candidateId)
    {
        if (!_eventsByCandidateId.TryGetValue(candidateId, out var events))
        {
            return [];
        }

        return events.OrderByDescending(x => x.Id).ToArray();
    }

    public EvolutionLifecycleEventRecord Record(int candidateId, string eventType, string actor, string? details)
    {
        var normalizedEventType = eventType?.Trim();
        var normalizedActor = actor?.Trim();
        var normalizedDetails = details?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedEventType))
        {
            throw new ArgumentException("EventType is required.");
        }

        if (string.IsNullOrWhiteSpace(normalizedActor))
        {
            throw new ArgumentException("Actor is required.");
        }

        lock (_sync)
        {
            var candidateEvents = _eventsByCandidateId.GetOrAdd(candidateId, _ => []);
            var id = Interlocked.Increment(ref _nextId);
            var created = new EvolutionLifecycleEventRecord(
                id,
                candidateId,
                normalizedEventType,
                normalizedActor,
                string.IsNullOrWhiteSpace(normalizedDetails) ? null : normalizedDetails,
                DateTime.UtcNow);

            candidateEvents.Add(created);
            return created;
        }
    }
}
