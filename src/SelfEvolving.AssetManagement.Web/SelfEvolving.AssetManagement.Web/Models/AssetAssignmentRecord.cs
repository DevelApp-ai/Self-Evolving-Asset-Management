namespace SelfEvolving.AssetManagement.Web.Models;

public sealed record AssetAssignmentRecord(
    int Id,
    int AssetId,
    string OwnerId,
    string OwnerName,
    DateTime AssignedUtc,
    bool IsActive);
