using Microsoft.AspNetCore.Mvc.Rendering;

namespace AssetTracker.Models.Reports;

public enum AssignmentTargetTypeFilter
{
    Staff,
    Location,
    Department
}

public class ReportsIndexVm
{
    public string? InventorySearch { get; set; }
    public string? InventoryType { get; set; }
    public string? InventoryStatus { get; set; }
    public string? InventoryDepartment { get; set; }
    public string? InventoryLocation { get; set; }

    public string? ActiveTargetType { get; set; }
    public string? ActiveSearch { get; set; }

    public DateTime? HistoryFrom { get; set; }
    public DateTime? HistoryTo { get; set; }
    public string? HistorySearch { get; set; }

    public ProcurementReportType ProcurementReportType { get; set; } = ProcurementReportType.PrSummary;
    public DateTime? ProcurementFrom { get; set; }
    public DateTime? ProcurementTo { get; set; }
    public int? ProcurementVendorId { get; set; }
    public string? ProcurementDepartment { get; set; }
    public string? ProcurementSearch { get; set; }

    public SelectList TypeOptions { get; set; } = new(Array.Empty<SelectListItem>(), "Value", "Text");
    public SelectList StatusOptions { get; set; } = new(Array.Empty<SelectListItem>(), "Value", "Text");
    public SelectList TargetTypeOptions { get; set; } = new(Array.Empty<SelectListItem>(), "Value", "Text");
    public SelectList ProcurementReportOptions { get; set; } = new(Array.Empty<SelectListItem>(), "Value", "Text");
    public SelectList ProcurementVendorOptions { get; set; } = new(Array.Empty<SelectListItem>(), "Value", "Text");
}
