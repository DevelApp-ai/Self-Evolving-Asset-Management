namespace SelfEvolving.AssetManagement.Web.Models;

public sealed record EvolutionAgentStepRecord(
    int Id,
    int RunId,
    string AgentType,
    string InputHash,
    string OutputSummary,
    int LatencyMilliseconds,
    int TokenCost,
    int DiagnosticCount,
    DateTime RecordedUtc);
