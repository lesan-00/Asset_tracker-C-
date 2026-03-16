using AssetTracker.Models;

namespace AssetTracker.Models.Assets;

public class AssetDetailsVm
{
    public Asset Asset { get; set; } = default!;
    public DateTime? IssueDate { get; set; }
    public DateTime? ReturnedDate { get; set; }
    public string? CurrentUser { get; set; }
    public bool HasActiveAssignment { get; set; }
}
