using System.Text;
using AssetTracker.Data;
using AssetTracker.Models.Procurement;
using AssetTracker.Models.Vendors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetTracker.Controllers;

[Authorize(Roles = "Admin,Staff")]
public class ProcurementController : Controller
{
    private readonly ApplicationDbContext _context;

    public ProcurementController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public Task<IActionResult> Pipeline(string filter = "all", string? q = null)
    {
        return Task.FromResult<IActionResult>(RedirectToAction(nameof(Requests), new { filter, q }));
    }

    [HttpGet]
    public async Task<IActionResult> Requests(string filter = "all", string? q = null)
    {
        var activeFilter = NormalizeFilter(filter);
        var query = q?.Trim();
        var today = DateTime.UtcNow.Date;
        var yearStart = new DateTime(today.Year, 1, 1);

        var prCount = await _context.PurchaseRequests.AsNoTracking().CountAsync();
        var poCount = await _context.PurchaseOrders.AsNoTracking().CountAsync();
        var invoiceCount = await _context.VendorInvoices.AsNoTracking().CountAsync();
        var pendingPrCount = await _context.PurchaseRequests.AsNoTracking()
            .CountAsync(pr => pr.Status == PurchaseRequestStatus.Submitted || pr.Status == PurchaseRequestStatus.PendingApproval);
        var openPoCount = await _context.PurchaseOrders.AsNoTracking()
            .CountAsync(po =>
                po.Status == PurchaseOrderStatus.Draft ||
                po.Status == PurchaseOrderStatus.Submitted ||
                po.Status == PurchaseOrderStatus.Invoiced ||
                po.Status == PurchaseOrderStatus.Approved);
        var pendingInvoiceCount = await _context.VendorInvoices.AsNoTracking()
            .CountAsync(i => i.PaymentStatus != InvoicePaymentStatus.Paid && i.PaymentStatus != InvoicePaymentStatus.Cancelled);
        var ytdSpend = await _context.PurchaseOrders.AsNoTracking()
            .Where(po => po.OrderDate >= yearStart &&
                (po.Status == PurchaseOrderStatus.Paid ||
                 po.Status == PurchaseOrderStatus.Closed ||
                 po.Status == PurchaseOrderStatus.Received))
            .SumAsync(po => (decimal?)po.TotalAmount);

        var documents = await BuildUnifiedDocumentsAsync(today, 250);
        var filteredDocuments = ApplyFilter(documents, activeFilter, query)
            .OrderByDescending(d => d.DocumentDate)
            .ThenByDescending(d => d.DocumentNumber)
            .Take(250)
            .ToList();

        var vm = new ProcurementPipelineVm
        {
            ActiveFilter = activeFilter,
            Query = query,
            PrCount = prCount,
            PoCount = poCount,
            InvoiceCount = invoiceCount,
            YtdSpend = ytdSpend ?? 0m,
            PendingPrCount = pendingPrCount,
            OpenPoCount = openPoCount,
            PendingInvoiceCount = pendingInvoiceCount,
            StepCards =
            [
                new ProcurementStepCardVm
                {
                    StepNumber = 1,
                    Title = "Purchase Request",
                    Subtitle = "Internal request intake",
                    Count = prCount,
                    LatestDocumentNumber = documents.Where(d => d.DocumentTypeKey == "pr")
                        .OrderByDescending(d => d.DocumentDate)
                        .Select(d => d.DocumentNumber)
                        .FirstOrDefault(),
                    Icon = "bi bi-journal-text"
                },
                new ProcurementStepCardVm
                {
                    StepNumber = 2,
                    Title = "Purchase Order",
                    Subtitle = "Approved orders to suppliers",
                    Count = poCount,
                    LatestDocumentNumber = documents.Where(d => d.DocumentTypeKey == "po")
                        .OrderByDescending(d => d.DocumentDate)
                        .Select(d => d.DocumentNumber)
                        .FirstOrDefault(),
                    Icon = "bi bi-receipt"
                },
                new ProcurementStepCardVm
                {
                    StepNumber = 3,
                    Title = "Invoice",
                    Subtitle = "Billing and payment tracking",
                    Count = invoiceCount,
                    LatestDocumentNumber = documents.Where(d => d.DocumentTypeKey == "invoice")
                        .OrderByDescending(d => d.DocumentDate)
                        .Select(d => d.DocumentNumber)
                        .FirstOrDefault(),
                    Icon = "bi bi-cash-coin"
                }
            ],
            FilterTabs =
            [
                new ProcurementFilterTabVm { Key = "all", Label = "All Documents", Count = prCount + poCount + invoiceCount, IsActive = activeFilter == "all" },
                new ProcurementFilterTabVm { Key = "pr", Label = "Purchase Requests", Count = prCount, IsActive = activeFilter == "pr" },
                new ProcurementFilterTabVm { Key = "po", Label = "Purchase Orders", Count = poCount, IsActive = activeFilter == "po" },
                new ProcurementFilterTabVm { Key = "invoice", Label = "Invoices", Count = invoiceCount, IsActive = activeFilter == "invoice" }
            ],
            UnifiedDocuments = filteredDocuments
        };

        return View("Pipeline", vm);
    }

