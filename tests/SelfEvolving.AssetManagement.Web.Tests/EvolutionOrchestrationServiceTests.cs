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

    [Fact]
    public void Activate_WhenApproved_TransitionsToActive()
    {
        var service = new EvolutionOrchestrationService();
        var feedback = new FeedbackRecord(4, "Ops", "Rollout", "Enable staged activation", DateTime.UtcNow);
        var created = service.CreateFromFeedback(feedback);
        service.UpdateStatus(created.Id, "Approved");

        var activated = service.Activate(created.Id);

        Assert.Equal("Active", activated.Status);
        Assert.Equal("Active", service.GetById(created.Id)?.Status);
    }

    [Fact]
    public void Activate_WhenNotApproved_ThrowsInvalidOperationException()
    {
        var service = new EvolutionOrchestrationService();
        var feedback = new FeedbackRecord(5, "Ops", "Rollout", "Activate directly", DateTime.UtcNow);
        var created = service.CreateFromFeedback(feedback);

        var action = () => service.Activate(created.Id);

        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void Rollback_WhenActive_TransitionsToRolledBack()
    {
        var service = new EvolutionOrchestrationService();
        var feedback = new FeedbackRecord(6, "Ops", "Rollout", "Rollback on regression", DateTime.UtcNow);
        var created = service.CreateFromFeedback(feedback);
        service.UpdateStatus(created.Id, "Approved");
        service.Activate(created.Id);

        var rolledBack = service.Rollback(created.Id);

        Assert.Equal("RolledBack", rolledBack.Status);
        Assert.Equal("RolledBack", service.GetById(created.Id)?.Status);
    }

    [Fact]
    public void Rollback_WhenNotActive_ThrowsInvalidOperationException()
    {
        var service = new EvolutionOrchestrationService();
        var feedback = new FeedbackRecord(7, "Ops", "Rollout", "Cannot rollback proposed", DateTime.UtcNow);
        var created = service.CreateFromFeedback(feedback);

        var action = () => service.Rollback(created.Id);

        Assert.Throws<InvalidOperationException>(action);
    }
}
