using AssetTracker.Models;
using AssetTracker.Models.Assets;
using Microsoft.AspNetCore.Mvc;

namespace AssetTracker.Services;

public interface IAssetTaxonomyService
{
    AssetTaxonomySelection InferSelectionFromAssetType(AssetType assetType);

    AssetTaxonomySelection ResolveAssetSelection(Asset asset);

    bool TryNormalizeSelection(
        string? topLevelCategory,
        string? category,
        string? subCategory,
        out AssetTaxonomySelection normalizedSelection,
        out string? validationError);

    IReadOnlyList<AssetTaxonomyOptionVm> GetOptionsTree();

    IReadOnlyList<AssetNavNodeVm> BuildNavigationTree(
        IReadOnlyList<AssetTaxonomyCountRow> rows,
        AssetTaxonomySelection selected,
        IUrlHelper urlHelper);

    IReadOnlyList<AssetBreadcrumbVm> BuildBreadcrumbs(
        AssetTaxonomySelection selected,
        IUrlHelper urlHelper);

    string BuildHeading(AssetTaxonomySelection selected);
}
