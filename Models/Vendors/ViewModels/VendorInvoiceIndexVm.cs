namespace AssetTracker.Models.Vendors.ViewModels;

public sealed class VendorInvoiceIndexVm
{
    public string? Query { get; init; }
    public InvoicePaymentStatus? StatusFilter { get; init; }
    public int TotalInvoices { get; init; }
    public int PendingInvoices { get; init; }
    public int PaidInvoices { get; init; }
    public int OverdueInvoices { get; init; }
    public IReadOnlyList<VendorInvoiceListItemVm> Items { get; init; } = [];
}
