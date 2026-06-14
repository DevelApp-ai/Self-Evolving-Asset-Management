namespace SelfEvolving.AssetManagement.Web.Models;

public sealed record PolicyDecision(
    bool Allowed,
    IReadOnlyList<string> DenyReasons,
    string PolicyVersion = "inline-v1",
    string PolicySource = "in-process");
