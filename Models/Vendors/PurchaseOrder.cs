using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace AssetTracker.Models.Vendors;

[Index(nameof(PoNumber), IsUnique = true)]
public class PurchaseOrder
{
    public int Id { get; set; }

    [Required]
    [StringLength(20)]
    public string PoNumber { get; set; } = string.Empty;

    [Required]
    public int VendorId { get; set; }

    public int? PurchaseRequestId { get; set; }

    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    [Range(0, 999999999999.99)]
    public decimal TotalAmount { get; set; }

    [Required]
    [StringLength(10)]
    public string Currency { get; set; } = "KES";

    [Required]
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

    [StringLength(2000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Vendor Vendor { get; set; } = default!;
    public PurchaseRequest? PurchaseRequest { get; set; }
    public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();
    public ICollection<VendorInvoice> Invoices { get; set; } = new List<VendorInvoice>();
}
