using System.ComponentModel.DataAnnotations;

namespace AssetTracker.Models.Vendors.ViewModels;

public sealed class PurchaseOrderCreateVm
{
    [Required]
    public int VendorId { get; set; }

    public DateTime OrderDate { get; set; } = DateTime.UtcNow.Date;

    [Range(0, 999999999999.99)]
    public decimal TotalAmount { get; set; }

    [Required]
    [StringLength(10)]
    public string Currency { get; set; } = "KES";

    [Required]
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

    [StringLength(2000)]
    public string? Notes { get; set; }

    [Required]
    [StringLength(200)]
    public string ItemName { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; } = 1;

    [Range(0, 999999999999.99)]
    public decimal UnitPrice { get; set; }
}
