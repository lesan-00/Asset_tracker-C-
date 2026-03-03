using System.ComponentModel.DataAnnotations;
using AssetTracker.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AssetTracker.Models.Issues;

public class LookupOptionVm
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class IssueCreateVm
{
    [Required]
    [StringLength(120)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Category { get; set; } = string.Empty;

    [Required]
    public IssuePriority Priority { get; set; } = IssuePriority.Low;

    public int? AssetId { get; set; }
    public int? ReportedForStaffProfileId { get; set; }

    [Required]
    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    public string? AssetSearchText { get; set; }
    public string? ReportedForSearchText { get; set; }

    public SelectList Assets { get; set; } = new(Array.Empty<SelectListItem>(), "Value", "Text");
    public SelectList StaffProfiles { get; set; } = new(Array.Empty<SelectListItem>(), "Value", "Text");
    public List<LookupOptionVm> AssetLookupItems { get; set; } = new();
    public List<LookupOptionVm> StaffLookupItems { get; set; } = new();
}
