using SelfEvolving.AssetManagement.Web.Models;

namespace SelfEvolving.AssetManagement.Web.Services;

public sealed class OpaGuidancePolicyService
{
    private static readonly HashSet<string> AllowedCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Hardware",
        "Software",
        "Devices",
        "General"
    };

    public PolicyDecision EvaluateAssetCreate(CreateAssetRequest request)
    {
        var denyReasons = new List<string>();

        if (!request.AssetTag.Trim().StartsWith("A-", StringComparison.OrdinalIgnoreCase))
        {
            denyReasons.Add("assetTag must start with 'A-' for policy compliance.");
        }

        if (request.Name.Trim().Length > 100)
        {
            denyReasons.Add("name length must be 100 characters or less.");
        }

        var category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim();
        if (!AllowedCategories.Contains(category))
        {
            denyReasons.Add($"category '{category}' is not allowed by policy.");
        }

        return new PolicyDecision(denyReasons.Count == 0, denyReasons);
    }
}
