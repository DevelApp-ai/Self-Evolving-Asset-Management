using System.Collections.Concurrent;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using SelfEvolving.AssetManagement.Web.Data;
using SelfEvolving.AssetManagement.Web.Models;

namespace SelfEvolving.AssetManagement.Web.Services;

public sealed class EvolutionLifecycleService
{
    private readonly AssetManagementDbContext? _dbContext;
    private readonly ConcurrentDictionary<int, List<EvolutionLifecycleEventRecord>> _eventsByCandidateId = new();
    private readonly object _sync = new();
    private int _nextId;

    public EvolutionLifecycleService(AssetManagementDbContext? dbContext = null)
    {
        _dbContext = dbContext;
    }

    public IReadOnlyList<EvolutionLifecycleEventRecord> GetByCandidateId(int candidateId)
    {
        if (_dbContext is not null)
        {
            return _dbContext.EvolutionLifecycleEvents.AsNoTracking()
                .Where(x => x.CandidateId == candidateId)
                .OrderByDescending(x => x.Id)
                .Select(x => new EvolutionLifecycleEventRecord(
                    x.Id,
                    x.CandidateId,
                    x.EventType,
                    x.Actor,
                    x.Details,
                    x.OccurredUtc))
                .ToArray();
        }

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

        if (_dbContext is not null)
        {
            var entity = new EvolutionLifecycleEventEntity
            {
                CandidateId = candidateId,
                EventType = normalizedEventType,
                Actor = normalizedActor,
                Details = string.IsNullOrWhiteSpace(normalizedDetails) ? null : normalizedDetails,
                OccurredUtc = DateTime.UtcNow
            };
            _dbContext.EvolutionLifecycleEvents.Add(entity);
            _dbContext.SaveChanges();

            return new EvolutionLifecycleEventRecord(
                entity.Id,
                entity.CandidateId,
                entity.EventType,
                entity.Actor,
                entity.Details,
                entity.OccurredUtc);
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
