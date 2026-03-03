using System.ComponentModel.DataAnnotations;
using AssetTracker.Models;

namespace AssetTracker.Models.Issues;

public class IssueEditVm
{
    [Required]
    public int Id { get; set; }

    public int? AssetId { get; set; }
    public int? ReportedForStaffProfileId { get; set; }

    [Required]
    public IssueStatus Status { get; set; }

    [Required]
    public IssuePriority Priority { get; set; }

    public string? AssignedToUserId { get; set; }
}
