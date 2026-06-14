using Microsoft.EntityFrameworkCore;

namespace SelfEvolving.AssetManagement.Web.Data;

public sealed class AssetManagementDbContext(DbContextOptions<AssetManagementDbContext> options) : DbContext(options)
{
    public DbSet<AssetEntity> Assets => Set<AssetEntity>();
    public DbSet<AssetAssignmentEntity> AssetAssignments => Set<AssetAssignmentEntity>();
    public DbSet<FeedbackEntity> Feedback => Set<FeedbackEntity>();
    public DbSet<EvolutionCandidateEntity> EvolutionCandidates => Set<EvolutionCandidateEntity>();
    public DbSet<EvolutionTelemetryEntity> EvolutionTelemetry => Set<EvolutionTelemetryEntity>();
    public DbSet<EvolutionFitnessEntity> EvolutionFitness => Set<EvolutionFitnessEntity>();
    public DbSet<EvolutionAgentRunEntity> EvolutionAgentRuns => Set<EvolutionAgentRunEntity>();
    public DbSet<EvolutionAgentStepEntity> EvolutionAgentSteps => Set<EvolutionAgentStepEntity>();
    public DbSet<EvolutionAgentDecisionEntity> EvolutionAgentDecisions => Set<EvolutionAgentDecisionEntity>();
    public DbSet<EvolutionRunAuditLinkEntity> EvolutionRunAuditLinks => Set<EvolutionRunAuditLinkEntity>();
    public DbSet<EvolutionApprovalEntity> EvolutionApprovals => Set<EvolutionApprovalEntity>();
    public DbSet<EvolutionLifecycleEventEntity> EvolutionLifecycleEvents => Set<EvolutionLifecycleEventEntity>();
    public DbSet<PolicyDecisionAuditEntity> PolicyDecisionAudits => Set<PolicyDecisionAuditEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EvolutionTelemetryEntity>()
            .HasKey(x => x.CandidateId);

        modelBuilder.Entity<EvolutionFitnessEntity>()
            .HasKey(x => x.CandidateId);

        modelBuilder.Entity<AssetEntity>()
            .HasIndex(x => x.AssetTag)
            .IsUnique();

        modelBuilder.Entity<EvolutionCandidateEntity>()
            .HasIndex(x => x.SourceFeedbackId)
            .IsUnique();

        modelBuilder.Entity<EvolutionAgentRunEntity>()
            .HasIndex(x => x.SourceFeedbackId)
            .IsUnique();

        modelBuilder.Entity<EvolutionAgentRunEntity>()
            .HasIndex(x => x.CandidateId);

        modelBuilder.Entity<EvolutionAgentStepEntity>()
            .HasIndex(x => x.RunId);

        modelBuilder.Entity<EvolutionAgentDecisionEntity>()
            .HasIndex(x => x.RunId);

        modelBuilder.Entity<EvolutionRunAuditLinkEntity>()
            .HasIndex(x => x.CandidateId);
    }
}
