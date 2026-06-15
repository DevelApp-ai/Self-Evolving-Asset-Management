namespace SelfEvolving.AssetManagement.Web.Models;

public sealed record EvolutionAgentRunRecord(
    int Id,
    int CandidateId,
    int SourceFeedbackId,
    string Status,
    string FrameworkVersion,
    DateTime StartedUtc,
    DateTime? CompletedUtc);
