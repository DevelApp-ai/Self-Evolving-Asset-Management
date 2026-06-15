namespace SelfEvolving.AssetManagement.Web.Models;

public sealed record EvolutionAgentDecisionRecord(
    int Id,
    int RunId,
    string Recommendation,
    double Confidence,
    string Rationale,
    bool IsBlocking,
    DateTime RecordedUtc);
