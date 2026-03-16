namespace AssetTracker.Models.Vendors.ViewModels;

public sealed class VendorIndexVm
{
    public string ArchiveView { get; init; } = "active";
    public string? Query { get; init; }
    public VendorCategory? Category { get; init; }
    public VendorStatus? Status { get; init; }
    public bool PreferredOnly { get; init; }
    public VendorKpiVm Kpis { get; init; } = new();
    public IReadOnlyList<VendorListItemVm> Items { get; init; } = [];
}