    [HttpGet]
    public async Task<IActionResult> Export(string filter = "all", string? q = null)
    {
        var activeFilter = NormalizeFilter(filter);
        var query = q?.Trim();
        var today = DateTime.UtcNow.Date;

        var documents = await BuildUnifiedDocumentsAsync(today, 1000);
        var filteredDocuments = ApplyFilter(documents, activeFilter, query)
            .OrderByDescending(d => d.DocumentDate)
            .ThenByDescending(d => d.DocumentNumber)
            .Take(1000)
            .ToList();

        var csv = new StringBuilder();
        csv.AppendLine("DocumentType,DocumentNumber,Date,VendorOrRequester,Status,Amount,Currency,LinkedDocument");

        foreach (var item in filteredDocuments)
        {
            csv.AppendLine(string.Join(',',
                Csv(item.DocumentTypeLabel),
                Csv(item.DocumentNumber),
                Csv(item.DocumentDate.ToString("yyyy-MM-dd")),
                Csv(item.VendorOrRequester),
                Csv(item.StatusLabel),
                Csv(item.Amount?.ToString("F2") ?? string.Empty),
                Csv(item.Currency),
                Csv(item.LinkedDocument)));
        }

        var fileName = $"procurement-pipeline-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv";
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
    }

    private async Task<List<ProcurementUnifiedDocumentVm>> BuildUnifiedDocumentsAsync(DateTime today, int takePerType)
    {
        var prRows = await _context.PurchaseRequests
            .AsNoTracking()
            .OrderByDescending(pr => pr.CreatedAt)
            .ThenByDescending(pr => pr.Id)
            .Select(pr => new
            {
                pr.Id,
                pr.PrNumber,
                pr.RequestDate,
                pr.Status,
                Requester = pr.RequestedByStaff != null
                    ? pr.RequestedByStaff.FullName
                    : (string.IsNullOrWhiteSpace(pr.Department) ? "-" : pr.Department),
                EstimatedTotal = pr.Lines.Sum(line => (decimal?)line.EstimatedTotal) ?? 0m,
                LinkedPoNumber = pr.PurchaseOrders
                    .OrderByDescending(po => po.CreatedAt)
                    .Select(po => po.PoNumber)
                    .FirstOrDefault()
            })
            .Take(takePerType)
            .ToListAsync();

        var poRows = await _context.PurchaseOrders
            .AsNoTracking()
            .OrderByDescending(po => po.CreatedAt)
            .ThenByDescending(po => po.Id)
            .Select(po => new
            {
                po.Id,
                po.PoNumber,
                po.OrderDate,
                po.Status,
                po.TotalAmount,
                po.Currency,
                VendorName = po.Vendor.CompanyName,
                LinkedPrNumber = po.PurchaseRequest != null ? po.PurchaseRequest.PrNumber : null,
                LinkedInvoiceNumber = po.Invoices
                    .OrderByDescending(inv => inv.InvoiceDate)
                    .Select(inv => inv.InvoiceNumber)
                    .FirstOrDefault()
            })
            .Take(takePerType)
            .ToListAsync();

        var invoiceRows = await _context.VendorInvoices
            .AsNoTracking()
            .OrderByDescending(inv => inv.CreatedAt)
            .ThenByDescending(inv => inv.Id)
            .Select(inv => new
            {
                inv.Id,
                inv.InvoiceNumber,
                inv.InvoiceDate,
                inv.DueDate,
                inv.PaymentStatus,
                inv.Amount,
                inv.Currency,
                VendorName = inv.Vendor.CompanyName,
                LinkedPoNumber = inv.PurchaseOrder != null ? inv.PurchaseOrder.PoNumber : null,
                LinkedPrNumber = inv.PurchaseOrder != null && inv.PurchaseOrder.PurchaseRequest != null
                    ? inv.PurchaseOrder.PurchaseRequest.PrNumber
                    : null
            })
            .Take(takePerType)
            .ToListAsync();

        var documents = new List<ProcurementUnifiedDocumentVm>();

        documents.AddRange(prRows.Select(pr => new ProcurementUnifiedDocumentVm
        {
            DocumentTypeKey = "pr",
            DocumentTypeLabel = "Purchase Request",
            DocumentId = pr.Id,
            DocumentNumber = pr.PrNumber,
            DocumentDate = pr.RequestDate,
            VendorOrRequester = string.IsNullOrWhiteSpace(pr.Requester) ? "-" : pr.Requester,
            StatusLabel = pr.Status.ToString(),
            StatusBadgeClass = PurchaseRequestStatusBadge(pr.Status),
            Amount = pr.EstimatedTotal,
            Currency = "KES",
            LinkedDocument = string.IsNullOrWhiteSpace(pr.LinkedPoNumber) ? "-" : $"{pr.PrNumber} -> {pr.LinkedPoNumber}"
        }));

        documents.AddRange(poRows.Select(po => new ProcurementUnifiedDocumentVm
        {
            DocumentTypeKey = "po",
            DocumentTypeLabel = "Purchase Order",
            DocumentId = po.Id,
            DocumentNumber = po.PoNumber,
            DocumentDate = po.OrderDate,
            VendorOrRequester = string.IsNullOrWhiteSpace(po.VendorName) ? "-" : po.VendorName,
            StatusLabel = po.Status.ToString(),
            StatusBadgeClass = PurchaseOrderStatusBadge(po.Status),
            Amount = po.TotalAmount,
            Currency = string.IsNullOrWhiteSpace(po.Currency) ? "KES" : po.Currency,
            LinkedDocument = BuildPoLinkedFlow(po.LinkedPrNumber, po.PoNumber, po.LinkedInvoiceNumber)
        }));

        documents.AddRange(invoiceRows.Select(inv =>
        {
            var effectiveStatus = EffectiveInvoiceStatus(inv.PaymentStatus, inv.DueDate, today);

            return new ProcurementUnifiedDocumentVm
            {
                DocumentTypeKey = "invoice",
                DocumentTypeLabel = "Invoice",
                DocumentId = inv.Id,
                DocumentNumber = inv.InvoiceNumber,
                DocumentDate = inv.InvoiceDate,
                VendorOrRequester = string.IsNullOrWhiteSpace(inv.VendorName) ? "-" : inv.VendorName,
                StatusLabel = effectiveStatus.ToString(),
                StatusBadgeClass = InvoiceStatusBadge(effectiveStatus),
                Amount = inv.Amount,
                Currency = string.IsNullOrWhiteSpace(inv.Currency) ? "KES" : inv.Currency,
                LinkedDocument = BuildInvoiceLinkedFlow(inv.LinkedPrNumber, inv.LinkedPoNumber, inv.InvoiceNumber)
            };
        }));

        return documents;
    }

