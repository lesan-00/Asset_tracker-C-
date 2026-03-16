using System.ComponentModel.DataAnnotations;

namespace AssetTracker.Models.Vendors.ViewModels;

public sealed class PurchaseRequestUpsertVm
{
    public int? Id { get; set; }

    public string? PrNumber { get; set; }

    [Required]
    public DateTime RequestDate { get; set; } = DateTime.UtcNow.Date;

    public DateTime? NeededByDate { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    public PurchaseRequestStatus Status { get; set; } = PurchaseRequestStatus.Draft;

    public List<PurchaseRequestLineInputVm> Lines { get; set; } = [new()];

    // Alias used by dynamic line-item editors that bind with Items[i].FieldName.
    public List<PurchaseRequestLineInputVm> Items
    {
        get => Lines;
        set => Lines = value ?? new List<PurchaseRequestLineInputVm>();
    }
}
