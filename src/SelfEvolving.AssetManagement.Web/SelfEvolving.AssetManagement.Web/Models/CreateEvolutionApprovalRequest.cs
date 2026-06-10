namespace SelfEvolving.AssetManagement.Web.Models;

public sealed record CreateEvolutionApprovalRequest(
    string Decision,
    string ReviewerId,
    string? Notes);
