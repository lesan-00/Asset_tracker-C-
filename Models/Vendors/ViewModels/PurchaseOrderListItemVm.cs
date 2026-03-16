namespace AssetTracker.Models.Vendors.ViewModels;

public sealed class PurchaseOrderListItemVm
{
    public int Id { get; init; }
    public string PoNumber { get; init; } = string.Empty;
    public DateTime OrderDate { get; init; }
    public string VendorName { get; init; } = "-";
    public PurchaseOrderStatus Status { get; init; }
    public decimal TotalAmount { get; init; }
    public string Currency { get; init; } = "KES";
    public string LinkedPrNumber { get; init; } = "-";
    public string LinkedInvoiceNumber { get; init; } = "-";
}
