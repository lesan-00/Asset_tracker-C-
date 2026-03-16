namespace AssetTracker.Models.Vendors.ViewModels;

public sealed class PurchaseRequestDetailsVm
{
    public PurchaseRequest Request { get; init; } = default!;
    public IReadOnlyList<PurchaseRequestLine> Lines { get; init; } = [];
    public PurchaseOrder? LinkedPurchaseOrder { get; init; }
    public string RequestedByLabel { get; init; } = "-";
    public string ApprovedByLabel { get; init; } = "-";
    public decimal EstimatedTotal { get; init; }
}
