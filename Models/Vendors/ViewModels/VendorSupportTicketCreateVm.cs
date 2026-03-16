using System.ComponentModel.DataAnnotations;

namespace AssetTracker.Models.Vendors.ViewModels;

public sealed class VendorSupportTicketCreateVm
{
    [Required]
    public int VendorId { get; set; }

    public int? AssetId { get; set; }

    [Required]
    public SupportTicketType Type { get; set; } = SupportTicketType.Support;

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    public DateTime OpenedDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime? ClosedDate { get; set; }

    [Required]
    public SupportTicketStatus Status { get; set; } = SupportTicketStatus.Open;

    [StringLength(2000)]
    public string? Notes { get; set; }
}
