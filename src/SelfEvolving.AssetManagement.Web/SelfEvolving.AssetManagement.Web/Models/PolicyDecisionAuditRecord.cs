namespace SelfEvolving.AssetManagement.Web.Models;

public sealed record PolicyDecisionAuditRecord(
    int Id,
    string Operation,
    bool Allowed,
    string AssetTag,
    string Name,
    string Category,
    IReadOnlyList<string> DenyReasons,
    string PolicyVersion,
    string PolicySource,
    DateTime EvaluatedUtc);
