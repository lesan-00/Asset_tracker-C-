using AssetTracker.Data;
using AssetTracker.Models.Vendors;
using AssetTracker.Models.Vendors.ViewModels;
using AssetTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetTracker.Controllers;

[Authorize(Roles = "Admin,Staff")]
public class PurchaseOrdersController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IPurchaseOrderStatusSyncService _purchaseOrderStatusSyncService;

    public PurchaseOrdersController(
        ApplicationDbContext context,
        IPurchaseOrderStatusSyncService purchaseOrderStatusSyncService)
    {
        _context = context;
        _purchaseOrderStatusSyncService = purchaseOrderStatusSyncService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q = null, PurchaseOrderStatus? status = null, bool open = false)
    {
        var normalizedQ = q?.Trim();
        var query = _context.PurchaseOrders
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(normalizedQ))
        {
            var lowered = normalizedQ.ToLower();
            query = query.Where(po =>
                po.PoNumber.ToLower().Contains(lowered) ||
                po.Vendor.CompanyName.ToLower().Contains(lowered) ||
                (po.PurchaseRequest != null && po.PurchaseRequest.PrNumber.ToLower().Contains(lowered)));
        }

        if (open)
        {
            query = query.Where(po =>
                po.Status == PurchaseOrderStatus.Draft ||
                po.Status == PurchaseOrderStatus.Submitted ||
                po.Status == PurchaseOrderStatus.Invoiced ||
                po.Status == PurchaseOrderStatus.Approved);
        }
        else if (status.HasValue)
        {
            query = query.Where(po => po.Status == status.Value);
        }

        var items = await query
            .OrderByDescending(po => po.OrderDate)
            .ThenByDescending(po => po.Id)
            .Select(po => new PurchaseOrderListItemVm
            {
                Id = po.Id,
                PoNumber = po.PoNumber,
                OrderDate = po.OrderDate,
                VendorName = po.Vendor.CompanyName,
                Status = po.Status,
                TotalAmount = po.TotalAmount,
                Currency = po.Currency,
                LinkedPrNumber = po.PurchaseRequest != null ? po.PurchaseRequest.PrNumber : "-",
                LinkedInvoiceNumber = po.Invoices
                    .OrderByDescending(i => i.InvoiceDate)
                    .Select(i => i.InvoiceNumber)
                    .FirstOrDefault() ?? "-"
            })
            .ToListAsync();

        var yearStart = new DateTime(DateTime.UtcNow.Year, 1, 1);
        var vm = new PurchaseOrderIndexVm
        {
            Query = normalizedQ,
            StatusFilter = status,
            TotalOrders = await _context.PurchaseOrders.AsNoTracking().CountAsync(),
            OpenOrders = await _context.PurchaseOrders.AsNoTracking()
                .CountAsync(po =>
                    po.Status == PurchaseOrderStatus.Draft ||
                    po.Status == PurchaseOrderStatus.Submitted ||
                    po.Status == PurchaseOrderStatus.Invoiced ||
                    po.Status == PurchaseOrderStatus.Approved),
            ReceivedOrders = await _context.PurchaseOrders.AsNoTracking()
                .CountAsync(po =>
                    po.Status == PurchaseOrderStatus.Paid ||
                    po.Status == PurchaseOrderStatus.Closed ||
                    po.Status == PurchaseOrderStatus.Received),
            YtdSpend = await _context.PurchaseOrders.AsNoTracking()
                .Where(po => po.OrderDate >= yearStart &&
                    (po.Status == PurchaseOrderStatus.Paid ||
                     po.Status == PurchaseOrderStatus.Closed ||
                     po.Status == PurchaseOrderStatus.Received))
                .SumAsync(po => (decimal?)po.TotalAmount) ?? 0m,
            Items = items
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var order = await _context.PurchaseOrders
            .AsNoTracking()
            .Include(po => po.Vendor)
            .Include(po => po.PurchaseRequest)
            .Include(po => po.Lines)
            .Include(po => po.Invoices)
            .FirstOrDefaultAsync(po => po.Id == id);

        if (order is null)
        {
            return NotFound();
        }

        return View(order);
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public IActionResult Create(int? vendorId, bool allowBlacklisted = false)
    {
        if (vendorId.HasValue)
        {
            TempData["VendorWarning"] = "Purchase orders are created from approved purchase requests. Start a PR first.";
            return RedirectToAction("Create", "PurchaseRequests", new { suggestedVendorId = vendorId.Value });
        }

        TempData["VendorWarning"] = "Purchase orders are created from approved purchase requests.";
        return RedirectToAction("Index", "PurchaseRequests");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public IActionResult Create(PurchaseOrderCreateVm vm, bool allowBlacklisted = false)
    {
        TempData["VendorWarning"] = "Direct PO creation is disabled. Convert an approved purchase request to a PO.";
        return RedirectToAction("Index", "PurchaseRequests");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SyncStatuses()
    {
        await _purchaseOrderStatusSyncService.SyncAllPurchaseOrderStatusesAsync();
        TempData["VendorSuccess"] = "Purchase order statuses synchronized successfully.";
        return RedirectToAction(nameof(Index));
    }
}
