using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace AssetTracker.Models.Vendors;

[Index(nameof(VendorCode), IsUnique = true)]
public class Vendor
{
    public int Id { get; set; }

    [Required]
    [StringLength(20)]
    public string VendorCode { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    [Required]
    public VendorCategory Category { get; set; } = VendorCategory.Other;

    [Url]
    [StringLength(255)]
    public string? Website { get; set; }

    [EmailAddress]
    [StringLength(150)]
    public string? Email { get; set; }

    [StringLength(50)]
    public string? Phone { get; set; }

    [StringLength(400)]
    public string? PhysicalAddress { get; set; }

    [StringLength(120)]
    public string? AccountManagerName { get; set; }

    [EmailAddress]
    [StringLength(150)]
    public string? AccountManagerEmail { get; set; }

    [StringLength(50)]
    public string? AccountManagerPhone { get; set; }

    [Required]
    public VendorStatus Status { get; set; } = VendorStatus.Active;

    public bool IsPreferredVendor { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    public bool IsArchived { get; set; }
    public DateTime? ArchivedAt { get; set; }

    [StringLength(255)]
    public string? ArchivedByUserId { get; set; }

    [StringLength(500)]
    public string? ArchiveReason { get; set; }

    public ICollection<AssetTracker.Models.Asset> Assets { get; set; } = new List<AssetTracker.Models.Asset>();
    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
    public ICollection<VendorInvoice> Invoices { get; set; } = new List<VendorInvoice>();
    public ICollection<VendorContract> Contracts { get; set; } = new List<VendorContract>();
    public ICollection<VendorSupportTicket> SupportTickets { get; set; } = new List<VendorSupportTicket>();
    public ICollection<VendorDocument> Documents { get; set; } = new List<VendorDocument>();
}
