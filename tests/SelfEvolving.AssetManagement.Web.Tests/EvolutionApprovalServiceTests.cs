using SelfEvolving.AssetManagement.Web.Models;
using SelfEvolving.AssetManagement.Web.Services;

namespace SelfEvolving.AssetManagement.Web.Tests;

public class EvolutionApprovalServiceTests
{
    [Fact]
    public void CreateApproval_WithApproveDecision_CreatesApproval()
    {
        var service = new EvolutionApprovalService();

        var approval = service.CreateApproval(1, new CreateEvolutionApprovalRequest("Approve", "reviewer-1", "Looks good"));

        Assert.Equal(1, approval.CandidateId);
        Assert.Equal("Approve", approval.Decision);
        Assert.Equal("reviewer-1", approval.ReviewerId);
        Assert.Single(service.GetApprovals(1));
    }

    [Fact]
    public void CreateApproval_WhenDecisionInvalid_ThrowsArgumentException()
    {
        var service = new EvolutionApprovalService();
        var action = () => service.CreateApproval(1, new CreateEvolutionApprovalRequest("Hold", "reviewer-1", null));

        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void CreateApproval_WhenCandidateAlreadyReviewed_ThrowsInvalidOperationException()
    {
        var service = new EvolutionApprovalService();
        service.CreateApproval(1, new CreateEvolutionApprovalRequest("Reject", "reviewer-1", null));
        var action = () => service.CreateApproval(1, new CreateEvolutionApprovalRequest("Approve", "reviewer-2", null));

        Assert.Throws<InvalidOperationException>(action);
    }
}
