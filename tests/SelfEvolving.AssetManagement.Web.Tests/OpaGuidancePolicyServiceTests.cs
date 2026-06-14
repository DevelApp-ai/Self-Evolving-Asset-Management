using SelfEvolving.AssetManagement.Web.Models;
using SelfEvolving.AssetManagement.Web.Configuration;
using SelfEvolving.AssetManagement.Web.Services;
using Microsoft.Extensions.Options;

namespace SelfEvolving.AssetManagement.Web.Tests;

public class OpaGuidancePolicyServiceTests
{
    [Fact]
    public void EvaluateAssetCreate_WhenCompliant_AllowsRequest()
    {
        var service = new OpaGuidancePolicyService();

        var decision = service.EvaluateAssetCreate(new CreateAssetRequest("A-101", "Laptop", "Hardware"));

        Assert.True(decision.Allowed);
        Assert.Empty(decision.DenyReasons);
        Assert.Equal("opa-bundle-json", decision.PolicySource);
    }

    [Fact]
    public void EvaluateAssetCreate_WhenAssetTagViolatesPolicy_DeniesRequest()
    {
        var service = new OpaGuidancePolicyService();

        var decision = service.EvaluateAssetCreate(new CreateAssetRequest("X-101", "Laptop", "Hardware"));

        Assert.False(decision.Allowed);
        Assert.Contains(decision.DenyReasons, reason => reason.Contains("assetTag", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EvaluateAssetCreate_WhenExternalPolicyBundleConfigured_UsesBundleValues()
    {
        var policyFile = Path.Combine(Path.GetTempPath(), $"asset-policy-{Guid.NewGuid():N}.json");
        File.WriteAllText(policyFile, """
                                  {
                                    "policyVersion": "v2-test",
                                    "assetTagPrefix": "B-",
                                    "maxNameLength": 10,
                                    "allowedCategories": [ "Hardware" ],
                                    "policySource": "opa-bundle-test"
                                  }
                                  """);

        try
        {
            var options = Options.Create(new SystemArchitectureOptions
            {
                OpaPolicyBundlePath = policyFile
            });
            var service = new OpaGuidancePolicyService(options);

            var decision = service.EvaluateAssetCreate(new CreateAssetRequest("A-101", "VeryLongAssetName", "General"));

            Assert.False(decision.Allowed);
            Assert.Equal("v2-test", decision.PolicyVersion);
            Assert.Equal("opa-bundle-test", decision.PolicySource);
            Assert.Equal(3, decision.DenyReasons.Count);
        }
        finally
        {
            if (File.Exists(policyFile))
            {
                File.Delete(policyFile);
            }
        }
    }
}
