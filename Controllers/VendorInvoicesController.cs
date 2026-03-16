using AssetTracker.Data;
using AssetTracker.Models.Vendors;
using AssetTracker.Models.Vendors.ViewModels;
using AssetTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AssetTracker.Controllers;

[Authorize(Roles = "Admin,Staff")]
public class VendorInvoicesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IPurchaseOrderStatusSyncService _purchaseOrderStatusSyncService;

    public VendorInvoicesController(
        ApplicationDbContext context,
        IPurchaseOrderStatusSyncService purchaseOrderStatusSyncService)
    {
        _context = context;
        _purchaseOrderStatusSyncService = purchaseOrderStatusSyncService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q = null, InvoicePaymentStatus? status = null)
    {
        var normalizedQ = q?.Trim();
        var today = DateTime.UtcNow.Date;

        var query = _context.VendorInvoices
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(normalizedQ))
        {
            var lowered = normalizedQ.ToLower();
            query = query.Where(i =>
                i.InvoiceNumber.ToLower().Contains(lowered) ||
                i.Vendor.CompanyName.ToLower().Contains(lowered) ||
                (i.PurchaseOrder != null && i.PurchaseOrder.PoNumber.ToLower().Contains(lowered)));
        }

        if (status.HasValue)
        {
            query = query.Where(i => i.PaymentStatus == status.Value);
        }

        var rawItems = await query
            .OrderByDescending(i => i.InvoiceDate)
            .ThenByDescending(i => i.Id)
            .Select(i => new
            {
                i.Id,
                i.VendorId,
                i.InvoiceNumber,
                i.InvoiceDate,
                i.DueDate,
                i.PaymentStatus,
                i.Amount,
                i.Currency,
                VendorName = i.Vendor.CompanyName,
                LinkedPoNumber = i.PurchaseOrder != null ? i.PurchaseOrder.PoNumber : null
            })
            .ToListAsync();

        var items = rawItems.Select(i =>
        {
            var effectiveStatus = i.PaymentStatus == InvoicePaymentStatus.Pending && i.DueDate.Date < today
                ? InvoicePaymentStatus.Overdue
                : i.PaymentStatus;

            return new VendorInvoiceListItemVm
            {
                Id = i.Id,
                VendorId = i.VendorId,
                InvoiceNumber = i.InvoiceNumber,
                InvoiceDate = i.InvoiceDate,
                DueDate = i.DueDate,
                PaymentStatus = i.PaymentStatus,
                EffectiveStatus = effectiveStatus,
                Amount = i.Amount,
                Currency = i.Currency,
                VendorName = i.VendorName,
                LinkedPoNumber = string.IsNullOrWhiteSpace(i.LinkedPoNumber) ? "-" : i.LinkedPoNumber
            };
        }).ToList();

        var totalInvoices = await _context.VendorInvoices
            .AsNoTracking()
            .CountAsync();

        var paidInvoices = await _context.VendorInvoices
            .AsNoTracking()
            .CountAsync(i => i.PaymentStatus == InvoicePaymentStatus.Paid);

        var pendingInvoices = await _context.VendorInvoices
            .AsNoTracking()
            .CountAsync(i => i.PaymentStatus != InvoicePaymentStatus.Paid && i.PaymentStatus != InvoicePaymentStatus.Cancelled);

        var overdueInvoices = await _context.VendorInvoices
            .AsNoTracking()
            .CountAsync(i => i.PaymentStatus == InvoicePaymentStatus.Overdue || (i.PaymentStatus == InvoicePaymentStatus.Pending && i.DueDate.Date < today));

        var vm = new VendorInvoiceIndexVm
        {
            Query = normalizedQ,
            StatusFilter = status,
            TotalInvoices = totalInvoices,
            PendingInvoices = pendingInvoices,
            PaidInvoices = paidInvoices,
            OverdueInvoices = overdueInvoices,
            Items = items
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var invoice = await _context.VendorInvoices
            .AsNoTracking()
            .Include(i => i.Vendor)
            .Include(i => i.PurchaseOrder)
                .ThenInclude(po => po!.PurchaseRequest)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (invoice is null)
        {
            return NotFound();
        }

        return View(invoice);
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(int vendorId)
    {
        var vendor = await _context.Vendors
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == vendorId && !v.IsArchived);
        if (vendor is null)
        {
            return NotFound();
        }

        await PopulatePurchaseOrdersAsync(vendorId, null);
        ViewData["VendorLabel"] = $"{vendor.CompanyName} ({vendor.VendorCode})";
        return View(new VendorInvoiceCreateVm
        {
            VendorId = vendorId,
            InvoiceDate = DateTime.UtcNow.Date,
            DueDate = DateTime.UtcNow.Date.AddDays(30),
            PaymentStatus = InvoicePaymentStatus.Pending,
            Currency = "KES"
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(VendorInvoiceCreateVm vm)
    {
        NormalizeCreateVm(vm);
        var vendor = await ValidateInvoiceVmAsync(vm);

        if (!ModelState.IsValid)
        {
            await PopulatePurchaseOrdersAsync(vm.VendorId, vm.PurchaseOrderId);
            ViewData["VendorLabel"] = vendor is null ? "Vendor" : $"{vendor.CompanyName} ({vendor.VendorCode})";
            return View(vm);
        }

        var invoice = new VendorInvoice
        {
            VendorId = vm.VendorId,
            InvoiceNumber = vm.InvoiceNumber,
            InvoiceDate = vm.InvoiceDate.Date,
            DueDate = vm.DueDate.Date,
            Amount = vm.Amount,
            Currency = vm.Currency,
            PaymentStatus = vm.PaymentStatus,
            PaidDate = vm.PaymentStatus == InvoicePaymentStatus.Paid ? DateTime.UtcNow : null,
            PurchaseOrderId = vm.PurchaseOrderId,
            Notes = vm.Notes,
            CreatedAt = DateTime.UtcNow
        };

        _context.VendorInvoices.Add(invoice);
        await _context.SaveChangesAsync();
        if (invoice.PurchaseOrderId.HasValue)
        {
            await _purchaseOrderStatusSyncService.SyncPurchaseOrderStatusAsync(invoice.PurchaseOrderId.Value);
        }
        TempData["VendorSuccess"] = "Invoice logged successfully.";
        return RedirectToAction("Details", "Vendors", new { id = vm.VendorId });
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var invoice = await _context.VendorInvoices
            .AsNoTracking()
            .Include(i => i.Vendor)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (invoice is null)
        {
            return NotFound();
        }

        if (invoice.Vendor.IsArchived)
        {
            TempData["VendorWarning"] = "Cannot edit invoices for archived vendors. Restore the vendor first.";
            return RedirectToAction("Details", "Vendors", new { id = invoice.VendorId });
        }

        await PopulatePurchaseOrdersAsync(invoice.VendorId, invoice.PurchaseOrderId);
        ViewData["VendorLabel"] = $"{invoice.Vendor.CompanyName} ({invoice.Vendor.VendorCode})";

        var vm = new EditVendorInvoiceVm
        {
            Id = invoice.Id,
            VendorId = invoice.VendorId,
            InvoiceNumber = invoice.InvoiceNumber,
            InvoiceDate = invoice.InvoiceDate.Date,
            DueDate = invoice.DueDate.Date,
            Amount = invoice.Amount,
            Currency = invoice.Currency,
            PaymentStatus = invoice.PaymentStatus,
            PurchaseOrderId = invoice.PurchaseOrderId,
            Notes = invoice.Notes
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id, EditVendorInvoiceVm vm)
    {
        if (id != vm.Id)
        {
            return NotFound();
        }

        NormalizeEditVm(vm);

        var invoice = await _context.VendorInvoices
            .Include(i => i.Vendor)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (invoice is null)
        {
            return NotFound();
        }

        if (invoice.Vendor.IsArchived)
        {
            TempData["VendorWarning"] = "Cannot edit invoices for archived vendors. Restore the vendor first.";
            return RedirectToAction("Details", "Vendors", new { id = invoice.VendorId });
        }

        var vendor = invoice.Vendor;

        if (vm.VendorId != invoice.VendorId)
        {
            ModelState.AddModelError(nameof(EditVendorInvoiceVm.VendorId), "Vendor mismatch for this invoice.");
        }

        await ValidateInvoiceRulesAsync(vm.VendorId, vm.InvoiceNumber, vm.InvoiceDate, vm.DueDate, vm.Amount, vm.PurchaseOrderId, excludeInvoiceId: vm.Id);

        if (!ModelState.IsValid)
        {
            await PopulatePurchaseOrdersAsync(vm.VendorId, vm.PurchaseOrderId);
            ViewData["VendorLabel"] = $"{vendor.CompanyName} ({vendor.VendorCode})";
            return View(vm);
        }

        invoice.InvoiceNumber = vm.InvoiceNumber;
        invoice.InvoiceDate = vm.InvoiceDate.Date;
        invoice.DueDate = vm.DueDate.Date;
        invoice.Amount = vm.Amount;
        invoice.Currency = vm.Currency;
        var previousPurchaseOrderId = invoice.PurchaseOrderId;
        invoice.PurchaseOrderId = vm.PurchaseOrderId;
        invoice.Notes = vm.Notes;

        if (vm.PaymentStatus == InvoicePaymentStatus.Paid)
        {
            invoice.PaymentStatus = InvoicePaymentStatus.Paid;
            invoice.PaidDate ??= DateTime.UtcNow;
        }
        else
        {
            invoice.PaymentStatus = vm.PaymentStatus;
            invoice.PaidDate = null;
        }

        await _context.SaveChangesAsync();
        var purchaseOrderIdsToSync = new HashSet<int>();
        if (previousPurchaseOrderId.HasValue)
        {
            purchaseOrderIdsToSync.Add(previousPurchaseOrderId.Value);
        }

        if (invoice.PurchaseOrderId.HasValue)
        {
            purchaseOrderIdsToSync.Add(invoice.PurchaseOrderId.Value);
        }

        foreach (var purchaseOrderId in purchaseOrderIdsToSync)
        {
            await _purchaseOrderStatusSyncService.SyncPurchaseOrderStatusAsync(purchaseOrderId);
        }

        TempData["VendorSuccess"] = "Invoice updated successfully.";
        return RedirectToAction("Details", "Vendors", new { id = vm.VendorId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> MarkAsPaid(int id)
    {
        var invoice = await _context.VendorInvoices
            .Include(i => i.Vendor)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (invoice is null)
        {
            return NotFound();
        }

        if (invoice.Vendor.IsArchived)
        {
            TempData["VendorWarning"] = "Cannot update invoices for archived vendors. Restore the vendor first.";
            return RedirectToAction("Details", "Vendors", new { id = invoice.VendorId });
        }

        if (invoice.PaymentStatus is InvoicePaymentStatus.Paid or InvoicePaymentStatus.Cancelled)
        {
            TempData["VendorWarning"] = "Only pending or overdue invoices can be marked as paid.";
            return RedirectToAction("Details", "Vendors", new { id = invoice.VendorId });
        }

        invoice.PaymentStatus = InvoicePaymentStatus.Paid;
        invoice.PaidDate = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        if (invoice.PurchaseOrderId.HasValue)
        {
            await _purchaseOrderStatusSyncService.SyncPurchaseOrderStatusAsync(invoice.PurchaseOrderId.Value);
        }

        TempData["VendorSuccess"] = "Invoice marked as paid.";
        return RedirectToAction("Details", "Vendors", new { id = invoice.VendorId });
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var invoice = await _context.VendorInvoices
            .AsNoTracking()
            .Include(i => i.Vendor)
            .Include(i => i.PurchaseOrder)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (invoice is null)
        {
            return NotFound();
        }

        return View(invoice);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var invoice = await _context.VendorInvoices.FirstOrDefaultAsync(i => i.Id == id);
        if (invoice is null)
        {
            return NotFound();
        }

        var vendorId = invoice.VendorId;
        var purchaseOrderId = invoice.PurchaseOrderId;
        _context.VendorInvoices.Remove(invoice);
        await _context.SaveChangesAsync();
        if (purchaseOrderId.HasValue)
        {
            await _purchaseOrderStatusSyncService.SyncPurchaseOrderStatusAsync(purchaseOrderId.Value);
        }

        TempData["VendorSuccess"] = "Invoice deleted successfully.";
        return RedirectToAction("Details", "Vendors", new { id = vendorId });
    }

    private async Task PopulatePurchaseOrdersAsync(int vendorId, int? selectedPurchaseOrderId)
    {
        var orders = await _context.PurchaseOrders
            .AsNoTracking()
            .Where(po => po.VendorId == vendorId)
            .OrderByDescending(po => po.OrderDate)
            .Select(po => new
            {
                po.Id,
                Label = $"{po.PoNumber} - {po.TotalAmount:N2} {po.Currency}"
            })
            .ToListAsync();

        ViewData["PurchaseOrderId"] = new SelectList(orders, "Id", "Label", selectedPurchaseOrderId);
    }

    private async Task<Vendor?> ValidateInvoiceVmAsync(VendorInvoiceCreateVm vm)
    {
        var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.Id == vm.VendorId && !v.IsArchived);
        if (vendor is null)
        {
            ModelState.AddModelError(nameof(VendorInvoiceCreateVm.VendorId), "Selected vendor does not exist.");
        }

        await ValidateInvoiceRulesAsync(vm.VendorId, vm.InvoiceNumber, vm.InvoiceDate, vm.DueDate, vm.Amount, vm.PurchaseOrderId, excludeInvoiceId: null);
        return vendor;
    }

    private async Task ValidateInvoiceRulesAsync(
        int vendorId,
        string invoiceNumber,
        DateTime invoiceDate,
        DateTime dueDate,
        decimal amount,
        int? purchaseOrderId,
        int? excludeInvoiceId)
    {
        if (dueDate.Date < invoiceDate.Date)
        {
            ModelState.AddModelError(nameof(VendorInvoiceCreateVm.DueDate), "Due date cannot be before invoice date.");
        }

        if (amount <= 0)
        {
            ModelState.AddModelError(nameof(VendorInvoiceCreateVm.Amount), "Amount must be greater than zero.");
        }

        if (!string.IsNullOrWhiteSpace(invoiceNumber))
        {
            var normalizedInvoiceNumber = invoiceNumber.Trim().ToLower();
            var query = _context.VendorInvoices
                .AsNoTracking()
                .Where(i => i.VendorId == vendorId && i.InvoiceNumber.ToLower() == normalizedInvoiceNumber);

            if (excludeInvoiceId.HasValue)
            {
                query = query.Where(i => i.Id != excludeInvoiceId.Value);
            }

            var duplicateInvoice = await query.AnyAsync();
            if (duplicateInvoice)
            {
                ModelState.AddModelError(nameof(VendorInvoiceCreateVm.InvoiceNumber), "Invoice number already exists for this vendor.");
            }
        }

        if (purchaseOrderId.HasValue)
        {
            var orderBelongsToVendor = await _context.PurchaseOrders
                .AsNoTracking()
                .AnyAsync(po => po.Id == purchaseOrderId.Value && po.VendorId == vendorId);
            if (!orderBelongsToVendor)
            {
                ModelState.AddModelError(nameof(VendorInvoiceCreateVm.PurchaseOrderId), "Selected purchase order does not belong to this vendor.");
            }
        }
    }

    private static void NormalizeCreateVm(VendorInvoiceCreateVm vm)
    {
        vm.InvoiceNumber = vm.InvoiceNumber?.Trim() ?? string.Empty;
        vm.Currency = string.IsNullOrWhiteSpace(vm.Currency) ? "KES" : vm.Currency.Trim().ToUpperInvariant();
        vm.Notes = string.IsNullOrWhiteSpace(vm.Notes) ? null : vm.Notes.Trim();
    }

    private static void NormalizeEditVm(EditVendorInvoiceVm vm)
    {
        vm.InvoiceNumber = vm.InvoiceNumber?.Trim() ?? string.Empty;
        vm.Currency = string.IsNullOrWhiteSpace(vm.Currency) ? "KES" : vm.Currency.Trim().ToUpperInvariant();
        vm.Notes = string.IsNullOrWhiteSpace(vm.Notes) ? null : vm.Notes.Trim();
    }
}
