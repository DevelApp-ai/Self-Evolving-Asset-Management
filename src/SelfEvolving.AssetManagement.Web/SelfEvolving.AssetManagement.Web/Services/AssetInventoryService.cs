using System.Collections.Concurrent;
using System.Threading;
using SelfEvolving.AssetManagement.Web.Models;

namespace SelfEvolving.AssetManagement.Web.Services;

public sealed class AssetInventoryService
{
    private readonly ConcurrentDictionary<int, AssetRecord> _assetsById = new();
    private readonly ConcurrentDictionary<string, int> _assetIdByTag = new(StringComparer.OrdinalIgnoreCase);
    private int _nextId;

    public IReadOnlyList<AssetRecord> GetAll() =>
        _assetsById.Values
            .OrderBy(asset => asset.Id)
            .ToArray();

    public AssetRecord? GetById(int id) =>
        _assetsById.TryGetValue(id, out var asset) ? asset : null;

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
