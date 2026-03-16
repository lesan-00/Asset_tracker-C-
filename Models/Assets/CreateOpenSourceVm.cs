using System.ComponentModel.DataAnnotations;

namespace AssetTracker.Models.Assets;

public class CreateOpenSourceVm
{
    [Required]
    [StringLength(50)]
    public string AssetTag { get; set; } = string.Empty;

    [Required]
    [StringLength(160)]
    public string Name { get; set; } = string.Empty;

    [StringLength(80)]
    public string? Version { get; set; }

    [StringLength(120)]
    public string? OpenSourceLicenseType { get; set; }

    [StringLength(160)]
    public string? MaintainerOrVendor { get; set; }

    public int? VendorId { get; set; }

    [StringLength(160)]
    public string? InstalledOn { get; set; }

    [StringLength(100)]
    public string? Location { get; set; }

    public SoftwareDeploymentEnvironment? DeploymentEnvironment { get; set; }

    [StringLength(120)]
    public string? SecurityStatus { get; set; }

    public DateTime? LastUpdatedDate { get; set; }

    [Required]
    public SoftwareStatus Status { get; set; } = SoftwareStatus.Active;

    [StringLength(2000)]
    public string? Notes { get; set; }
}
