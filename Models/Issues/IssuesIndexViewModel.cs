using AssetTracker.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AssetTracker.Models.Issues;

public class IssuesIndexViewModel
{
    public List<IssueListItemVm> Items { get; set; } = new();
    public int OpenCount { get; set; }
    public int ResolvedCount { get; set; }
    public string? Q { get; set; }
    public IssueStatus? Status { get; set; }
    public IssuePriority? Priority { get; set; }
    public string? StatusFilter { get; set; }
    public string? PriorityFilter { get; set; }
    public SelectList StatusOptions { get; set; } = new(Array.Empty<SelectListItem>(), "Value", "Text");
    public SelectList PriorityOptions { get; set; } = new(Array.Empty<SelectListItem>(), "Value", "Text");
    public IssueCreateVm CreateForm { get; set; } = new();
    public bool OpenCreateModal { get; set; }
}
