using SelfEvolving.AssetManagement.Web.Models;
using SelfEvolving.AssetManagement.Web.Services;

namespace SelfEvolving.AssetManagement.Web.Tests;

public class PolicyDecisionAuditServiceTests
{
    [Fact]
    public void RecordAssetCreate_WhenCalled_PersistsAuditRecord()
    {
        var service = new PolicyDecisionAuditService();
        var request = new CreateAssetRequest("A-120", "Audit Candidate", "Hardware");
        var decision = new PolicyDecision(true, []);

        var created = service.RecordAssetCreate(request, decision);
        var records = service.GetAll();

        Assert.Equal("AssetCreate", created.Operation);
        Assert.True(created.Allowed);
        Assert.Equal("A-120", created.AssetTag);
        Assert.Equal("inline-v1", created.PolicyVersion);
        Assert.Single(records);
        Assert.Equal(created.Id, records[0].Id);
    }

    [Fact]
    public void RecordAssetCreate_WhenDenied_PersistsDenyReasons()
    {
        var service = new PolicyDecisionAuditService();
        var request = new CreateAssetRequest("X-120", "Denied Candidate", "Hardware");
        var decision = new PolicyDecision(false, ["assetTag must start with 'A-' for policy compliance."]);

        var created = service.RecordAssetCreate(request, decision);

        Assert.False(created.Allowed);
        Assert.Single(created.DenyReasons);
        Assert.Equal("in-process", created.PolicySource);
    }
}
