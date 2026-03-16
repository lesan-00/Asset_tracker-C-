using System.ComponentModel.DataAnnotations;

namespace AssetTracker.Models.Vendors.ViewModels;

public sealed class VendorContractCreateVm
{
    public int? Id { get; set; }

    [Required]
    public int VendorId { get; set; }

    [StringLength(20)]
    public string ContractNumber { get; set; } = string.Empty;

    [Required]
    public ContractType? ContractType { get; set; }

    [Range(0, 999999999999.99)]
    public decimal? ContractValue { get; set; }

    [Required]
    [StringLength(10)]
    public string Currency { get; set; } = "KES";

    [StringLength(120)]
    public string? SignedBy { get; set; }

    public DateTime? SignedDate { get; set; }

    [Required]
    [StringLength(200)]
    public string ContractTitle { get; set; } = string.Empty;

    public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime EndDate { get; set; } = DateTime.UtcNow.Date;

    [Required]
    public ContractStatus Status { get; set; } = ContractStatus.Active;

    public bool AutoRenew { get; set; }

    [Range(1, 3650)]
    public int? AlertBeforeDays { get; set; } = 30;

    [StringLength(2000)]
    public string? Notes { get; set; }

    public string VendorName { get; set; } = string.Empty;
    public string VendorCode { get; set; } = string.Empty;
}
