using System.ComponentModel.DataAnnotations;
using AssetTracker.Models.Vendors;

namespace AssetTracker.Models.Vendors.ViewModels;

public sealed class PurchaseRequestLineInputVm
{
    public int? Id { get; set; }

    public PurchaseRequestLineType? LineType { get; set; }

    [StringLength(200)]
    public string? Name { get; set; }

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
}
