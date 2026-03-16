using System.ComponentModel.DataAnnotations;

namespace AssetTracker.Models.Assets;

public class CreateSubscriptionVm
{
    [Required]
    [StringLength(50)]
    public string AssetTag { get; set; } = string.Empty;

    [Required]
    [StringLength(160)]
    public string Name { get; set; } = string.Empty;

    public int? VendorId { get; set; }

    [Required]
    public SoftwareBillingCycle BillingCycle { get; set; } = SoftwareBillingCycle.Monthly;

    public DateTime? StartDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool AutoRenew { get; set; }
    public DateTime? RenewalReminderDate { get; set; }

    [Range(0, 999999999999.99)]
    public decimal? Cost { get; set; }

    [StringLength(10)]
    public string Currency { get; set; } = "KES";

    [StringLength(100)]
    public string? InvoiceReference { get; set; }

    public int? PurchaseOrderId { get; set; }

    [StringLength(100)]
    public string? PurchaseOrderReference { get; set; }

    [StringLength(160)]
    public string? AssignedToUserOrDepartment { get; set; }

    [StringLength(160)]
    public string? InstalledOn { get; set; }

    [StringLength(100)]
    public string? Location { get; set; }

    [Required]
    public SoftwareStatus Status { get; set; } = SoftwareStatus.Active;

    [StringLength(2000)]
    public string? Notes { get; set; }
}
