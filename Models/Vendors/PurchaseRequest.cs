using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace AssetTracker.Models.Vendors;

[Index(nameof(PrNumber), IsUnique = true)]
public class PurchaseRequest
{
    public int Id { get; set; }

    [Required]
    [StringLength(20)]
    public string PrNumber { get; set; } = string.Empty;

    public DateTime RequestDate { get; set; } = DateTime.UtcNow.Date;

    public int? RequestedByStaffId { get; set; }

    [StringLength(100)]
    public string? Department { get; set; }

    public DateTime? NeededByDate { get; set; }

    [StringLength(2000)]
    public string? Justification { get; set; }

    public PurchaseRequestPriority Priority { get; set; } = PurchaseRequestPriority.Normal;

    public PurchaseRequestStatus Status { get; set; } = PurchaseRequestStatus.Draft;

    [StringLength(255)]
    public string? ApprovedByUserId { get; set; }

    public DateTime? ApprovedAt { get; set; }

    [StringLength(1000)]
    public string? RejectedReason { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public StaffProfile? RequestedByStaff { get; set; }
    public ICollection<PurchaseRequestLine> Lines { get; set; } = new List<PurchaseRequestLine>();
    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
}
