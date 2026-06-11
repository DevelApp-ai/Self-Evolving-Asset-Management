namespace SelfEvolving.AssetManagement.Web.Models;

public sealed record PolicyDecision(
    bool Allowed,
    IReadOnlyList<string> DenyReasons);
