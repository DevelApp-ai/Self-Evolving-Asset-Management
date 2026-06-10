namespace SelfEvolving.AssetManagement.Web.Models;

public sealed record CreateAssetRequest(
    string AssetTag,
    string Name,
    string Category);
