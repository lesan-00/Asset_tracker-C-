namespace AssetTracker.Models.Assets;

public sealed class AssetSidebarNavVm
{
    public int TotalCount { get; init; }
    public bool IsExpanded { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<AssetNavNodeVm> Nodes { get; init; } = [];
}
