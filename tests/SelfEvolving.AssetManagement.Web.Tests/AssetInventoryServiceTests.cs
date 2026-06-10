using SelfEvolving.AssetManagement.Web.Models;
using SelfEvolving.AssetManagement.Web.Services;

namespace SelfEvolving.AssetManagement.Web.Tests;

public class AssetInventoryServiceTests
{
    [Fact]
    public void Create_AssignsIdAndStoresAsset()
    {
        var service = new AssetInventoryService();

        var created = service.Create(new CreateAssetRequest("A-100", "Laptop", "Hardware"));

        Assert.Equal(1, created.Id);
        Assert.Equal("A-100", created.AssetTag);
        Assert.Single(service.GetAll());
        Assert.Equal(created, service.GetById(created.Id));
    }

    [Fact]
    public void Create_WhenAssetTagAlreadyExists_Throws()
    {
        var service = new AssetInventoryService();
        service.Create(new CreateAssetRequest("A-100", "Laptop", "Hardware"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            service.Create(new CreateAssetRequest("a-100", "Monitor", "Hardware")));

        Assert.Contains("already exists", ex.Message);
    }
}
