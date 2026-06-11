namespace SelfEvolving.AssetManagement.Web.Models;

public sealed record AssetRecord(
    int Id,
    string AssetTag,
    string Name,
    string Category,
    DateTime CreatedUtc);
