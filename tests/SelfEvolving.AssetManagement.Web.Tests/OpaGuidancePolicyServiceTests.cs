using SelfEvolving.AssetManagement.Web.Models;
using SelfEvolving.AssetManagement.Web.Services;

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
    }

    [Fact]
    public void EvaluateAssetCreate_WhenAssetTagViolatesPolicy_DeniesRequest()
    {
        var service = new OpaGuidancePolicyService();

        var decision = service.EvaluateAssetCreate(new CreateAssetRequest("X-101", "Laptop", "Hardware"));

        Assert.False(decision.Allowed);
        Assert.Contains(decision.DenyReasons, reason => reason.Contains("assetTag", StringComparison.OrdinalIgnoreCase));
    }
}
