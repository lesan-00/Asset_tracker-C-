namespace AssetTracker.Models.Assets;

public sealed class AssetBreadcrumbVm
{
    public string Label { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}
