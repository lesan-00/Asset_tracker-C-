using System.ComponentModel.DataAnnotations;

namespace AssetTracker.Models.Reports;

public enum ProcurementReportType
{
    [Display(Name = "PR Summary")]
    PrSummary,
    [Display(Name = "Pending Approval PRs")]
    PendingApprovalPrs,
    [Display(Name = "PR to PO Conversion")]
    PrToPoConversion,
    [Display(Name = "Department PR Spending")]
    DepartmentPrSpending,
    [Display(Name = "PO Summary")]
    PoSummary,
    [Display(Name = "Vendor PO Spend")]
    VendorPoSpend,
    [Display(Name = "Open Purchase Orders")]
    OpenPurchaseOrders,
    [Display(Name = "Vendor Delivery Performance")]
    VendorDeliveryPerformance,
    [Display(Name = "Invoice Summary")]
    InvoiceSummary,
    [Display(Name = "Pending/Overdue Invoices")]
    PendingOverdueInvoices,
    [Display(Name = "Vendor Invoice Spend")]
    VendorInvoiceSpend,
    [Display(Name = "Invoice vs PO Matching")]
    InvoiceVsPoMatching
}

public sealed class ProcurementSummaryMetricVm
{
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}

public sealed class ProcurementReportVm
{
    public ProcurementReportType ReportType { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public int? VendorId { get; init; }
    public string? Department { get; init; }
    public string? Search { get; init; }
    public string VendorLabel { get; init; } = "All Vendors";
    public IReadOnlyList<string> Headers { get; init; } = [];
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; init; } = [];
    public IReadOnlyList<ProcurementSummaryMetricVm> SummaryCards { get; init; } = [];
}
