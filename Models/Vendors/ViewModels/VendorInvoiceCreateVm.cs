using System.ComponentModel.DataAnnotations;

namespace AssetTracker.Models.Vendors.ViewModels;

public sealed class VendorInvoiceCreateVm
{
    [Required]
    public int VendorId { get; set; }

    public int? PurchaseOrderId { get; set; }

    [Required]
    [StringLength(50)]
    public string InvoiceNumber { get; set; } = string.Empty;

    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime DueDate { get; set; } = DateTime.UtcNow.Date;

    [Range(0.01, 999999999999.99)]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(10)]
    public string Currency { get; set; } = "KES";

    [Required]
    public InvoicePaymentStatus PaymentStatus { get; set; } = InvoicePaymentStatus.Pending;

    [StringLength(2000)]
    public string? Notes { get; set; }
}
