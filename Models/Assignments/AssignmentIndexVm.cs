namespace AssetTracker.Models.Assignments;

public class AssignmentCardVm
{
    public int AssignmentId { get; set; }
    public int AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string AssetTag { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string AssetType { get; set; } = string.Empty;
    public int StaffProfileId { get; set; }
    public string StaffFullName { get; set; } = string.Empty;
    public DateTime IssuedDate { get; set; }
    public AssignmentStatus Status { get; set; }
}

public class AssignmentIndexVm
{
    public string? Query { get; set; }
    public List<AssignmentCardVm> Assignments { get; set; } = new();
}