    private static IEnumerable<ProcurementUnifiedDocumentVm> ApplyFilter(
        IEnumerable<ProcurementUnifiedDocumentVm> documents,
        string filter,
        string? query)
    {
        var filtered = filter == "all"
            ? documents
            : documents.Where(d => d.DocumentTypeKey == filter);

        if (string.IsNullOrWhiteSpace(query))
        {
            return filtered;
        }

        var lowered = query.ToLowerInvariant();
        return filtered.Where(d =>
            d.DocumentNumber.ToLowerInvariant().Contains(lowered) ||
            d.DocumentTypeLabel.ToLowerInvariant().Contains(lowered) ||
            d.VendorOrRequester.ToLowerInvariant().Contains(lowered) ||
            d.StatusLabel.ToLowerInvariant().Contains(lowered) ||
            d.LinkedDocument.ToLowerInvariant().Contains(lowered));
    }

    private static InvoicePaymentStatus EffectiveInvoiceStatus(InvoicePaymentStatus status, DateTime dueDate, DateTime today)
    {
        if (status == InvoicePaymentStatus.Pending && dueDate.Date < today)
        {
            return InvoicePaymentStatus.Overdue;
        }

        return status;
    }

    private static string BuildPoLinkedFlow(string? prNumber, string poNumber, string? invoiceNumber)
    {
        var segments = new List<string>();

        if (!string.IsNullOrWhiteSpace(prNumber))
        {
            segments.Add(prNumber);
        }

        segments.Add(poNumber);

        if (!string.IsNullOrWhiteSpace(invoiceNumber))
        {
            segments.Add(invoiceNumber);
        }

        return segments.Count > 1 ? string.Join(" -> ", segments) : "-";
    }

