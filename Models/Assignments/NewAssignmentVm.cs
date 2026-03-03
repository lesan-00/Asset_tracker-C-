using System.ComponentModel.DataAnnotations;

namespace AssetTracker.Models.Assignments;

public class NewAssignmentVm : IValidatableObject
{
    [Required]
    [Display(Name = "Asset")]
    public int? AssetId { get; set; }

    [Display(Name = "Asset")]
    public string? AssetSearchText { get; set; }

    [Required]
    [Display(Name = "Assignment Target")]
    public AssignmentTargetType TargetType { get; set; } = AssignmentTargetType.Staff;

    [Display(Name = "Receiver")]
    public string? ReceiverSearchText { get; set; }

    [Display(Name = "Receiver (Staff)")]
    public int? StaffProfileId { get; set; }

    [StringLength(100)]
    [Display(Name = "Department")]
    public string? Department { get; set; }

    [StringLength(100)]
    [Display(Name = "Location")]
    public string? Location { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Issue Date")]
    public DateTime? IssueDate { get; set; }

    [StringLength(100)]
    [Display(Name = "Issue Condition")]
    public string? IssueCondition { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!AssetId.HasValue || AssetId.Value <= 0)
        {
            yield return new ValidationResult("Please select an asset.", new[] { nameof(AssetId) });
        }

        var department = Department?.Trim();
        var location = Location?.Trim();

        switch (TargetType)
        {
            case AssignmentTargetType.Staff when !StaffProfileId.HasValue:
                yield return new ValidationResult("Please select a staff receiver.", new[] { nameof(StaffProfileId) });
                break;
            case AssignmentTargetType.Department when string.IsNullOrWhiteSpace(department) || department.Length < 2:
                yield return new ValidationResult("Department is required and must be at least 2 characters.", new[] { nameof(Department) });
                break;
            case AssignmentTargetType.Location when string.IsNullOrWhiteSpace(location) || location.Length < 2:
                yield return new ValidationResult("Location is required and must be at least 2 characters.", new[] { nameof(Location) });
                break;
        }
    }
}
