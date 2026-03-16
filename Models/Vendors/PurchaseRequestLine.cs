using System.ComponentModel.DataAnnotations;

namespace AssetTracker.Models.Vendors;

public class PurchaseRequestLine
{
    public int Id { get; set; }

    [Required]
    public int PurchaseRequestId { get; set; }

    [Required]
    public PurchaseRequestLineType LineType { get; set; } = PurchaseRequestLineType.Item;

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Category { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    [Range(0, int.MaxValue)]
    public int? Quantity { get; set; }

    [Range(0, 999999999999.99)]
    public decimal? EstimatedUnitCost { get; set; }

    [Range(0, 999999999999.99)]
    public decimal? EstimatedTotal { get; set; }

    public int? SuggestedVendorId { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public PurchaseRequest PurchaseRequest { get; set; } = default!;
    public Vendor? SuggestedVendor { get; set; }
}
