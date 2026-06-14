using System.Collections.Concurrent;
using System.Threading;
using SelfEvolving.AssetManagement.Web.Models;

namespace SelfEvolving.AssetManagement.Web.Services;

public sealed class PolicyDecisionAuditService
{
    private readonly ConcurrentDictionary<int, PolicyDecisionAuditRecord> _recordsById = new();
    private int _nextId;

    public IReadOnlyList<PolicyDecisionAuditRecord> GetAll() =>
        _recordsById.Values
            .OrderByDescending(x => x.Id)
            .ToArray();

    public PolicyDecisionAuditRecord RecordAssetCreate(CreateAssetRequest request, PolicyDecision decision)
    {
        var id = Interlocked.Increment(ref _nextId);
        var category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim();
        var record = new PolicyDecisionAuditRecord(
            id,
            Operation: "AssetCreate",
            Allowed: decision.Allowed,
            AssetTag: request.AssetTag.Trim(),
            Name: request.Name.Trim(),
            Category: category,
            DenyReasons: decision.DenyReasons.ToArray(),
            EvaluatedUtc: DateTime.UtcNow);

        _recordsById[id] = record;
        return record;
    }
}
