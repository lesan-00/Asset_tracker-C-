using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace AssetTracker.Models.Vendors;

[Index(nameof(TicketNumber), IsUnique = true)]
public class VendorSupportTicket
{
    public int Id { get; set; }

    [Required]
    public int VendorId { get; set; }

    [Required]
    [StringLength(20)]
    public string TicketNumber { get; set; } = string.Empty;

    [Required]
    public SupportTicketType Type { get; set; } = SupportTicketType.Support;

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    public DateTime OpenedDate { get; set; } = DateTime.UtcNow;

    public DateTime? ClosedDate { get; set; }

    [Required]
    public SupportTicketStatus Status { get; set; } = SupportTicketStatus.Open;

    public int? AssetId { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Vendor Vendor { get; set; } = default!;
    public AssetTracker.Models.Asset? Asset { get; set; }
}
