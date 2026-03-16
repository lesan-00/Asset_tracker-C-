namespace AssetTracker.Models.Vendors.ViewModels;

public sealed class PurchaseRequestListItemVm
{
    public int Id { get; init; }
    public string PrNumber { get; init; } = string.Empty;
    public DateTime RequestDate { get; init; }
    public string RequestedBy { get; init; } = "-";
    public string Department { get; init; } = "-";
    public PurchaseRequestPriority Priority { get; init; }
    public PurchaseRequestStatus Status { get; init; }
    public DateTime? NeededByDate { get; init; }
    public decimal EstimatedTotal { get; init; }
    public string? LinkedPoNumber { get; init; }
    public int? LinkedPurchaseOrderId { get; init; }
    public bool CanEdit { get; init; }
}
