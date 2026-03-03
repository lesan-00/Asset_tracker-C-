namespace AssetTracker.Models.Reports;

public class AssetRegisterRowVm
{
    public int AssetId { get; set; }
    public string AssetTag { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string AssigneeLabel { get; set; } = "-";
    public string TargetType { get; set; } = "-";
    public DateTime? AssignedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ActiveAssignmentRowVm
{
    public int AssignmentId { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public string TargetDisplay { get; set; } = string.Empty;
    public string AssetTag { get; set; } = string.Empty;
    public string AssetDisplay { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class AssignmentHistoryRowVm
{
    public int AssignmentId { get; set; }
    public string AssetTag { get; set; } = string.Empty;
    public string StaffName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
}
