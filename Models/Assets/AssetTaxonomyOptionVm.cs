namespace AssetTracker.Models.Assets;

public sealed class AssetTaxonomyOptionVm
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<AssetTaxonomyOptionVm> Children { get; init; } = [];
}
