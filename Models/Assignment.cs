using System.ComponentModel.DataAnnotations;

namespace AssetTracker.Models;

public enum AssignmentStatus
{
    // Legacy statuses kept for backward compatibility with older records.
    PendingAcceptance,
    Accepted,
    ReturnRequested,
    ReturnedApproved,
    Rejected,
    // Admin-only workflow statuses.
    Active,
    Reverted,
    Returned
}

public class Assignment
{
    public int Id { get; set; }

    [Required]
    public int AssetId { get; set; }

    [Required]
    public int StaffProfileId { get; set; }

    [Required]
    public AssignmentStatus Status { get; set; } = AssignmentStatus.Active;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcceptedAt { get; set; }
    public DateTime? ReturnRequestedAt { get; set; }
    public DateTime? ReturnedApprovedAt { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public Asset Asset { get; set; } = default!;
    public StaffProfile StaffProfile { get; set; } = default!;
}
