using AssetTracker.Data;
using AssetTracker.Models.Vendors;
using AssetTracker.Models.Vendors.ViewModels;
using AssetTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace AssetTracker.Controllers;

[Authorize(Roles = "Admin,Staff")]
public class VendorsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IVendorKpiService _vendorKpiService;
    private readonly IVendorCodeGenerator _codeGenerator;
    private readonly ILogger<VendorsController> _logger;

    public VendorsController(
        ApplicationDbContext context,
        IVendorKpiService vendorKpiService,
        IVendorCodeGenerator codeGenerator,
        ILogger<VendorsController> logger)
    {
        _context = context;
        _vendorKpiService = vendorKpiService;
        _codeGenerator = codeGenerator;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string archiveView = "active", string? q = null, VendorCategory? category = null, VendorStatus? status = null, bool preferredOnly = false)
    {
        archiveView = NormalizeArchiveView(archiveView);
        var normalizedQ = q?.Trim();
        var query = _context.Vendors
            .AsNoTracking()
            .AsQueryable();

        query = archiveView switch
        {
            "archived" => query.Where(v => v.IsArchived),
            "all" => query,
            _ => query.Where(v => !v.IsArchived)
        };

        if (!string.IsNullOrWhiteSpace(normalizedQ))
        {
            var lowered = normalizedQ.ToLower();
            query = query.Where(v =>
                v.VendorCode.ToLower().Contains(lowered) ||
                v.CompanyName.ToLower().Contains(lowered) ||
                (v.Email != null && v.Email.ToLower().Contains(lowered)) ||
                (v.Phone != null && v.Phone.ToLower().Contains(lowered)));
        }

        if (category.HasValue)
        {
            query = query.Where(v => v.Category == category.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(v => v.Status == status.Value);
        }

        if (preferredOnly)
        {
            query = query.Where(v => v.IsPreferredVendor);
        }

        var vendors = await query
            .OrderBy(v => v.CompanyName)
            .Select(v => new
            {
                v.Id,
                v.VendorCode,
                v.CompanyName,
                v.Category,
                v.Status,
                v.IsPreferredVendor,
                v.IsArchived,
                v.ArchivedAt
            })
            .ToListAsync();

        var vendorIds = vendors.Select(v => v.Id).ToList();
        var spendByVendor = await _context.PurchaseOrders
            .AsNoTracking()
            .Where(po => vendorIds.Contains(po.VendorId) &&
                         (po.Status == PurchaseOrderStatus.Paid ||
                          po.Status == PurchaseOrderStatus.Closed ||
                          po.Status == PurchaseOrderStatus.Received))
            .GroupBy(po => po.VendorId)
            .Select(g => new { VendorId = g.Key, Total = g.Sum(x => x.TotalAmount) })
            .ToDictionaryAsync(x => x.VendorId, x => x.Total);

        var lastPoByVendor = await _context.PurchaseOrders
            .AsNoTracking()
            .Where(po => vendorIds.Contains(po.VendorId))
            .GroupBy(po => po.VendorId)
            .Select(g => new { VendorId = g.Key, LastDate = (DateTime?)g.Max(x => x.OrderDate) })
            .ToDictionaryAsync(x => x.VendorId, x => x.LastDate);

        var lastInvoiceByVendor = await _context.VendorInvoices
            .AsNoTracking()
            .Where(i => vendorIds.Contains(i.VendorId))
            .GroupBy(i => i.VendorId)
            .Select(g => new { VendorId = g.Key, LastDate = (DateTime?)g.Max(x => x.InvoiceDate) })
            .ToDictionaryAsync(x => x.VendorId, x => x.LastDate);

        var contractRows = await _context.VendorContracts
            .AsNoTracking()
            .Where(c => vendorIds.Contains(c.VendorId))
            .Select(c => new { c.VendorId, c.EndDate, c.Status })
            .ToListAsync();

        var latestContractStatus = contractRows
            .GroupBy(x => x.VendorId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.EndDate).ThenByDescending(x => x.Status).First().Status);

        var items = vendors.Select(v =>
        {
            var spend = spendByVendor.GetValueOrDefault(v.Id, 0m);
            var lastPo = lastPoByVendor.GetValueOrDefault(v.Id);
            var lastInv = lastInvoiceByVendor.GetValueOrDefault(v.Id);
            ContractStatus? contractStatus = latestContractStatus.TryGetValue(v.Id, out var statusValue)
                ? statusValue
                : null;

            DateTime? lastTransaction = null;
            if (lastPo.HasValue || lastInv.HasValue)
            {
                lastTransaction = new[] { lastPo, lastInv }.Where(d => d.HasValue).Max();
            }

            return new VendorListItemVm
            {
                Id = v.Id,
                VendorCode = v.VendorCode,
                CompanyName = v.CompanyName,
                Category = v.Category,
                Status = v.Status,
                IsPreferredVendor = v.IsPreferredVendor,
                IsArchived = v.IsArchived,
                ArchivedAt = v.ArchivedAt,
                LastTransactionDate = lastTransaction,
                ContractStatus = contractStatus,
                SpendAmount = spend
            };
        }).ToList();

        var vm = new VendorIndexVm
        {
            ArchiveView = archiveView,
            Query = normalizedQ,
            Category = category,
            Status = status,
            PreferredOnly = preferredOnly,
            Kpis = await _vendorKpiService.BuildSummaryAsync(),
            Items = items
        };

        return View(vm);
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public IActionResult Create()
    {
        return View(new CreateVendorVm
        {
            Status = VendorStatus.Active
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(CreateVendorVm vm)
    {
        _logger.LogInformation("Create Vendor POST received.");

        NormalizeVendorVmFields(vm);

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Create Vendor validation failed: {Errors}", string.Join(" | ", GetModelErrors()));
            return View(vm);
        }

        var vendor = new Vendor
        {
            CompanyName = vm.CompanyName,
            Category = vm.Category,
            Website = vm.Website,
            Email = vm.Email,
            Phone = vm.Phone,
            PhysicalAddress = vm.PhysicalAddress,
            AccountManagerName = vm.AccountManagerName,
            AccountManagerEmail = vm.AccountManagerEmail,
            AccountManagerPhone = vm.AccountManagerPhone,
            Status = vm.Status,
            IsPreferredVendor = vm.IsPreferredVendor,
            Notes = vm.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var duplicateNameExists = await _context.Vendors
            .AsNoTracking()
            .AnyAsync(v => !v.IsArchived && v.CompanyName.ToLower() == vendor.CompanyName.ToLower());

        var attempts = 0;
        while (attempts < 5)
        {
            attempts++;
            var candidate = await _codeGenerator.NextVendorCodeAsync();
            var exists = await _context.Vendors.AsNoTracking().AnyAsync(v => v.VendorCode == candidate);
            if (exists)
            {
                continue;
            }

            vendor.VendorCode = candidate;
            break;
        }

        if (string.IsNullOrWhiteSpace(vendor.VendorCode))
        {
            ModelState.AddModelError(string.Empty, "Unable to generate a unique vendor code. Please retry.");
            _logger.LogError("Create Vendor failed: VendorCode generation returned empty after retries.");
            return View(vm);
        }

        try
        {
            _context.Vendors.Add(vendor);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Vendor created successfully. VendorId={VendorId}, VendorCode={VendorCode}", vendor.Id, vendor.VendorCode);
        }
        catch (DbUpdateException ex)
        {
            ModelState.AddModelError(string.Empty, "Unable to save vendor right now. Please retry.");
            _logger.LogError(ex, "Create Vendor failed during SaveChanges.");
            return View(vm);
        }

        if (duplicateNameExists)
        {
            TempData["VendorWarning"] = "A vendor with a similar company name already exists. Please verify duplicates.";
        }

        TempData["VendorSuccess"] = "Vendor created successfully.";
        return RedirectToAction(nameof(Details), new { id = vendor.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var vendor = await _context.Vendors
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id);
        if (vendor is null)
        {
            return NotFound();
        }

        var linkedAssets = await _context.Assets
            .AsNoTracking()
            .Where(a => a.VendorId == vendor.Id)
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Take(50)
            .ToListAsync();

        var purchaseOrders = await _context.PurchaseOrders
            .AsNoTracking()
            .Include(po => po.Lines)
            .Where(po => po.VendorId == vendor.Id)
            .OrderByDescending(po => po.OrderDate)
            .ThenByDescending(po => po.Id)
            .Take(50)
            .ToListAsync();

        var invoices = await _context.VendorInvoices
            .AsNoTracking()
            .Include(i => i.PurchaseOrder)
            .Where(i => i.VendorId == vendor.Id)
            .OrderByDescending(i => i.InvoiceDate)
            .ThenByDescending(i => i.Id)
            .Take(50)
            .ToListAsync();

        var contracts = await _context.VendorContracts
            .AsNoTracking()
            .Where(c => c.VendorId == vendor.Id)
            .OrderByDescending(c => c.EndDate)
            .ThenByDescending(c => c.Id)
            .Take(50)
            .ToListAsync();

        var supportTickets = await _context.VendorSupportTickets
            .AsNoTracking()
            .Include(t => t.Asset)
            .Where(t => t.VendorId == vendor.Id)
            .OrderByDescending(t => t.OpenedDate)
            .ThenByDescending(t => t.Id)
            .Take(50)
            .ToListAsync();

        var documents = await _context.VendorDocuments
            .AsNoTracking()
            .Where(d => d.VendorId == vendor.Id)
            .OrderByDescending(d => d.UploadedAt)
            .ThenByDescending(d => d.Id)
            .Take(50)
            .ToListAsync();

        var totalSpend = purchaseOrders
            .Where(po => po.Status is PurchaseOrderStatus.Paid or PurchaseOrderStatus.Closed or PurchaseOrderStatus.Received)
            .Sum(po => po.TotalAmount);

        var transactionDates = new DateTime?[]
        {
            purchaseOrders.Select(po => (DateTime?)po.OrderDate).DefaultIfEmpty().Max(),
            invoices.Select(i => (DateTime?)i.InvoiceDate).DefaultIfEmpty().Max()
        }.Where(d => d.HasValue).ToList();

        var lastTransaction = transactionDates.Count > 0
            ? transactionDates.Max()
            : null;

        var vm = new VendorDetailsVm
        {
            Vendor = vendor,
            LinkedAssets = linkedAssets,
            PurchaseOrders = purchaseOrders,
            Invoices = invoices,
            Contracts = contracts,
            SupportTickets = supportTickets,
            Documents = documents,
            TotalSpend = totalSpend,
            LastTransactionDate = lastTransaction,
            ActiveContracts = contracts.Count(c => c.Status == ContractStatus.Active),
            PendingInvoices = invoices.Count(i => i.PaymentStatus is InvoicePaymentStatus.Pending or InvoicePaymentStatus.Overdue),
            OverdueInvoices = invoices.Count(i => i.PaymentStatus == InvoicePaymentStatus.Overdue || (i.PaymentStatus == InvoicePaymentStatus.Pending && i.DueDate.Date < DateTime.UtcNow.Date))
        };

        return View(vm);
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.Id == id && !v.IsArchived);
        if (vendor is null)
        {
            return NotFound();
        }

        return View(vendor);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id, [Bind("Id,VendorCode,CompanyName,Category,Website,Email,Phone,PhysicalAddress,AccountManagerName,AccountManagerEmail,AccountManagerPhone,Status,IsPreferredVendor,Notes")] Vendor model)
    {
        if (id != model.Id)
        {
            return NotFound();
        }

        NormalizeVendorFields(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.Id == id && !v.IsArchived);
        if (vendor is null)
        {
            return NotFound();
        }

        vendor.CompanyName = model.CompanyName;
        vendor.Category = model.Category;
        vendor.Website = model.Website;
        vendor.Email = model.Email;
        vendor.Phone = model.Phone;
        vendor.PhysicalAddress = model.PhysicalAddress;
        vendor.AccountManagerName = model.AccountManagerName;
        vendor.AccountManagerEmail = model.AccountManagerEmail;
        vendor.AccountManagerPhone = model.AccountManagerPhone;
        vendor.Status = model.Status;
        vendor.IsPreferredVendor = model.IsPreferredVendor;
        vendor.Notes = model.Notes;
        vendor.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(string.Empty, "Unable to save changes right now. Please retry.");
            return View(model);
        }

        return RedirectToAction(nameof(Details), new { id = vendor.Id });
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int? id)
    {
        return await Archive(id);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteConfirmed(int id, string? archiveReason = null)
    {
        return await ArchiveConfirmed(id, archiveReason);
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Archive(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var vendor = await _context.Vendors
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id);
        if (vendor is null)
        {
            return NotFound();
        }

        return View(nameof(Delete), vendor);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ArchiveConfirmed(int id, string? archiveReason = null)
    {
        var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.Id == id);
        if (vendor is null)
        {
            return NotFound();
        }

        if (vendor.IsArchived)
        {
            TempData["VendorWarning"] = "Vendor is already archived.";
            return RedirectToAction(nameof(Details), new { id = vendor.Id });
        }

        vendor.IsArchived = true;
        vendor.ArchivedAt = DateTime.UtcNow;
        vendor.ArchivedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        vendor.ArchiveReason = string.IsNullOrWhiteSpace(archiveReason) ? null : archiveReason.Trim();
        vendor.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["VendorSuccess"] = "Vendor archived successfully.";
        return RedirectToAction(nameof(Details), new { id = vendor.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Restore(int id)
    {
        var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.Id == id);
        if (vendor is null)
        {
            return NotFound();
        }

        if (!vendor.IsArchived)
        {
            TempData["VendorWarning"] = "Vendor is already active.";
            return RedirectToAction(nameof(Details), new { id = vendor.Id });
        }

        vendor.IsArchived = false;
        vendor.ArchivedAt = null;
        vendor.ArchivedByUserId = null;
        vendor.ArchiveReason = null;
        vendor.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["VendorSuccess"] = "Vendor restored successfully.";
        return RedirectToAction(nameof(Details), new { id = vendor.Id });
    }

    private static string NormalizeArchiveView(string? archiveView)
    {
        if (string.IsNullOrWhiteSpace(archiveView))
        {
            return "active";
        }

        var normalized = archiveView.Trim().ToLowerInvariant();
        return normalized switch
        {
            "active" => "active",
            "archived" => "archived",
            "all" => "all",
            _ => "active"
        };
    }

    private static void NormalizeVendorFields(Vendor vendor)
    {
        vendor.CompanyName = vendor.CompanyName.Trim();
        vendor.Website = string.IsNullOrWhiteSpace(vendor.Website) ? null : vendor.Website.Trim();
        vendor.Email = string.IsNullOrWhiteSpace(vendor.Email) ? null : vendor.Email.Trim();
        vendor.Phone = string.IsNullOrWhiteSpace(vendor.Phone) ? null : vendor.Phone.Trim();
        vendor.PhysicalAddress = string.IsNullOrWhiteSpace(vendor.PhysicalAddress) ? null : vendor.PhysicalAddress.Trim();
        vendor.AccountManagerName = string.IsNullOrWhiteSpace(vendor.AccountManagerName) ? null : vendor.AccountManagerName.Trim();
        vendor.AccountManagerEmail = string.IsNullOrWhiteSpace(vendor.AccountManagerEmail) ? null : vendor.AccountManagerEmail.Trim();
        vendor.AccountManagerPhone = string.IsNullOrWhiteSpace(vendor.AccountManagerPhone) ? null : vendor.AccountManagerPhone.Trim();
        vendor.Notes = string.IsNullOrWhiteSpace(vendor.Notes) ? null : vendor.Notes.Trim();
    }

    private static void NormalizeVendorVmFields(CreateVendorVm vm)
    {
        vm.CompanyName = vm.CompanyName.Trim();
        vm.Website = string.IsNullOrWhiteSpace(vm.Website) ? null : vm.Website.Trim();
        vm.Email = string.IsNullOrWhiteSpace(vm.Email) ? null : vm.Email.Trim();
        vm.Phone = string.IsNullOrWhiteSpace(vm.Phone) ? null : vm.Phone.Trim();
        vm.PhysicalAddress = string.IsNullOrWhiteSpace(vm.PhysicalAddress) ? null : vm.PhysicalAddress.Trim();
        vm.AccountManagerName = string.IsNullOrWhiteSpace(vm.AccountManagerName) ? null : vm.AccountManagerName.Trim();
        vm.AccountManagerEmail = string.IsNullOrWhiteSpace(vm.AccountManagerEmail) ? null : vm.AccountManagerEmail.Trim();
        vm.AccountManagerPhone = string.IsNullOrWhiteSpace(vm.AccountManagerPhone) ? null : vm.AccountManagerPhone.Trim();
        vm.Notes = string.IsNullOrWhiteSpace(vm.Notes) ? null : vm.Notes.Trim();
    }

    private IEnumerable<string> GetModelErrors()
    {
        return ModelState
            .Where(kvp => kvp.Value?.Errors.Count > 0)
            .SelectMany(kvp => kvp.Value!.Errors.Select(err => $"{kvp.Key}: {err.ErrorMessage}"));
    }
}
