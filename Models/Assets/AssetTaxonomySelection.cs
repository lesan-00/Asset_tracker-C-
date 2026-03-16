namespace AssetTracker.Models.Assets;

public sealed class AssetTaxonomySelection
{
    public string? TopLevelCategory { get; init; }
    public string? Category { get; init; }
    public string? SubCategory { get; init; }

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(TopLevelCategory)
        && string.IsNullOrWhiteSpace(Category)
        && string.IsNullOrWhiteSpace(SubCategory);
}
