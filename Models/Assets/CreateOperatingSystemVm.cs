using System.ComponentModel.DataAnnotations;

namespace AssetTracker.Models.Assets;

public class CreateOperatingSystemVm
{
    [Required]
    [StringLength(50)]
    public string AssetTag { get; set; } = string.Empty;

    [Required]
    [StringLength(160)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Edition { get; set; } = string.Empty;

    [Required]
    [StringLength(80)]
    public string Version { get; set; } = string.Empty;

    [StringLength(60)]
    public string? BuildNumber { get; set; }

    public int? VendorId { get; set; }

    [StringLength(120)]
    public string? LicenseType { get; set; }

    [StringLength(255)]
    public string? LicenseKey { get; set; }

    [StringLength(160)]
    public string? InstalledOn { get; set; }

    [StringLength(100)]
    public string? Location { get; set; }

    public DateTime? SupportEndDate { get; set; }

    [StringLength(120)]
    public string? PatchStatus { get; set; }

    public DateTime? PurchaseDate { get; set; }

    [Required]
    public SoftwareStatus Status { get; set; } = SoftwareStatus.Active;

    [StringLength(2000)]
    public string? Notes { get; set; }
}
