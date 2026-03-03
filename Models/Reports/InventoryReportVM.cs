namespace AssetTracker.Models.Reports;

public class InventoryStatusCountRow
{
    public AssetStatus Status { get; set; }
    public int Count { get; set; }
}

public class InventoryReportVM
{
    public int TotalAssets { get; set; }
    public int InStock { get; set; }
    public int Assigned { get; set; }
    public int InRepair { get; set; }
    public int Retired { get; set; }
    public List<InventoryStatusCountRow> StatusCounts { get; set; } = new();
}
