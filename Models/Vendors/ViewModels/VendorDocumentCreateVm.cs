using System.ComponentModel.DataAnnotations;

namespace AssetTracker.Models.Vendors.ViewModels;

public sealed class VendorDocumentCreateVm
{
    [Required]
    public int VendorId { get; set; }

    [Required]
    [StringLength(200)]
    public string DocumentName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string DocumentType { get; set; } = string.Empty;

    [StringLength(500)]
    public string? FilePath { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }
}
