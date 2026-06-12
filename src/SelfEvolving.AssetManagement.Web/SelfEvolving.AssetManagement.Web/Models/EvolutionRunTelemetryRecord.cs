namespace SelfEvolving.AssetManagement.Web.Models;

public sealed record EvolutionRunTelemetryRecord(
    int CandidateId,
    double TotalDurationMilliseconds,
    double MutationDurationMilliseconds,
    double SecurityEvaluationDurationMilliseconds,
    double CompilationDurationMilliseconds,
    double FitnessEvaluationDurationMilliseconds,
    int DiagnosticCount,
    bool CanceledByCaller,
    bool TimedOut,
    int ExecutionBudgetMilliseconds,
    DateTime RecordedUtc);