    private static string BuildInvoiceLinkedFlow(string? prNumber, string? poNumber, string invoiceNumber)
    {
        var segments = new List<string>();

        if (!string.IsNullOrWhiteSpace(prNumber))
        {
            segments.Add(prNumber);
        }

        if (!string.IsNullOrWhiteSpace(poNumber))
        {
            segments.Add(poNumber);
        }

        segments.Add(invoiceNumber);

        return segments.Count > 1 ? string.Join(" -> ", segments) : "-";
    }

    private static string NormalizeFilter(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return "all";
        }

        var normalized = filter.Trim().ToLowerInvariant();
        return normalized is "pr" or "po" or "invoice" ? normalized : "all";
    }

    private static string PurchaseRequestStatusBadge(PurchaseRequestStatus status) => status switch
    {
        PurchaseRequestStatus.Draft => "text-bg-secondary",
        PurchaseRequestStatus.Submitted => "text-bg-warning",
        PurchaseRequestStatus.PendingApproval => "text-bg-warning",
        PurchaseRequestStatus.Approved => "text-bg-success",
        PurchaseRequestStatus.Rejected => "text-bg-danger",
        PurchaseRequestStatus.ConvertedToPO => "text-bg-primary",
        PurchaseRequestStatus.Cancelled => "text-bg-dark",
        _ => "text-bg-secondary"
    };

    private static string PurchaseOrderStatusBadge(PurchaseOrderStatus status) => status switch
    {
        PurchaseOrderStatus.Draft => "text-bg-secondary",
        PurchaseOrderStatus.Submitted => "text-bg-warning",
        PurchaseOrderStatus.Invoiced => "text-bg-warning",
        PurchaseOrderStatus.Paid => "text-bg-success",
        PurchaseOrderStatus.Closed => "text-bg-info",
        PurchaseOrderStatus.Approved => "text-bg-primary",
        PurchaseOrderStatus.Received => "text-bg-success",
        PurchaseOrderStatus.Cancelled => "text-bg-danger",
        _ => "text-bg-secondary"
    };

    private static string InvoiceStatusBadge(InvoicePaymentStatus status) => status switch
    {
        InvoicePaymentStatus.Pending => "text-bg-warning",
        InvoicePaymentStatus.Paid => "text-bg-success",
        InvoicePaymentStatus.Overdue => "text-bg-danger",
        InvoicePaymentStatus.Cancelled => "text-bg-secondary",
        _ => "text-bg-secondary"
    };

    private static string Csv(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
