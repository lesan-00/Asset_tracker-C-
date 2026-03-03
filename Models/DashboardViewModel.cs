namespace AssetTracker.Models;

public class DashboardViewModel
{
    public int TotalAssets { get; set; }
    public int InStock { get; set; }
    public int Assigned { get; set; }
    public int InRepair { get; set; }
    public int OpenIssues { get; set; }
    public string? Query { get; set; }
    public List<SearchResultRow> Results { get; set; } = new();
}

public class SearchResultRow
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string SubTitle { get; set; } = string.Empty;
    public string StatusText { get; set; } = string.Empty;
    public string LinkUrl { get; set; } = string.Empty;
}
