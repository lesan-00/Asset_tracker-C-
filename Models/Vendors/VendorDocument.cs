using System.ComponentModel.DataAnnotations;

namespace AssetTracker.Models.Vendors;

public class VendorDocument
{
    public int Id { get; set; }

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

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    [StringLength(1000)]
    public string? Notes { get; set; }

    public Vendor Vendor { get; set; } = default!;
}
