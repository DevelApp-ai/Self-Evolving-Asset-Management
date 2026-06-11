using System.Collections.Concurrent;
using System.Threading;
using SelfEvolving.AssetManagement.Web.Models;

namespace SelfEvolving.AssetManagement.Web.Services;

public sealed class AssetOwnershipService
{
    private readonly ConcurrentDictionary<int, List<AssetAssignmentRecord>> _assignmentsByAssetId = new();
    private readonly object _sync = new();
    private int _nextAssignmentId;

    public IReadOnlyList<AssetAssignmentRecord> GetAssignments(int assetId)
    {
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
