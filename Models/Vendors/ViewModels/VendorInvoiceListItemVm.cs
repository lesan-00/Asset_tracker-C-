namespace AssetTracker.Models.Vendors.ViewModels;

public sealed class VendorInvoiceListItemVm
{
    public int Id { get; init; }
    public int VendorId { get; init; }
    public string InvoiceNumber { get; init; } = string.Empty;
    public DateTime InvoiceDate { get; init; }
    public DateTime DueDate { get; init; }
    public InvoicePaymentStatus PaymentStatus { get; init; }
    public InvoicePaymentStatus EffectiveStatus { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "KES";
    public string VendorName { get; init; } = "-";
    public string LinkedPoNumber { get; init; } = "-";
}
