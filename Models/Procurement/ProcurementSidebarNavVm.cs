namespace AssetTracker.Models.Procurement;

public sealed class ProcurementSidebarNavVm
{
    public int TotalCount { get; init; }
    public int PurchaseRequestCount { get; init; }
    public int PurchaseOrderCount { get; init; }
    public int InvoiceCount { get; init; }
    public bool IsExpanded { get; init; }
    public bool IsAnyActive { get; init; }
    public bool IsPurchaseRequestsActive { get; init; }
    public bool IsPurchaseOrdersActive { get; init; }
    public bool IsInvoicesActive { get; init; }
    public bool IsNewRequestActive { get; init; }
}
