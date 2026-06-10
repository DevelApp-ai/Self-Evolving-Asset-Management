using SelfEvolving.AssetManagement.Web.Services;

namespace SelfEvolving.AssetManagement.Web.Tests;

public class EvolutionLifecycleServiceTests
{
    [Fact]
    public void Record_WhenValid_CreatesEvent()
    {
        var service = new EvolutionLifecycleService();

        var created = service.Record(1, "Approved", "reviewer-1", "Looks good");

        Assert.Equal(1, created.CandidateId);
        Assert.Equal("Approved", created.EventType);
        Assert.Equal("reviewer-1", created.Actor);
        Assert.Equal("Looks good", created.Details);
        Assert.Single(service.GetByCandidateId(1));
    }

    [Fact]
    public void GetByCandidateId_ReturnsMostRecentFirst()
    {
        var service = new EvolutionLifecycleService();
        service.Record(2, "Approved", "reviewer-1", null);
        service.Record(2, "Activated", "system", null);

        var events = service.GetByCandidateId(2);

        Assert.Equal(2, events.Count);
        Assert.Equal("Activated", events[0].EventType);
        Assert.Equal("Approved", events[1].EventType);
    }
}
