using System.ComponentModel.DataAnnotations;

namespace AssetTracker.Models.Vendors.ViewModels;

public sealed class CreateVendorVm
{
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

    [StringLength(2000)]
    public string? Notes { get; set; }
}
