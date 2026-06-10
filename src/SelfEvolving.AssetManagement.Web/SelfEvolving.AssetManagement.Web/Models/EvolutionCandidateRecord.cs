namespace SelfEvolving.AssetManagement.Web.Models;

public sealed record EvolutionCandidateRecord(
    int Id,
    int SourceFeedbackId,
    string Title,
    string Summary,
    string Status,
    DateTime CreatedUtc);
