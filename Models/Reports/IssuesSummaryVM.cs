namespace AssetTracker.Models.Reports;

public class PriorityCountRow
{
    public IssuePriority Priority { get; set; }
    public int Count { get; set; }
}

public class RecentIssueRow
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public IssueStatus Status { get; set; }
    public IssuePriority Priority { get; set; }
    public string AssetTag { get; set; } = "-";
    public string CreatedByEmail { get; set; } = "-";
    public string AssignedToEmail { get; set; } = "-";
    public DateTime CreatedAt { get; set; }
}

public class IssuesSummaryVM
{
    public int TotalIssues { get; set; }
    public int Open { get; set; }
    public int InProgress { get; set; }
    public int Resolved { get; set; }
    public int Closed { get; set; }
    public List<PriorityCountRow> PriorityCounts { get; set; } = new();
    public List<RecentIssueRow> RecentIssues { get; set; } = new();
}
