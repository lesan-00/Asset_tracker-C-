using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using AssetTracker.Models.Vendors;

namespace AssetTracker.Models;

public enum AssetType
{
    Software,
    Laptop,
    Printer,
    Switch,
    Router,
    Desktop,
    Monitor,
    Keyboard,
    [Display(Name = "Mobile Phone")]
    MobilePhone,
    [Display(Name = "System Unit")]
    SystemUnit,
    [Display(Name = "PDA")]
    Pda,
    [Display(Name = "Hcs Crane Scale")]
    HcsCraneScale,
    Chairs,
    Whiteboards,
    Desks,
    Cabinets,
    Tables,
    Drawers,
    Other
}

[Index(nameof(AssetTag), IsUnique = true)]
public class Asset
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string AssetTag { get; set; } = string.Empty;

    [StringLength(100)]
    public string SerialNumber { get; set; } = string.Empty;

    [Required]
    public AssetType AssetType { get; set; } = AssetType.Laptop;

    [Required]
    [StringLength(100)]
    public string Brand { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Model { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Specifications { get; set; }

    [Required]
    public global::AssetTracker.Models.AssetStatus Status { get; set; } = global::AssetTracker.Models.AssetStatus.InStock;

    [Required]
    [StringLength(100)]
    public string Location { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Condition { get; set; } = string.Empty;

    [StringLength(50)]
    public string? TopLevelCategory { get; set; }

    [StringLength(80)]
    public string? Category { get; set; }

    [StringLength(80)]
    public string? SubCategory { get; set; }

    public int? VendorId { get; set; }

    public DateTime? PurchaseDate { get; set; }

    public DateTime? IssueDate { get; set; }

    public DateTime? ReturnDate { get; set; }

    [StringLength(150)]
    public string? PreviousOwner { get; set; }

    [StringLength(50)]
    public string? AssetStatus { get; set; }

    public DateTime? WarrantyEndDate { get; set; }

    public SoftwareCategory? SoftwareCategory { get; set; }

    [StringLength(160)]
    public string? SoftwareName { get; set; }

    [StringLength(80)]
    public string? SoftwareVersion { get; set; }

    [StringLength(120)]
    public string? LicenseType { get; set; }

    [StringLength(255)]
    public string? LicenseKey { get; set; }

    [StringLength(160)]
    public string? AssignedToUserOrDepartment { get; set; }

    [StringLength(160)]
    public string? InstalledOn { get; set; }

    public DateTime? InstalledDate { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public DateTime? RenewalReminderDate { get; set; }

    [Range(0, 999999999999.99)]
    public decimal? Cost { get; set; }

    [Required]
    [StringLength(10)]
    public string Currency { get; set; } = "KES";

    [StringLength(100)]
    public string? InvoiceReference { get; set; }

    [StringLength(100)]
    public string? PurchaseOrderReference { get; set; }

    public SoftwareStatus? SoftwareStatus { get; set; }

    [StringLength(100)]
    public string? Edition { get; set; }

    [StringLength(60)]
    public string? BuildNumber { get; set; }

    public DateTime? SupportEndDate { get; set; }

    [StringLength(120)]
    public string? PatchStatus { get; set; }

    [StringLength(120)]
    public string? PlanOrTier { get; set; }

    public SoftwareBillingCycle? BillingCycle { get; set; }

    public bool AutoRenew { get; set; }

    [StringLength(120)]
    public string? OpenSourceLicenseType { get; set; }

    [StringLength(160)]
    public string? MaintainerOrVendor { get; set; }

    public SoftwareDeploymentEnvironment? DeploymentEnvironment { get; set; }

    [StringLength(120)]
    public string? SecurityStatus { get; set; }

    public DateTime? LastUpdatedDate { get; set; }

    [StringLength(2000)]
    public string? SoftwareNotes { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Vendor? Vendor { get; set; }
}

public enum SoftwareCategory
{
    LicensedApplications,
    OperatingSystems,
    Subscriptions,
    OpenSource
}

public enum SoftwareStatus
{
    Active,
    Expired,
    Revoked,
    PendingRenewal,
    Suspended,
    Retired,
    Trial
}

public enum SoftwareBillingCycle
{
    Monthly,
    Quarterly,
    Annual,
    Custom
}

public enum SoftwareDeploymentEnvironment
{
    Development,
    Testing,
    Production,
    Mixed
}

public enum AssetStatus
{
    InStock,
    Assigned,
    InRepair,
    Retired
}
