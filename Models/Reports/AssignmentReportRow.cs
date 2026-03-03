namespace AssetTracker.Models.Reports;

public class AssignmentReportRow
{
    public int AssignmentId { get; set; }
    public string AssetTag { get; set; } = string.Empty;
    public string StaffName { get; set; } = string.Empty;
    public string StaffEmail { get; set; } = string.Empty;
    public AssignmentStatus Status { get; set; }
    public DateTime AssignedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? ReturnRequestedAt { get; set; }
    public DateTime? ReturnedApprovedAt { get; set; }
}
