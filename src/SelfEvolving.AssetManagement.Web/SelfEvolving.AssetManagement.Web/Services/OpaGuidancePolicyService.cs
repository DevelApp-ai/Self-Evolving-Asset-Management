using System.Text.Json;
using Microsoft.Extensions.Options;
using SelfEvolving.AssetManagement.Web.Configuration;
using SelfEvolving.AssetManagement.Web.Models;

namespace SelfEvolving.AssetManagement.Web.Services;

public sealed class OpaGuidancePolicyService
{
    private readonly SystemArchitectureOptions _options;
    private readonly string _contentRootPath;
    private OpaAssetCreatePolicyBundle? _cachedBundle;

    public OpaGuidancePolicyService(IOptions<SystemArchitectureOptions>? options = null, IHostEnvironment? hostEnvironment = null)
    {
        _options = options?.Value ?? new SystemArchitectureOptions();
        _contentRootPath = hostEnvironment?.ContentRootPath ?? Directory.GetCurrentDirectory();
    }

    public PolicyDecision EvaluateAssetCreate(CreateAssetRequest request)
    {
        var bundle = LoadBundle();
        var denyReasons = new List<string>();
        var assetTag = request.AssetTag?.Trim() ?? string.Empty;
        var name = request.Name?.Trim() ?? string.Empty;
        var category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim();
        var assetTagPrefix = bundle.AssetTagPrefix ?? "A-";
        var allowedCategories = bundle.AllowedCategories ?? ["Hardware", "Software", "Devices", "General"];
        var policyVersion = string.IsNullOrWhiteSpace(bundle.PolicyVersion) ? _options.OpaPolicyBundleVersion : bundle.PolicyVersion;
        var policySource = string.IsNullOrWhiteSpace(bundle.PolicySource) ? "opa-bundle-json" : bundle.PolicySource;

        if (!assetTag.StartsWith(assetTagPrefix, StringComparison.OrdinalIgnoreCase))
        {
            denyReasons.Add($"assetTag must start with '{assetTagPrefix}' for policy compliance.");
        }

        if (name.Length > bundle.MaxNameLength)
        {
            denyReasons.Add($"name length must be {bundle.MaxNameLength} characters or less.");
        }

        if (!allowedCategories.Contains(category, StringComparer.OrdinalIgnoreCase))
        {
            denyReasons.Add($"category '{category}' is not allowed by policy.");
        }

        return new PolicyDecision(
            denyReasons.Count == 0,
            denyReasons,
            policyVersion,
            policySource);
    }

    private OpaAssetCreatePolicyBundle LoadBundle()
    {
        if (_cachedBundle is not null)
        {
            return _cachedBundle;
        }

        var relativePath = _options.OpaPolicyBundlePath?.Trim() ?? string.Empty;
        var resolvedPath = Path.IsPathRooted(relativePath)
            ? relativePath
            : Path.Combine(_contentRootPath, relativePath);

        if (!File.Exists(resolvedPath))
        {
            _cachedBundle = OpaAssetCreatePolicyBundle.Default(_options.OpaPolicyBundleVersion);
            return _cachedBundle;
        }

        var json = File.ReadAllText(resolvedPath);
        var parsed = JsonSerializer.Deserialize<OpaAssetCreatePolicyBundle>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        _cachedBundle = (parsed ?? OpaAssetCreatePolicyBundle.Default(_options.OpaPolicyBundleVersion)).Normalize(_options.OpaPolicyBundleVersion);

        return _cachedBundle;
    }

    private sealed record OpaAssetCreatePolicyBundle(
        string? PolicyVersion,
        string? AssetTagPrefix,
        int MaxNameLength,
        string[]? AllowedCategories,
        string? PolicySource)
    {
        public OpaAssetCreatePolicyBundle Normalize(string configuredVersion) =>
            new(
                string.IsNullOrWhiteSpace(PolicyVersion) ? configuredVersion : PolicyVersion,
                string.IsNullOrWhiteSpace(AssetTagPrefix) ? "A-" : AssetTagPrefix,
                MaxNameLength <= 0 ? 100 : MaxNameLength,
                AllowedCategories is { Length: > 0 } ? AllowedCategories : ["Hardware", "Software", "Devices", "General"],
                string.IsNullOrWhiteSpace(PolicySource) ? "opa-bundle-json" : PolicySource);

        public static OpaAssetCreatePolicyBundle Default(string configuredVersion) =>
            new(
                string.IsNullOrWhiteSpace(configuredVersion) ? "v1.0.0" : configuredVersion,
                "A-",
                100,
                ["Hardware", "Software", "Devices", "General"],
                "opa-bundle-json");
    }
}
