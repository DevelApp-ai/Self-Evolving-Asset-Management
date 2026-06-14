using System.Collections.Concurrent;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using SelfEvolving.AssetManagement.Web.Data;
using SelfEvolving.AssetManagement.Web.Models;

namespace SelfEvolving.AssetManagement.Web.Services;

public sealed class EvolutionApprovalService
{
    private static readonly HashSet<string> AllowedDecisions = ["Approve", "Reject"];
    private readonly AssetManagementDbContext? _dbContext;
    private readonly ConcurrentDictionary<int, List<EvolutionApprovalRecord>> _approvalsByCandidateId = new();
    private readonly object _sync = new();
    private int _nextApprovalId;

    public EvolutionApprovalService(AssetManagementDbContext? dbContext = null)
    {
        _dbContext = dbContext;
    }

    public IReadOnlyList<EvolutionApprovalRecord> GetApprovals(int candidateId)
    {
        if (_dbContext is not null)
        {
            return _dbContext.EvolutionApprovals.AsNoTracking()
                .Where(x => x.CandidateId == candidateId)
                .OrderByDescending(x => x.Id)
                .Select(x => new EvolutionApprovalRecord(
                    x.Id,
                    x.CandidateId,
                    x.Decision,
                    x.ReviewerId,
                    x.Notes,
                    x.ReviewedUtc))
                .ToArray();
        }

        if (!_approvalsByCandidateId.TryGetValue(candidateId, out var approvals))
        {
            return [];
        }

        return approvals.OrderByDescending(x => x.Id).ToArray();
    }

    public EvolutionApprovalRecord CreateApproval(int candidateId, CreateEvolutionApprovalRequest request)
    {
        var reviewerId = request.ReviewerId?.Trim();
        var notes = request.Notes?.Trim();
        var decision = request.Decision?.Trim();

        if (string.IsNullOrWhiteSpace(reviewerId))
        {
            throw new ArgumentException("ReviewerId is required.");
        }

        if (string.IsNullOrWhiteSpace(decision))
        {
            throw new ArgumentException("Decision is required.");
        }

        var normalizedDecision = char.ToUpperInvariant(decision[0]) + decision[1..].ToLowerInvariant();
        if (!AllowedDecisions.Contains(normalizedDecision))
        {
            throw new ArgumentException("Decision must be either Approve or Reject.");
        }

        if (_dbContext is not null)
        {
            var hasReview = _dbContext.EvolutionApprovals.Any(x => x.CandidateId == candidateId);
            if (hasReview)
            {
                throw new InvalidOperationException($"Candidate '{candidateId}' has already been reviewed.");
            }

            var createdEntity = new EvolutionApprovalEntity
            {
                CandidateId = candidateId,
                Decision = normalizedDecision,
                ReviewerId = reviewerId,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes,
                ReviewedUtc = DateTime.UtcNow
            };

            _dbContext.EvolutionApprovals.Add(createdEntity);
            _dbContext.SaveChanges();

            return new EvolutionApprovalRecord(
                createdEntity.Id,
                createdEntity.CandidateId,
                createdEntity.Decision,
                createdEntity.ReviewerId,
                createdEntity.Notes,
                createdEntity.ReviewedUtc);
        }

        lock (_sync)
        {
            var existing = _approvalsByCandidateId.GetOrAdd(candidateId, _ => []);
            if (existing.Count > 0)
            {
                throw new InvalidOperationException($"Candidate '{candidateId}' has already been reviewed.");
            }

            var approvalId = Interlocked.Increment(ref _nextApprovalId);
            var created = new EvolutionApprovalRecord(
                approvalId,
                candidateId,
                normalizedDecision,
                reviewerId,
                string.IsNullOrWhiteSpace(notes) ? null : notes,
                DateTime.UtcNow);

            existing.Add(created);
            return created;
        }
    }
}
