namespace SelfEvolving.AssetManagement.Web.Models;

public sealed record EvolutionRunAuditLinkRecord(
    int Id,
    int RunId,
    int CandidateId,
    int? LifecycleEventId,
    string RelationType,
    DateTime LinkedUtc);
