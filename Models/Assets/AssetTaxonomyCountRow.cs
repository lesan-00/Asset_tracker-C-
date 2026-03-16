using AssetTracker.Models;

namespace AssetTracker.Models.Assets;

public sealed class AssetTaxonomyCountRow
{
    public string? TopLevelCategory { get; init; }
    public string? Category { get; init; }
    public string? SubCategory { get; init; }
    public AssetType AssetType { get; init; }
    public int Count { get; init; } = 1;
}
