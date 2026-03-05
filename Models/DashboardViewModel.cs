namespace AssetTracker.Models;

public class DashboardVm
{
    public int TotalAssets { get; set; }
    public int InStock { get; set; }
    public int Assigned { get; set; }
    public int InRepair { get; set; }
    public int OpenIssues { get; set; }
    public int CriticalIssues { get; set; }
    public List<DepartmentAllocationVm> AllocationByDepartment { get; set; } = new();
    public List<OpenIssueVm> LatestOpenIssues { get; set; } = new();
    public List<ActivityVm> RecentActivity { get; set; } = new();
}

public class DepartmentAllocationVm
{
    public string DepartmentName { get; set; } = "Unassigned";
    public int Count { get; set; }
    public int Percent { get; set; }
}

public class OpenIssueVm
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public IssuePriority Priority { get; set; }
    public string AssetTag { get; set; } = "No Asset";
    public DateTime CreatedAt { get; set; }
}

public class ActivityVm
{
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
}
