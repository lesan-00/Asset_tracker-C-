namespace AssetTracker.Models.Assets;

public sealed class AssetNavNodeVm
{
    public string Name { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public string? Icon { get; init; }
    public int Count { get; init; }
    public int Depth { get; init; }
    public string NodeId { get; init; } = string.Empty;
    public string NavigationUrl { get; init; } = string.Empty;
    public bool IsExpanded { get; init; }
    public bool IsSelected { get; init; }
    public bool HasSelectedDescendant { get; init; }
    public IReadOnlyList<AssetNavNodeVm> Children { get; init; } = [];

    // Alias properties for partials that use generic sidebar naming conventions.
    public string Url => NavigationUrl;
    public bool IsActive => IsSelected;
}
