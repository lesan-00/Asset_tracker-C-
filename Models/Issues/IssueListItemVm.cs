using AssetTracker.Models;

namespace AssetTracker.Models.Issues;

public class IssueListItemVm
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IssueStatus Status { get; set; }
    public IssuePriority Priority { get; set; }
    public string AssetLabel { get; set; } = "No Asset";
    public string ReportedForLabel { get; set; } = "Not specified";
    public DateTime CreatedAt { get; set; }
}
