using SelfEvolving.AssetManagement.Web.Models;
using SelfEvolving.AssetManagement.Web.Configuration;
using SelfEvolving.AssetManagement.Web.Services;
using Microsoft.Extensions.Options;

namespace SelfEvolving.AssetManagement.Web.Tests;

public class EvolutionOrchestrationServiceTests
{
    [Fact]
    public void Constructor_WhenExecutionBudgetIsNonPositive_ThrowsArgumentOutOfRangeException()
    {
        var options = Options.Create(new SystemArchitectureOptions
        {
            EvolutionExecutionBudgetMilliseconds = 0
        });

        var action = () => new EvolutionOrchestrationService(options);

        Assert.Throws<ArgumentOutOfRangeException>(action);
    }

    [Fact]
    public void Constructor_WhenMinimumFitnessScoreIsOutOfRange_ThrowsArgumentOutOfRangeException()
    {
        var options = Options.Create(new SystemArchitectureOptions
        {
            EvolutionMinimumFitnessScore = 1.5
        });

        var action = () => new EvolutionOrchestrationService(options);

        Assert.Throws<ArgumentOutOfRangeException>(action);
    }

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
    public void CreateFromFeedback_PersistsTelemetryWithConfiguredBudget()
    {
        var options = Options.Create(new SystemArchitectureOptions
        {
            EvolutionExecutionBudgetMilliseconds = 1234
        });
        var service = new EvolutionOrchestrationService(options);
        var feedback = new FeedbackRecord(20, "Ops", "Telemetry", "Persist telemetry for candidate generation", DateTime.UtcNow);

        var candidate = service.CreateFromFeedback(feedback);
        var telemetry = service.GetTelemetry(candidate.Id);

        Assert.NotNull(telemetry);
        Assert.Equal(candidate.Id, telemetry!.CandidateId);
        Assert.Equal(1234, telemetry.ExecutionBudgetMilliseconds);
        Assert.False(telemetry.TimedOut);
        Assert.False(telemetry.CanceledByCaller);
        Assert.True(telemetry.TotalDurationMilliseconds >= 0);
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
        service.SetFitnessEvaluation(created.Id, new CreateEvolutionFitnessEvaluationRequest(0.9, "fitness-bot", null));

        var activated = service.Activate(created.Id);

        Assert.Equal("Active", activated.Status);
        Assert.Equal("Internal", activated.RolloutStage);
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
    public void Activate_WhenFitnessIsMissing_ThrowsInvalidOperationException()
    {
        var service = new EvolutionOrchestrationService();
        var feedback = new FeedbackRecord(21, "Ops", "Rollout", "Require quality gate", DateTime.UtcNow);
        var created = service.CreateFromFeedback(feedback);
        service.UpdateStatus(created.Id, "Approved");

        var action = () => service.Activate(created.Id);

        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void Activate_WhenFitnessBelowThreshold_ThrowsInvalidOperationException()
    {
        var service = new EvolutionOrchestrationService();
        var feedback = new FeedbackRecord(22, "Ops", "Rollout", "Block low fitness candidate", DateTime.UtcNow);
        var created = service.CreateFromFeedback(feedback);
        service.UpdateStatus(created.Id, "Approved");
        service.SetFitnessEvaluation(created.Id, new CreateEvolutionFitnessEvaluationRequest(0.5, "fitness-bot", "below threshold"));

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
        service.SetFitnessEvaluation(created.Id, new CreateEvolutionFitnessEvaluationRequest(0.9, "fitness-bot", null));
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

    [Fact]
    public void PromoteRollout_WhenActive_TransitionsFromInternalToPilot()
    {
        var service = new EvolutionOrchestrationService();
        var feedback = new FeedbackRecord(8, "Ops", "Rollout", "Promote to pilot", DateTime.UtcNow);
        var created = service.CreateFromFeedback(feedback);
        service.UpdateStatus(created.Id, "Approved");
        service.SetFitnessEvaluation(created.Id, new CreateEvolutionFitnessEvaluationRequest(0.9, "fitness-bot", null));
        service.Activate(created.Id);

        var promoted = service.PromoteRollout(created.Id);

        Assert.Equal("Pilot", promoted.RolloutStage);
    }

    [Fact]
    public void PromoteRollout_WhenPilot_TransitionsToFull()
    {
        var service = new EvolutionOrchestrationService();
        var feedback = new FeedbackRecord(9, "Ops", "Rollout", "Promote to full", DateTime.UtcNow);
        var created = service.CreateFromFeedback(feedback);
        service.UpdateStatus(created.Id, "Approved");
        service.SetFitnessEvaluation(created.Id, new CreateEvolutionFitnessEvaluationRequest(0.9, "fitness-bot", null));
        service.Activate(created.Id);
        service.PromoteRollout(created.Id);

        var promoted = service.PromoteRollout(created.Id);

        Assert.Equal("Full", promoted.RolloutStage);
    }

    [Fact]
    public void PromoteRollout_WhenNotActive_ThrowsInvalidOperationException()
    {
        var service = new EvolutionOrchestrationService();
        var feedback = new FeedbackRecord(10, "Ops", "Rollout", "Must activate first", DateTime.UtcNow);
        var created = service.CreateFromFeedback(feedback);

        var action = () => service.PromoteRollout(created.Id);

        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void PromoteRollout_WhenAlreadyFull_ThrowsInvalidOperationException()
    {
        var service = new EvolutionOrchestrationService();
        var feedback = new FeedbackRecord(11, "Ops", "Rollout", "No more promotions after full", DateTime.UtcNow);
        var created = service.CreateFromFeedback(feedback);
        service.UpdateStatus(created.Id, "Approved");
        service.SetFitnessEvaluation(created.Id, new CreateEvolutionFitnessEvaluationRequest(0.9, "fitness-bot", null));
        service.Activate(created.Id);
        service.PromoteRollout(created.Id);
        service.PromoteRollout(created.Id);

        var action = () => service.PromoteRollout(created.Id);

        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void PromoteRollout_WhenFitnessFallsBelowThreshold_ThrowsInvalidOperationException()
    {
        var service = new EvolutionOrchestrationService();
        var feedback = new FeedbackRecord(23, "Ops", "Rollout", "Block stage promotion on quality regression", DateTime.UtcNow);
        var created = service.CreateFromFeedback(feedback);
        service.UpdateStatus(created.Id, "Approved");
        service.SetFitnessEvaluation(created.Id, new CreateEvolutionFitnessEvaluationRequest(0.9, "fitness-bot", null));
        service.Activate(created.Id);
        service.SetFitnessEvaluation(created.Id, new CreateEvolutionFitnessEvaluationRequest(0.4, "fitness-bot", "regressed"));

        var action = () => service.PromoteRollout(created.Id);

        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void Release_WhenActiveAndFull_TransitionsToReleased()
    {
        var service = new EvolutionOrchestrationService();
        var feedback = new FeedbackRecord(12, "Ops", "Release", "Release successful rollout", DateTime.UtcNow);
        var created = service.CreateFromFeedback(feedback);
        service.UpdateStatus(created.Id, "Approved");
        service.SetFitnessEvaluation(created.Id, new CreateEvolutionFitnessEvaluationRequest(0.9, "fitness-bot", null));
        service.Activate(created.Id);
        service.PromoteRollout(created.Id);
        service.PromoteRollout(created.Id);

        var released = service.Release(created.Id);

        Assert.Equal("Released", released.Status);
        Assert.Equal("Full", released.RolloutStage);
    }

    [Fact]
    public void Release_WhenNotFull_ThrowsInvalidOperationException()
    {
        var service = new EvolutionOrchestrationService();
        var feedback = new FeedbackRecord(13, "Ops", "Release", "Cannot release before full rollout", DateTime.UtcNow);
        var created = service.CreateFromFeedback(feedback);
        service.UpdateStatus(created.Id, "Approved");
        service.SetFitnessEvaluation(created.Id, new CreateEvolutionFitnessEvaluationRequest(0.9, "fitness-bot", null));
        service.Activate(created.Id);

        var action = () => service.Release(created.Id);

        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void Release_WhenNotActive_ThrowsInvalidOperationException()
    {
        var service = new EvolutionOrchestrationService();
        var feedback = new FeedbackRecord(14, "Ops", "Release", "Cannot release before activation", DateTime.UtcNow);
        var created = service.CreateFromFeedback(feedback);

        var action = () => service.Release(created.Id);

        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void Release_WhenFitnessFallsBelowThreshold_ThrowsInvalidOperationException()
    {
        var service = new EvolutionOrchestrationService();
        var feedback = new FeedbackRecord(24, "Ops", "Release", "Do not release regressed candidate", DateTime.UtcNow);
        var created = service.CreateFromFeedback(feedback);
        service.UpdateStatus(created.Id, "Approved");
        service.SetFitnessEvaluation(created.Id, new CreateEvolutionFitnessEvaluationRequest(0.9, "fitness-bot", null));
        service.Activate(created.Id);
        service.PromoteRollout(created.Id);
        service.PromoteRollout(created.Id);
        service.SetFitnessEvaluation(created.Id, new CreateEvolutionFitnessEvaluationRequest(0.3, "fitness-bot", "regressed before release"));

        var action = () => service.Release(created.Id);

        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void AutoRollbackOnRegression_WhenActive_TransitionsToRolledBack()
    {
        var service = new EvolutionOrchestrationService();
        var feedback = new FeedbackRecord(15, "Ops", "Rollback", "Regression detected in pilot", DateTime.UtcNow);
        var created = service.CreateFromFeedback(feedback);
        service.UpdateStatus(created.Id, "Approved");
        service.SetFitnessEvaluation(created.Id, new CreateEvolutionFitnessEvaluationRequest(0.9, "fitness-bot", null));
        service.Activate(created.Id);

        var rolledBack = service.AutoRollbackOnRegression(created.Id);

        Assert.Equal("RolledBack", rolledBack.Status);
        Assert.Equal("RolledBack", service.GetById(created.Id)?.Status);
    }

    [Fact]
    public void AutoRollbackOnRegression_WhenNotActive_ThrowsInvalidOperationException()
    {
        var service = new EvolutionOrchestrationService();
        var feedback = new FeedbackRecord(16, "Ops", "Rollback", "Cannot auto-rollback proposed", DateTime.UtcNow);
        var created = service.CreateFromFeedback(feedback);

        var action = () => service.AutoRollbackOnRegression(created.Id);

        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void Retire_WhenReleased_TransitionsToRetired()
    {
        var service = new EvolutionOrchestrationService();
        var feedback = new FeedbackRecord(17, "Ops", "Retire", "Retire released candidate", DateTime.UtcNow);
        var created = service.CreateFromFeedback(feedback);
        service.UpdateStatus(created.Id, "Approved");
        service.SetFitnessEvaluation(created.Id, new CreateEvolutionFitnessEvaluationRequest(0.9, "fitness-bot", null));
        service.Activate(created.Id);
        service.PromoteRollout(created.Id);
        service.PromoteRollout(created.Id);
        service.Release(created.Id);

        var retired = service.Retire(created.Id);

        Assert.Equal("Retired", retired.Status);
    }

    [Fact]
    public void Retire_WhenRolledBack_TransitionsToRetired()
    {
        var service = new EvolutionOrchestrationService();
        var feedback = new FeedbackRecord(18, "Ops", "Retire", "Retire rolled back candidate", DateTime.UtcNow);
        var created = service.CreateFromFeedback(feedback);
        service.UpdateStatus(created.Id, "Approved");
        service.SetFitnessEvaluation(created.Id, new CreateEvolutionFitnessEvaluationRequest(0.9, "fitness-bot", null));
        service.Activate(created.Id);
        service.Rollback(created.Id);

        var retired = service.Retire(created.Id);

        Assert.Equal("Retired", retired.Status);
    }

    [Fact]
    public void Retire_WhenStatusIsInvalid_ThrowsInvalidOperationException()
    {
        var service = new EvolutionOrchestrationService();
        var feedback = new FeedbackRecord(19, "Ops", "Retire", "Cannot retire proposed", DateTime.UtcNow);
        var created = service.CreateFromFeedback(feedback);

        var action = () => service.Retire(created.Id);

        Assert.Throws<InvalidOperationException>(action);
    }
}
