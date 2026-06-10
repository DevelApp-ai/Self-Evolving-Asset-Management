namespace SelfEvolving.AssetManagement.Web.Models;

public sealed record EvolutionLifecycleEventRecord(
    int Id,
    int CandidateId,
    string EventType,
    string Actor,
    string? Details,
    DateTime OccurredUtc);
