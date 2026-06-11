using SelfEvolving.AssetManagement.Web.Models;
using SelfEvolving.AssetManagement.Web.Services;

namespace SelfEvolving.AssetManagement.Web.Tests;

public class AssetOwnershipServiceTests
{
    [Fact]
    public void Assign_CreatesActiveAssignment()
    {
        var service = new AssetOwnershipService();

        var created = service.Assign(1, new CreateAssetAssignmentRequest("u-1", "Alice"));

        Assert.Equal(1, created.AssetId);
        Assert.Equal("u-1", created.OwnerId);
        Assert.True(created.IsActive);
        Assert.Single(service.GetAssignments(1));
    }

    [Fact]
    public void Assign_WhenReassigned_DeactivatesPreviousAssignment()
    {
        var service = new AssetOwnershipService();
        service.Assign(1, new CreateAssetAssignmentRequest("u-1", "Alice"));

        var second = service.Assign(1, new CreateAssetAssignmentRequest("u-2", "Bob"));
        var assignments = service.GetAssignments(1);

        Assert.Equal(2, assignments.Count);
        Assert.True(second.IsActive);
        Assert.Single(assignments.Where(x => x.IsActive));
        Assert.Equal("u-2", assignments.Single(x => x.IsActive).OwnerId);
    }
}
