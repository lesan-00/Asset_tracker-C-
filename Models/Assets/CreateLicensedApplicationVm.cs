using System.ComponentModel.DataAnnotations;

namespace AssetTracker.Models.Assets;

public class CreateLicensedApplicationVm
{
    public static readonly string[] AllowedLicenseTypes =
    [
        "Per User",
        "Per Device",
        "Volume License",
        "Subscription",
        "Perpetual",
        "Trial",
        "OEM",
        "Other"
    ];

    public static readonly string[] AllowedCurrencies = ["KES", "USD", "EUR", "GBP"];

    public static readonly SoftwareStatus[] AllowedStatuses =
    [
        SoftwareStatus.Active,
        SoftwareStatus.Expired,
        SoftwareStatus.Suspended,
        SoftwareStatus.Retired,
        SoftwareStatus.Trial
    ];

    [Required]
    [StringLength(50)]
    public string AssetTag { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(50)]
    public string? Version { get; set; }

    public int? VendorId { get; set; }

    [Required]
    [StringLength(60)]
    public string? LicenseType { get; set; }

    [StringLength(255)]
    public string? LicenseKey { get; set; }

    public DateTime? InstalledDate { get; set; }

    public DateTime? PurchaseDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime? RenewalReminderDate { get; set; }

    [Range(0, 999999999999.99)]
    public decimal? Cost { get; set; }

    [Required]
    [StringLength(10)]
    public string Currency { get; set; } = "KES";

    [StringLength(100)]
    public string? InvoiceReference { get; set; }

    public int? PurchaseOrderId { get; set; }

    [StringLength(100)]
    public string? PurchaseOrderReference { get; set; }

    [Required]
    public SoftwareStatus Status { get; set; } = SoftwareStatus.Active;

    [StringLength(2000)]
    public string? Notes { get; set; }
}
