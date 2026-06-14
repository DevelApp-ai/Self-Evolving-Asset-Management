namespace SelfEvolving.AssetManagement.Web.Models;

public sealed record EvolutionFitnessEvaluationRecord(
    int CandidateId,
    double Score,
    string EvaluatorId,
    string? Notes,
    DateTime EvaluatedUtc);
