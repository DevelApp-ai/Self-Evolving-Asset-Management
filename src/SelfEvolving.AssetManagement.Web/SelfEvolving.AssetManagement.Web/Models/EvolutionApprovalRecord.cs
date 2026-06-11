namespace SelfEvolving.AssetManagement.Web.Models;

public sealed record EvolutionApprovalRecord(
    int Id,
    int CandidateId,
    string Decision,
    string ReviewerId,
    string? Notes,
    DateTime ReviewedUtc);
