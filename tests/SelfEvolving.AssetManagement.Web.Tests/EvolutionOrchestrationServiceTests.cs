using SelfEvolving.AssetManagement.Web.Models;
using SelfEvolving.AssetManagement.Web.Services;

namespace SelfEvolving.AssetManagement.Web.Tests;

public class EvolutionOrchestrationServiceTests
{
    [Fact]
    public void CreateFromFeedback_GeneratesProposedCandidate()
    {
        var service = new EvolutionOrchestrationService();
        var feedback = new FeedbackRecord(1, "UI", "Search", "Need better filters", DateTime.UtcNow);

        var candidate = service.CreateFromFeedback(feedback);

        Assert.Equal(1, candidate.SourceFeedbackId);
        Assert.Equal("Proposed", candidate.Status);
        Assert.Contains("Search", candidate.Title);
        Assert.Single(service.GetAll());
    }

    [Fact]
    public void CreateFromFeedback_WhenAlreadyGenerated_ThrowsInvalidOperationException()
    {
        var service = new EvolutionOrchestrationService();
        var feedback = new FeedbackRecord(2, "Ops", "Ownership", "Automate assignment", DateTime.UtcNow);
        service.CreateFromFeedback(feedback);

        var action = () => service.CreateFromFeedback(feedback);

        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void UpdateStatus_WhenCandidateExists_UpdatesCandidateStatus()
    {
        var service = new EvolutionOrchestrationService();
        var feedback = new FeedbackRecord(3, "Ops", "Rollout", "Require approval", DateTime.UtcNow);
        var created = service.CreateFromFeedback(feedback);

        var updated = service.UpdateStatus(created.Id, "Approved");

        Assert.Equal("Approved", updated.Status);
        Assert.Equal("Approved", service.GetById(created.Id)?.Status);
    }
}
