using System.Collections.Concurrent;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using SelfEvolving.AssetManagement.Web.Data;
using SelfEvolving.AssetManagement.Web.Models;

namespace SelfEvolving.AssetManagement.Web.Services;

public sealed class AssetInventoryService
{
    private readonly AssetManagementDbContext? _dbContext;
    private readonly ConcurrentDictionary<int, AssetRecord> _assetsById = new();
    private readonly ConcurrentDictionary<string, int> _assetIdByTag = new(StringComparer.OrdinalIgnoreCase);
    private int _nextId;

    public AssetInventoryService(AssetManagementDbContext? dbContext = null)
    {
        _dbContext = dbContext;
    }

    public IReadOnlyList<AssetRecord> GetAll() =>
        _dbContext is null
            ? _assetsById.Values.OrderBy(asset => asset.Id).ToArray()
            : _dbContext.Assets.AsNoTracking()
                .OrderBy(asset => asset.Id)
                .Select(asset => new AssetRecord(asset.Id, asset.AssetTag, asset.Name, asset.Category, asset.CreatedUtc))
                .ToArray();

    public AssetRecord? GetById(int id) =>
        _dbContext is null
            ? (_assetsById.TryGetValue(id, out var asset) ? asset : null)
            : _dbContext.Assets.AsNoTracking()
                .Where(asset => asset.Id == id)
                .Select(asset => new AssetRecord(asset.Id, asset.AssetTag, asset.Name, asset.Category, asset.CreatedUtc))
                .SingleOrDefault();

    public AssetRecord Create(CreateAssetRequest request)
    {
        var assetTag = request.AssetTag?.Trim();
        var name = request.Name?.Trim();
        var category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim();

        if (string.IsNullOrWhiteSpace(assetTag))
        {
            throw new ArgumentException("AssetTag is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.");
        }

        if (_dbContext is not null)
        {
            var exists = _dbContext.Assets.Any(existing => existing.AssetTag.ToLower() == assetTag.ToLower());
            if (exists)
            {
                throw new InvalidOperationException($"AssetTag '{assetTag}' already exists.");
            }

            var entity = new AssetEntity
            {
                AssetTag = assetTag,
                Name = name,
                Category = category,
                CreatedUtc = DateTime.UtcNow
            };

            _dbContext.Assets.Add(entity);
            _dbContext.SaveChanges();

            return new AssetRecord(entity.Id, entity.AssetTag, entity.Name, entity.Category, entity.CreatedUtc);
        }

        var id = Interlocked.Increment(ref _nextId);
        var asset = new AssetRecord(id, assetTag, name, category, DateTime.UtcNow);
        if (!_assetIdByTag.TryAdd(assetTag, id))
        {
            throw new InvalidOperationException($"AssetTag '{assetTag}' already exists.");
        }
        _assetsById[id] = asset;
        return asset;
    }
}
