using AssetTracker.Models;

namespace AssetTracker.Models.Assets;

public sealed class AssetIndexVm
{
    public string? Query { get; init; }
    public string Heading { get; init; } = "Assets";
    public int TotalCount { get; init; }
    public bool IsTreeExpanded { get; init; } = true;
    public bool IsSoftwareScope { get; init; }
    public bool IsPhysicalScope { get; init; }
    public int? VendorId { get; init; }
    public SoftwareStatus? SoftwareStatus { get; init; }
    public bool ExpiringSoon { get; init; }
    public SoftwareBillingCycle? BillingCycle { get; init; }
    public bool? AutoRenew { get; init; }
    public SoftwareDeploymentEnvironment? DeploymentEnvironment { get; init; }
    public string? AssignedTo { get; init; }
    public string? InstalledOn { get; init; }
    public AssetTaxonomySelection Selection { get; init; } = new();
    public IReadOnlyList<AssetBreadcrumbVm> Breadcrumbs { get; init; } = [];
    public IReadOnlyList<AssetNavNodeVm> NavigationTree { get; init; } = [];
    public IReadOnlyList<Asset> Items { get; init; } = [];
}
