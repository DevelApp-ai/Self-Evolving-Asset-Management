namespace SelfEvolving.AssetManagement.Web.Models;

public sealed record CreateAssetAssignmentRequest(
    string OwnerId,
    string OwnerName);
