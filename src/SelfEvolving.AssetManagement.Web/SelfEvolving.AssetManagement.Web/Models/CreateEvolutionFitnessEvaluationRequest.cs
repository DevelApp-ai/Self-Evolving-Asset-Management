namespace SelfEvolving.AssetManagement.Web.Models;

public sealed record CreateEvolutionFitnessEvaluationRequest(
    double Score,
    string EvaluatorId,
    string? Notes);
