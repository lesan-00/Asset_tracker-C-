using System.ComponentModel.DataAnnotations;

namespace AssetTracker.Models.Vendors;

public class PurchaseOrderLine
{
    public int Id { get; set; }

    [Required]
    public int PurchaseOrderId { get; set; }

    [Required]
    [StringLength(200)]
    public string ItemName { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    [Range(0, 999999999999.99)]
    public decimal UnitPrice { get; set; }

    [Range(0, 999999999999.99)]
    public decimal LineTotal { get; set; }

    public PurchaseOrder PurchaseOrder { get; set; } = default!;
}
