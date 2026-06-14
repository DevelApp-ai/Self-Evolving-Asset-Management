using System.Collections.Concurrent;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using SelfEvolving.AssetManagement.Web.Data;
using SelfEvolving.AssetManagement.Web.Models;

namespace SelfEvolving.AssetManagement.Web.Services;

public sealed class AssetOwnershipService
{
    private readonly AssetManagementDbContext? _dbContext;
    private readonly ConcurrentDictionary<int, List<AssetAssignmentRecord>> _assignmentsByAssetId = new();
    private readonly object _sync = new();
    private int _nextAssignmentId;

    public AssetOwnershipService(AssetManagementDbContext? dbContext = null)
    {
        _dbContext = dbContext;
    }

    public IReadOnlyList<AssetAssignmentRecord> GetAssignments(int assetId)
    {
        if (_dbContext is not null)
        {
            return _dbContext.AssetAssignments.AsNoTracking()
                .Where(x => x.AssetId == assetId)
                .OrderByDescending(x => x.Id)
                .Select(x => new AssetAssignmentRecord(
                    x.Id,
                    x.AssetId,
                    x.OwnerId,
                    x.OwnerName,
                    x.AssignedUtc,
                    x.IsActive))
                .ToArray();
        }

        if (!_assignmentsByAssetId.TryGetValue(assetId, out var assignments))
        {
            return [];
        }

        return assignments.OrderByDescending(x => x.Id).ToArray();
    }

    public AssetAssignmentRecord Assign(int assetId, CreateAssetAssignmentRequest request)
    {
        var ownerId = request.OwnerId?.Trim();
        var ownerName = request.OwnerName?.Trim();

        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new ArgumentException("OwnerId is required.");
        }

        if (string.IsNullOrWhiteSpace(ownerName))
        {
            throw new ArgumentException("OwnerName is required.");
        }

        if (_dbContext is not null)
        {
            var existingAssignments = _dbContext.AssetAssignments
                .Where(x => x.AssetId == assetId && x.IsActive)
                .ToArray();

            foreach (var existing in existingAssignments)
            {
                existing.IsActive = false;
            }

            var createdEntity = new AssetAssignmentEntity
            {
                AssetId = assetId,
                OwnerId = ownerId,
                OwnerName = ownerName,
                AssignedUtc = DateTime.UtcNow,
                IsActive = true
            };
            _dbContext.AssetAssignments.Add(createdEntity);
            _dbContext.SaveChanges();

            return new AssetAssignmentRecord(
                createdEntity.Id,
                createdEntity.AssetId,
                createdEntity.OwnerId,
                createdEntity.OwnerName,
                createdEntity.AssignedUtc,
                createdEntity.IsActive);
        }

        lock (_sync)
        {
            var assignmentId = Interlocked.Increment(ref _nextAssignmentId);
            var current = _assignmentsByAssetId.GetOrAdd(assetId, _ => []);
            var updatedAssignments = current
                .Select(existing => existing with { IsActive = false })
                .ToList();

            var created = new AssetAssignmentRecord(
                assignmentId,
                assetId,
                ownerId,
                ownerName,
                DateTime.UtcNow,
                IsActive: true);

            updatedAssignments.Add(created);
            _assignmentsByAssetId[assetId] = updatedAssignments;
            return created;
        }
    }
}
