namespace SelfEvolving.AssetManagement.Web.Data;

public sealed class AssetEntity
{
    public int Id { get; set; }
    public string AssetTag { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
}

public sealed class AssetAssignmentEntity
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public DateTime AssignedUtc { get; set; }
    public bool IsActive { get; set; }
}

public sealed class FeedbackEntity
{
    public int Id { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime SubmittedUtc { get; set; }
}

public sealed class EvolutionCandidateEntity
{
    public int Id { get; set; }
    public int SourceFeedbackId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? RolloutStage { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public sealed class EvolutionTelemetryEntity
{
    public int CandidateId { get; set; }
    public double TotalDurationMilliseconds { get; set; }
    public double MutationDurationMilliseconds { get; set; }
    public double SecurityEvaluationDurationMilliseconds { get; set; }
    public double CompilationDurationMilliseconds { get; set; }
    public double FitnessEvaluationDurationMilliseconds { get; set; }
    public int DiagnosticCount { get; set; }
    public bool CanceledByCaller { get; set; }
    public bool TimedOut { get; set; }
    public int ExecutionBudgetMilliseconds { get; set; }
    public DateTime RecordedUtc { get; set; }
}

public sealed class EvolutionFitnessEntity
{
    public int CandidateId { get; set; }
    public double Score { get; set; }
    public string EvaluatorId { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime EvaluatedUtc { get; set; }
}

public sealed class EvolutionAgentRunEntity
{
    public int Id { get; set; }
    public int CandidateId { get; set; }
    public int SourceFeedbackId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string FrameworkVersion { get; set; } = string.Empty;
    public DateTime StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
}

public sealed class EvolutionAgentStepEntity
{
    public int Id { get; set; }
    public int RunId { get; set; }
    public string AgentType { get; set; } = string.Empty;
    public string InputHash { get; set; } = string.Empty;
    public string OutputSummary { get; set; } = string.Empty;
    public int LatencyMilliseconds { get; set; }
    public int TokenCost { get; set; }
    public int DiagnosticCount { get; set; }
    public DateTime RecordedUtc { get; set; }
}

public sealed class EvolutionAgentDecisionEntity
{
    public int Id { get; set; }
    public int RunId { get; set; }
    public string Recommendation { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string Rationale { get; set; } = string.Empty;
    public bool IsBlocking { get; set; }
    public DateTime RecordedUtc { get; set; }
}

public sealed class EvolutionRunAuditLinkEntity
{
    public int Id { get; set; }
    public int RunId { get; set; }
    public int CandidateId { get; set; }
    public int? LifecycleEventId { get; set; }
    public string RelationType { get; set; } = string.Empty;
    public DateTime LinkedUtc { get; set; }
}

public sealed class EvolutionApprovalEntity
{
    public int Id { get; set; }
    public int CandidateId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string ReviewerId { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime ReviewedUtc { get; set; }
}

public sealed class EvolutionLifecycleEventEntity
{
    public int Id { get; set; }
    public int CandidateId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime OccurredUtc { get; set; }
}

public sealed class PolicyDecisionAuditEntity
{
    public int Id { get; set; }
    public string Operation { get; set; } = string.Empty;
    public bool Allowed { get; set; }
    public string AssetTag { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DenyReasonsJson { get; set; } = "[]";
    public string PolicyVersion { get; set; } = string.Empty;
    public string PolicySource { get; set; } = string.Empty;
    public DateTime EvaluatedUtc { get; set; }
}
