using AssetTracker.Data;
using AssetTracker.Models.Assets;
using AssetTracker.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetTracker.ViewComponents;

public sealed class AssetSidebarNavViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _context;
    private readonly IAssetTaxonomyService _assetTaxonomyService;

    public AssetSidebarNavViewComponent(
        ApplicationDbContext context,
        IAssetTaxonomyService assetTaxonomyService)
    {
        _context = context;
        _assetTaxonomyService = assetTaxonomyService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var currentController = ViewContext.RouteData.Values["controller"]?.ToString();
        var isAssetsPage = string.Equals(currentController, "Assets", StringComparison.OrdinalIgnoreCase);

        var group = HttpContext?.Request.Query["group"].ToString();
        var category = HttpContext?.Request.Query["category"].ToString();
        var subCategory = HttpContext?.Request.Query["subCategory"].ToString();
        if (!_assetTaxonomyService.TryNormalizeSelection(group, category, subCategory, out var selection, out _))
        {
            selection = new AssetTaxonomySelection();
        }

        var countRows = await _context.Assets
            .AsNoTracking()
            .GroupBy(a => new { a.TopLevelCategory, a.Category, a.SubCategory, a.AssetType })
            .Select(g => new AssetTaxonomyCountRow
            {
                TopLevelCategory = g.Key.TopLevelCategory,
                Category = g.Key.Category,
                SubCategory = g.Key.SubCategory,
                AssetType = g.Key.AssetType,
                Count = g.Count()
            })
            .ToListAsync();

        var model = new AssetSidebarNavVm
        {
            TotalCount = countRows.Sum(x => x.Count),
            IsExpanded = isAssetsPage || !selection.IsEmpty,
            IsActive = isAssetsPage,
            Nodes = _assetTaxonomyService.BuildNavigationTree(countRows, selection, Url)
        };

        return View(model);
    }
}
