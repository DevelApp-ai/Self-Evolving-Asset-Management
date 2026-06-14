using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using SelfEvolving.AssetManagement.Web.Data;
using SelfEvolving.AssetManagement.Web.Models;

namespace SelfEvolving.AssetManagement.Web.Services;

public sealed class PolicyDecisionAuditService
{
    private readonly AssetManagementDbContext? _dbContext;
    private readonly ConcurrentDictionary<int, PolicyDecisionAuditRecord> _recordsById = new();
    private int _nextId;

    public PolicyDecisionAuditService(AssetManagementDbContext? dbContext = null)
    {
        _dbContext = dbContext;
    }

    public IReadOnlyList<PolicyDecisionAuditRecord> GetAll() =>
        _dbContext is null
            ? _recordsById.Values.OrderByDescending(x => x.Id).ToArray()
            : _dbContext.PolicyDecisionAudits.AsNoTracking()
                .OrderByDescending(x => x.Id)
                .ToArray()
                .Select(x => new PolicyDecisionAuditRecord(
                    x.Id,
                    x.Operation,
                    x.Allowed,
                    x.AssetTag,
                    x.Name,
                    x.Category,
                    JsonSerializer.Deserialize<string[]>(x.DenyReasonsJson) ?? [],
                    x.PolicyVersion,
                    x.PolicySource,
                    x.EvaluatedUtc))
                .ToArray();

    public PolicyDecisionAuditRecord RecordAssetCreate(CreateAssetRequest request, PolicyDecision decision)
    {
        var category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim();
        if (_dbContext is not null)
        {
            var entity = new PolicyDecisionAuditEntity
            {
                Operation = "AssetCreate",
                Allowed = decision.Allowed,
                AssetTag = request.AssetTag.Trim(),
                Name = request.Name.Trim(),
                Category = category,
                DenyReasonsJson = JsonSerializer.Serialize(decision.DenyReasons),
                PolicyVersion = decision.PolicyVersion,
                PolicySource = decision.PolicySource,
                EvaluatedUtc = DateTime.UtcNow
            };

            _dbContext.PolicyDecisionAudits.Add(entity);
            _dbContext.SaveChanges();

            return new PolicyDecisionAuditRecord(
                entity.Id,
                entity.Operation,
                entity.Allowed,
                entity.AssetTag,
                entity.Name,
                entity.Category,
                decision.DenyReasons.ToArray(),
                entity.PolicyVersion,
                entity.PolicySource,
                entity.EvaluatedUtc);
        }

        var id = Interlocked.Increment(ref _nextId);
        var record = new PolicyDecisionAuditRecord(
            id,
            Operation: "AssetCreate",
            Allowed: decision.Allowed,
            AssetTag: request.AssetTag.Trim(),
            Name: request.Name.Trim(),
            Category: category,
            DenyReasons: decision.DenyReasons.ToArray(),
            PolicyVersion: decision.PolicyVersion,
            PolicySource: decision.PolicySource,
            EvaluatedUtc: DateTime.UtcNow);

        _recordsById[id] = record;
        return record;
    }
}
