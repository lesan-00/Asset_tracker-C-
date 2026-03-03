using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace AssetTracker.Models;

public enum IssueStatus
{
    Open,
    InProgress,
    Resolved,
    Closed
}

public enum IssuePriority
{
    Low,
    Medium,
    High
}

public class Issue
{
    public int Id { get; set; }
    public int? AssetId { get; set; }
    public int? ReportedForStaffProfileId { get; set; }

    [Required]
    public string CreatedByUserId { get; set; } = string.Empty;

    [Required]
    [StringLength(120)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Category { get; set; } = string.Empty;

    [Required]
    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public IssueStatus Status { get; set; } = IssueStatus.Open;

    [Required]
    public IssuePriority Priority { get; set; } = IssuePriority.Medium;

    public string? AssignedToUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Asset? Asset { get; set; }
    public StaffProfile? ReportedForStaffProfile { get; set; }
    public IdentityUser CreatedByUser { get; set; } = default!;
    public IdentityUser? AssignedToUser { get; set; }
}
