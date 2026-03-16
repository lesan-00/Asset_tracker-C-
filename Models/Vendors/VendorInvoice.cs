using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace AssetTracker.Models.Vendors;

[Index(nameof(VendorId), nameof(InvoiceNumber), IsUnique = true)]
public class VendorInvoice
{
    public int Id { get; set; }

    [Required]
    public int VendorId { get; set; }

    [Required]
    [StringLength(50)]
    public string InvoiceNumber { get; set; } = string.Empty;

    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;

    public DateTime DueDate { get; set; } = DateTime.UtcNow.Date;

    [Range(0.01, 999999999999.99)]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(10)]
    public string Currency { get; set; } = "KES";

    [Required]
    public InvoicePaymentStatus PaymentStatus { get; set; } = InvoicePaymentStatus.Pending;

    public DateTime? PaidDate { get; set; }

    public int? PurchaseOrderId { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Vendor Vendor { get; set; } = default!;
    public PurchaseOrder? PurchaseOrder { get; set; }
}
