using System.ComponentModel.DataAnnotations;
using AssetTracker.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AssetTracker.Models.Assignments;

public enum AssignmentTargetType
{
    Staff,
    Department,
    Location
}

public class CreateAssignmentVm
{
    [Required]
    [Display(Name = "Asset")]
    public int? AssetId { get; set; }

    [Display(Name = "Receiver (Staff)")]
    public int? StaffProfileId { get; set; }

    [Required]
    [Display(Name = "Assignment Target")]
    public AssignmentTargetType TargetType { get; set; } = AssignmentTargetType.Staff;

    [StringLength(150)]
    [Display(Name = "Receiver")]
    public string? TargetValue { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Issue Date")]
    public DateTime? IssueDate { get; set; }

    [StringLength(100)]
    [Display(Name = "Issue Condition")]
    public string? IssueCondition { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public SelectList AssetOptions { get; set; } = new(Array.Empty<SelectListItem>(), "Value", "Text");
    public SelectList StaffOptions { get; set; } = new(Array.Empty<SelectListItem>(), "Value", "Text");
}
